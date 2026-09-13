using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public sealed class CircleShape : ToolpathShape
{
    private float _radius = 5.0f;
    private int _segmentsCount = 64;

    public override string ShapeType => "Circle";
    public override bool IsClosed => true;

    public float Radius
    {
        get => _radius;
        set
        {
            float val = MathF.Max(0.01f, value);
            if (SetProperty(ref _radius, val))
            {
                InvalidateHatchCache();
                OnPropertyChanged(nameof(Diameter));
                OnShapeModified();
            }
        }
    }

    public float Diameter
    {
        get => _radius * 2.0f;
        set => Radius = value * 0.5f;
    }

    public int SegmentsCount
    {
        get => _segmentsCount;
        set
        {
            int val = Math.Clamp(value, 8, 360);
            if (SetProperty(ref _segmentsCount, val))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public CircleShape()
    {
        Name = "Circle";
    }

    public CircleShape(float centerX, float centerY, float centerZ, float radius, int segments = 64)
    {
        Name = "Circle";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        _radius = MathF.Max(0.01f, radius);
        _segmentsCount = Math.Clamp(segments, 8, 360);
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints()
    {
        int count = SegmentsCount;
        var points = new ToolpathPoint[count];
        float step = (2.0f * MathF.PI) / count;

        for (int i = 0; i < count; i++)
        {
            float theta = i * step;
            float x = PositionX + _radius * MathF.Cos(theta);
            float y = PositionY + _radius * MathF.Sin(theta);
            points[i] = new ToolpathPoint(x, y, PositionZ);
        }

        return points;
    }

    public override BoundingBox3D GetBounds() => new(
        PositionX - _radius, PositionY - _radius, PositionZ,
        PositionX + _radius, PositionY + _radius, PositionZ);

    public override void Scale(float factor, float originX, float originY)
    {
        PositionX = originX + (PositionX - originX) * factor;
        PositionY = originY + (PositionY - originY) * factor;
        Radius = MathF.Max(0.01f, Radius * factor);
        InvalidateHatchCache();
        OnShapeModified();
    }

    public override void Rotate(float deltaAngleDegrees, float originX, float originY)
    {
        var (newX, newY) = RotatePoint(PositionX, PositionY, originX, originY, deltaAngleDegrees);
        PositionX = newX;
        PositionY = newY;
    }

    public override void CopyTransformFrom(ToolpathShape source)
    {
        base.CopyTransformFrom(source);
        if (source is CircleShape circle)
        {
            _radius = circle._radius;
            _segmentsCount = circle._segmentsCount;
            OnPropertyChanged(nameof(Radius));
            OnPropertyChanged(nameof(Diameter));
            OnPropertyChanged(nameof(SegmentsCount));
            InvalidateHatchCache();
            OnShapeModified();
        }
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        float dist = MathF.Sqrt(MathF.Pow(worldX - PositionX, 2) + MathF.Pow(worldY - PositionY, 2));
        // Hit if inside the circle or near its boundary
        return dist <= _radius + tolerance;
    }

    public override ToolpathShape Clone()
    {
        var copy = new CircleShape(PositionX, PositionY, PositionZ, Radius, SegmentsCount)
        {
            Name = $"{Name} Copy",
            LayerId = LayerId,
            CutType = CutType
        };
        copy.Hatch.CopyFrom(Hatch);
        return copy;
    }
}
