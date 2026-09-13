using System.Collections.ObjectModel;
using System.Text;
using AblationStudio.Core.History;
using AblationStudio.Core.History.Actions;
using AblationStudio.Core.Models;
using AblationStudio.Core.Projects;

namespace AblationStudio.Core.Shapes;

public sealed class ShapeDocument
{
    private readonly ObservableCollection<ToolpathShape> _shapes = [];
    private ToolpathShape? _selectedShape;

    private ToolpathShape? _activeEditingShape;
    private ToolpathShape? _activeBeforeSnapshot;
    private string? _activeEditingProperty;

    public ReadOnlyObservableCollection<ToolpathShape> Shapes { get; }
    public UndoRedoManager UndoManager { get; } = new();

    public ToolpathShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                if (_selectedShape is not null)
                {
                    _selectedShape.IsSelected = false;
                }

                _selectedShape = value;

                if (_selectedShape is not null)
                {
                    _selectedShape.IsSelected = true;
                }

                SelectionChanged?.Invoke(_selectedShape);
            }
        }
    }

    public event Action? DocumentChanged;
    public event Action<ToolpathShape?>? SelectionChanged;

    public bool IsDragging { get; set; }

    public void NotifyDocumentChanged()
    {
        DocumentChanged?.Invoke();
    }

    public ShapeDocument()
    {
        Shapes = new ReadOnlyObservableCollection<ToolpathShape>(_shapes);
    }

    public void AddShape(ToolpathShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        shape.ShapeChanged += OnShapeChanged;
        shape.PropertyChanging += OnShapePropertyChanging;
        _shapes.Add(shape);
        SelectedShape = shape;
        DocumentChanged?.Invoke();
    }

    public void InsertShape(int index, ToolpathShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        shape.ShapeChanged += OnShapeChanged;
        shape.PropertyChanging += OnShapePropertyChanging;
        int clamped = Math.Clamp(index, 0, _shapes.Count);
        _shapes.Insert(clamped, shape);
        SelectedShape = shape;
        DocumentChanged?.Invoke();
    }

    public bool RemoveShape(ToolpathShape shape)
    {
        shape.ShapeChanged -= OnShapeChanged;
        shape.PropertyChanging -= OnShapePropertyChanging;
        bool removed = _shapes.Remove(shape);
        if (removed)
        {
            if (SelectedShape == shape)
            {
                SelectedShape = null;
            }
            DocumentChanged?.Invoke();
        }
        return removed;
    }

    public void Clear()
    {
        foreach (ToolpathShape s in _shapes)
        {
            s.ShapeChanged -= OnShapeChanged;
            s.PropertyChanging -= OnShapePropertyChanging;
            s.IsSelected = false;
        }

        _shapes.Clear();
        SelectedShape = null;
        FlushPropertyCoalescing();
        DocumentChanged?.Invoke();
    }

    public void FlushPropertyCoalescing()
    {
        _activeEditingShape = null;
        _activeEditingProperty = null;
        _activeBeforeSnapshot = null;
        UndoManager.FlushCoalescing();
    }

    public ToolpathShape? HitTest(float worldX, float worldY, float tolerance = 0.5f)
    {
        // Hit-test in reverse order to select the topmost (most recently added) shape first
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            if (_shapes[i].HitTest(worldX, worldY, tolerance))
            {
                return _shapes[i];
            }
        }

        return null;
    }

    public Toolpath CompileToolpath(string name = "CustomShapes.h", bool includeHomeTransitions = false)
    {
        var segments = new List<ToolpathSegment>();
        if (_shapes.Count == 0)
        {
            return new Toolpath(name, string.Empty, segments);
        }

        var home = new ToolpathPoint(0f, 0f, 0f);
        ToolpathPoint? currentPosition = includeHomeTransitions ? home : null;

        foreach (ToolpathShape shape in _shapes)
        {
            foreach (ToolpathSegment seg in shape.GenerateSegments(currentPosition))
            {
                segments.Add(seg);
                currentPosition = seg.End;
            }
        }

        if (includeHomeTransitions && currentPosition.HasValue && currentPosition.Value.DistanceTo(home) > 0.001f)
        {
            int lastLayer = segments.Count > 0 ? segments[^1].LayerId : 1;
            segments.Add(new ToolpathSegment(currentPosition.Value, home, SegmentType.Rapid, lastLayer));
        }

        return new Toolpath(name, string.Empty, segments);
    }

    public ToolpathProject ToProject(string name = "Untitled", ProjectProcessSettings? settings = null)
    {
        return ToolpathProject.FromShapeDocument(this, name, settings);
    }

    public void LoadFromProject(ToolpathProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ApplyTo(this);
    }

    public string ExportToHCode(
        string name = "CustomShapes.h",
        ProjectProcessSettings? settings = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"; Toolpath generated by Ablation Studio - {name}");
        sb.AppendLine("; Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");

        if (_shapes.Count == 0)
        {
            sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");
            return sb.ToString();
        }

        sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");

        int currentLayer = -1;
        SegmentType? activeType = null;
        Toolpath toolpath = CompileToolpath(name, includeHomeTransitions: true);

        for (int i = 0; i < toolpath.Segments.Count; i++)
        {
            ToolpathSegment seg = toolpath.Segments[i];

            // Determine target operation type and layer
            SegmentType targetType;
            int targetLayer;

            if (seg.Type == SegmentType.Rapid)
            {
                // Look ahead to find the next cutting segment (Cut or Hatch) that this rapid positions for
                int nextCutIdx = -1;
                for (int j = i + 1; j < toolpath.Segments.Count; j++)
                {
                    if (toolpath.Segments[j].Type != SegmentType.Rapid)
                    {
                        nextCutIdx = j;
                        break;
                    }
                }

                if (nextCutIdx >= 0)
                {
                    targetType = toolpath.Segments[nextCutIdx].Type;
                    targetLayer = toolpath.Segments[nextCutIdx].LayerId;
                }
                else
                {
                    // Trailing rapid move (e.g. return to origin) remains in active section
                    targetType = activeType ?? SegmentType.Rapid;
                    targetLayer = currentLayer >= 0 ? currentLayer : seg.LayerId;
                }
            }
            else
            {
                targetType = seg.Type;
                targetLayer = seg.LayerId;
            }

            // Emit section directive when entering a new section or layer
            if (targetType == SegmentType.Cut)
            {
                if (activeType != SegmentType.Cut || currentLayer != targetLayer)
                {
                    currentLayer = targetLayer;
                    sb.AppendLine($"PFL {currentLayer} ; Profile");
                    activeType = SegmentType.Cut;
                }
            }
            else if (targetType == SegmentType.Hatch)
            {
                if (activeType != SegmentType.Hatch || currentLayer != targetLayer)
                {
                    currentLayer = targetLayer;
                    sb.AppendLine($"HCH {currentLayer} ; Hatch");
                    activeType = SegmentType.Hatch;
                }
            }

            string command = (seg.Type == SegmentType.Cut || seg.Type == SegmentType.Hatch) ? "M03" : "M05";
            sb.AppendLine($"SL X{seg.End.X:F4} Y{seg.End.Y:F4} Z{seg.End.Z:F4} {command}");
        }

        var origin = new ToolpathPoint(0f, 0f, 0f);
        ToolpathSegment? lastSeg = toolpath.Segments.Count > 0 ? toolpath.Segments[^1] : null;
        if (lastSeg is null || lastSeg.Type != SegmentType.Rapid || lastSeg.End.DistanceTo(origin) > 0.001f)
        {
            sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");
        }

        return sb.ToString();
    }

    private void OnShapePropertyChanging(ToolpathShape shape, string propertyName)
    {
        if (UndoManager.IsPerformingUndoRedo || IsDragging || UndoManager.IsRecordingSuppressed)
        {
            return;
        }

        string coalesceKey = $"{shape.Id}:{propertyName}";
        if (_activeEditingShape == shape && _activeEditingProperty == propertyName && _activeBeforeSnapshot is not null && UndoManager.CanCoalesce(coalesceKey))
        {
            return;
        }

        _activeEditingShape = shape;
        _activeEditingProperty = propertyName;
        _activeBeforeSnapshot = shape.Clone();
    }

    private void OnShapeChanged(ToolpathShape shape)
    {
        if (!UndoManager.IsPerformingUndoRedo && !IsDragging && !UndoManager.IsRecordingSuppressed)
        {
            if (_activeEditingShape == shape && _activeBeforeSnapshot is not null && _activeEditingProperty is not null)
            {
                var afterSnapshot = shape.Clone();
                string coalesceKey = $"{shape.Id}:{_activeEditingProperty}";
                string description = $"Change {_activeEditingProperty}";

                UndoManager.RecordAction(new ModifyShapeAction(
                    shape,
                    _activeBeforeSnapshot,
                    afterSnapshot,
                    description,
                    coalesceKey,
                    this,
                    wasSelected: shape.IsSelected));
            }
        }

        DocumentChanged?.Invoke();
    }
}
