using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public sealed class RectangleShape : ToolpathShape
{
    private float _width = 10.0f;
    private float _height = 8.0f;
    private float _rotationDegrees;

    public override string ShapeType => "Rectangle";
    public override bool IsClosed => true;

    public float Width
    {
        get => _width;
        set
        {
            float val = MathF.Max(0.01f, value);
            if (SetProperty(ref _width, val))
            {
                OnShapeModified();
            }
        }
    }

    public float Height
    {
        get => _height;
        set
        {
            float val = MathF.Max(0.01f, value);
            if (SetProperty(ref _height, val))
            {
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
                OnShapeModified();
            }
        }
    }

    public RectangleShape()
    {
        Name = "Rectangle";
    }

    public RectangleShape(float centerX, float centerY, float centerZ, float width, float height, float rotation = 0f)
    {
        Name = "Rectangle";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        _width = MathF.Max(0.01f, width);
        _height = MathF.Max(0.01f, height);
        _rotationDegrees = (rotation % 360f + 360f) % 360f;
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints()
    {
        float hw = _width * 0.5f;
        float hh = _height * 0.5f;
        float rad = _rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        (float lx, float ly)[] localCorners =
        [
            (-hw, -hh),
            (hw, -hh),
            (hw, hh),
            (-hw, hh)
        ];

        var points = new ToolpathPoint[4];
        for (int i = 0; i < 4; i++)
        {
            float gx = PositionX + localCorners[i].lx * cos - localCorners[i].ly * sin;
            float gy = PositionY + localCorners[i].lx * sin + localCorners[i].ly * cos;
            points[i] = new ToolpathPoint(gx, gy, PositionZ);
        }

        return points;
    }

    public override void Scale(float factor, float originX, float originY)
    {
        PositionX = originX + (PositionX - originX) * factor;
        PositionY = originY + (PositionY - originY) * factor;
        Width = MathF.Max(0.01f, Width * factor);
        Height = MathF.Max(0.01f, Height * factor);
        OnShapeModified();
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        float dx = worldX - PositionX;
        float dy = worldY - PositionY;

        float rad = -_rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        float lx = MathF.Abs(dx * cos - dy * sin);
        float ly = MathF.Abs(dx * sin + dy * cos);

        return lx <= (_width * 0.5f + tolerance) && ly <= (_height * 0.5f + tolerance);
    }

    public override ToolpathShape Clone() => new RectangleShape(PositionX, PositionY, PositionZ, Width, Height, RotationDegrees)
    {
        Name = $"{Name} Copy",
        LayerId = LayerId,
        CutType = CutType
    };
}
