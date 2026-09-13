using System.ComponentModel;
using System.Runtime.CompilerServices;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes.Hatching;

namespace AblationStudio.Core.Shapes;

public abstract class ToolpathShape : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private float _positionX;
    private float _positionY;
    private float _positionZ;
    private int _layerId = 1;
    private SegmentType _cutType = SegmentType.Cut;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<ToolpathShape>? ShapeChanged;

    protected List<ToolpathSegment>? CachedHatchSegments;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public HatchSettings Hatch { get; } = new();

    protected ToolpathShape()
    {
        Hatch.SettingsChanged += () =>
        {
            InvalidateHatchCache();
            OnShapeModified();
        };
    }

    public void InvalidateHatchCache()
    {
        CachedHatchSegments = null;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public float PositionX
    {
        get => _positionX;
        set
        {
            if (SetProperty(ref _positionX, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float PositionY
    {
        get => _positionY;
        set
        {
            if (SetProperty(ref _positionY, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float PositionZ
    {
        get => _positionZ;
        set
        {
            if (SetProperty(ref _positionZ, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public int LayerId
    {
        get => _layerId;
        set
        {
            if (SetProperty(ref _layerId, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public SegmentType CutType
    {
        get => _cutType;
        set
        {
            if (SetProperty(ref _cutType, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public abstract string ShapeType { get; }
    public abstract bool IsClosed { get; }

    public abstract IReadOnlyList<ToolpathPoint> GetPathPoints();

    public virtual IEnumerable<ToolpathSegment> GenerateSegments(ToolpathPoint? currentPosition = null)
    {
        IReadOnlyList<ToolpathPoint> points = GetPathPoints();
        if (points.Count < 2)
        {
            yield break;
        }

        bool hasHatch = IsClosed && Hatch.IsEnabled && Hatch.Pattern != HatchPatternType.None;
        bool keepBoundary = !hasHatch || Hatch.KeepBoundary;

        ToolpathPoint? pos = currentPosition;

        if (keepBoundary)
        {
            // Rapid transition from previous tool position to start of this shape perimeter
            if (pos is not null && pos.Value.DistanceTo(points[0]) > 0.001f)
            {
                yield return new ToolpathSegment(pos.Value, points[0], SegmentType.Rapid, LayerId);
            }

            // Trace shape perimeter
            for (int i = 0; i < points.Count - 1; i++)
            {
                yield return new ToolpathSegment(points[i], points[i + 1], CutType, LayerId);
            }

            // Close shape if requested
            if (IsClosed && points.Count > 2 && points[^1].DistanceTo(points[0]) > 0.001f)
            {
                yield return new ToolpathSegment(points[^1], points[0], CutType, LayerId);
            }

            pos = points[0];
        }

        // Generate inner hatch infill
        if (hasHatch)
        {
            if (CachedHatchSegments is null)
            {
                ToolpathPoint? hatchPos = pos;
                CachedHatchSegments = HatchEngine.GenerateHatch(this, Hatch, ref hatchPos);
            }

            if (CachedHatchSegments.Count > 0)
            {
                if (pos is not null && pos.Value.DistanceTo(CachedHatchSegments[0].Start) > 0.001f)
                {
                    yield return new ToolpathSegment(pos.Value, CachedHatchSegments[0].Start, SegmentType.Rapid, LayerId);
                }

                for (int i = 0; i < CachedHatchSegments.Count; i++)
                {
                    yield return CachedHatchSegments[i];
                }

                pos = CachedHatchSegments[^1].End;
            }
        }
    }

    public virtual BoundingBox3D GetBounds()
    {
        IReadOnlyList<ToolpathPoint> points = GetPathPoints();
        if (points.Count == 0)
        {
            return new BoundingBox3D(PositionX, PositionY, PositionZ, PositionX, PositionY, PositionZ);
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (ToolpathPoint p in points)
        {
            minX = MathF.Min(minX, p.X);
            maxX = MathF.Max(maxX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxY = MathF.Max(maxY, p.Y);
            minZ = MathF.Min(minZ, p.Z);
            maxZ = MathF.Max(maxZ, p.Z);
        }

        return new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public abstract bool HitTest(float worldX, float worldY, float tolerance);

    public virtual void Translate(float deltaX, float deltaY, float deltaZ)
    {
        _positionX += deltaX;
        _positionY += deltaY;
        _positionZ += deltaZ;

        if (CachedHatchSegments is not null)
        {
            for (int i = 0; i < CachedHatchSegments.Count; i++)
            {
                ToolpathSegment s = CachedHatchSegments[i];
                var start = new ToolpathPoint(s.Start.X + deltaX, s.Start.Y + deltaY, s.Start.Z + deltaZ);
                var end = new ToolpathPoint(s.End.X + deltaX, s.End.Y + deltaY, s.End.Z + deltaZ);
                CachedHatchSegments[i] = new ToolpathSegment(start, end, s.Type, s.LayerId);
            }
        }

        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(PositionZ));
        OnShapeModified();
    }

    public abstract void Scale(float factor, float originX, float originY);

    public abstract void Rotate(float deltaAngleDegrees, float originX, float originY);

    public virtual void CopyTransformFrom(ToolpathShape source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _positionX = source._positionX;
        _positionY = source._positionY;
        _positionZ = source._positionZ;
        _layerId = source._layerId;
        _cutType = source._cutType;
        InvalidateHatchCache();
        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(PositionZ));
        OnPropertyChanged(nameof(LayerId));
        OnPropertyChanged(nameof(CutType));
        OnShapeModified();
    }

    public static (float X, float Y) RotatePoint(float x, float y, float originX, float originY, float deltaAngleDegrees)
    {
        if (MathF.Abs(deltaAngleDegrees) < 1e-6f)
        {
            return (x, y);
        }

        float rad = deltaAngleDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float dx = x - originX;
        float dy = y - originY;
        return (originX + dx * cos - dy * sin, originY + dx * sin + dy * cos);
    }

    public abstract ToolpathShape Clone();

    protected void OnShapeModified()
    {
        ShapeChanged?.Invoke(this);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
