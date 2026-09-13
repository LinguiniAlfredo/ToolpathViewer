using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public sealed class PolygonShape : ToolpathShape
{
    private float _radius = 6.0f;
    private int _sides = 5; // Default pentagon
    private float _rotationDegrees;

    public override string ShapeType => "Polygon";
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
                OnShapeModified();
            }
        }
    }

    public int Sides
    {
        get => _sides;
        set
        {
            int val = Math.Clamp(value, 3, 100);
            if (SetProperty(ref _sides, val))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float RotationDegrees
    {
        get => _rotationDegrees;
        set
        {
            float val = (value % 360f + 360f) % 360f;
            if (SetProperty(ref _rotationDegrees, val))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public PolygonShape()
    {
        Name = "Polygon";
    }

    public PolygonShape(float centerX, float centerY, float centerZ, float radius, int sides = 5, float rotation = 0f)
    {
        Name = "Polygon";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        _radius = MathF.Max(0.01f, radius);
        _sides = Math.Clamp(sides, 3, 100);
        _rotationDegrees = (rotation % 360f + 360f) % 360f;
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints()
    {
        int count = _sides;
        var points = new ToolpathPoint[count];
        float baseRad = _rotationDegrees * (MathF.PI / 180f);
        float step = (2.0f * MathF.PI) / count;

        for (int i = 0; i < count; i++)
        {
            float theta = baseRad + i * step;
            float x = PositionX + _radius * MathF.Cos(theta);
            float y = PositionY + _radius * MathF.Sin(theta);
            points[i] = new ToolpathPoint(x, y, PositionZ);
        }

        return points;
    }

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
        RotationDegrees = (RotationDegrees + deltaAngleDegrees % 360f + 360f) % 360f;
    }

    public override void CopyTransformFrom(ToolpathShape source)
    {
        base.CopyTransformFrom(source);
        if (source is PolygonShape poly)
        {
            _radius = poly._radius;
            _sides = poly._sides;
            _rotationDegrees = poly._rotationDegrees;
            OnPropertyChanged(nameof(Radius));
            OnPropertyChanged(nameof(Sides));
            OnPropertyChanged(nameof(RotationDegrees));
            InvalidateHatchCache();
            OnShapeModified();
        }
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        float dist = MathF.Sqrt(MathF.Pow(worldX - PositionX, 2) + MathF.Pow(worldY - PositionY, 2));
        return dist <= (_radius + tolerance);
    }

    public override ToolpathShape Clone()
    {
        var copy = new PolygonShape(PositionX, PositionY, PositionZ, Radius, Sides, RotationDegrees)
        {
            Name = $"{Name} Copy",
            LayerId = LayerId,
            CutType = CutType
        };
        copy.Hatch.CopyFrom(Hatch);
        return copy;
    }
}
