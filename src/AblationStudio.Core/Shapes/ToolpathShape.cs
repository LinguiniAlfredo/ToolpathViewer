using System.ComponentModel;
using System.Runtime.CompilerServices;
using AblationStudio.Core.Models;

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

    public string Id { get; } = Guid.NewGuid().ToString("N");

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

        // Rapid transition from previous tool position to start of this shape
        if (currentPosition is not null && currentPosition.Value.DistanceTo(points[0]) > 0.001f)
        {
            yield return new ToolpathSegment(currentPosition.Value, points[0], SegmentType.Rapid, LayerId);
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
        PositionX += deltaX;
        PositionY += deltaY;
        PositionZ += deltaZ;
    }

    public abstract void Scale(float factor, float originX, float originY);

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
