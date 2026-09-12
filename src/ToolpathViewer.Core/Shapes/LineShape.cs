using ToolpathViewer.Core.Models;

namespace ToolpathViewer.Core.Shapes;

public sealed class LineShape : ToolpathShape
{
    private float _endX;
    private float _endY;
    private float _endZ;

    public override string ShapeType => "Line";
    public override bool IsClosed => false;

    public float EndX
    {
        get => _endX;
        set
        {
            if (SetProperty(ref _endX, value))
            {
                OnPropertyChanged(nameof(Length));
                OnPropertyChanged(nameof(AngleDegrees));
                OnShapeModified();
            }
        }
    }

    public float EndY
    {
        get => _endY;
        set
        {
            if (SetProperty(ref _endY, value))
            {
                OnPropertyChanged(nameof(Length));
                OnPropertyChanged(nameof(AngleDegrees));
                OnShapeModified();
            }
        }
    }

    public float EndZ
    {
        get => _endZ;
        set
        {
            if (SetProperty(ref _endZ, value))
            {
                OnPropertyChanged(nameof(Length));
                OnShapeModified();
            }
        }
    }

    public float Length => MathF.Sqrt(
        MathF.Pow(EndX - PositionX, 2) +
        MathF.Pow(EndY - PositionY, 2) +
        MathF.Pow(EndZ - PositionZ, 2));

    public float AngleDegrees
    {
        get
        {
            float angle = MathF.Atan2(EndY - PositionY, EndX - PositionX) * (180f / MathF.PI);
            return angle < 0 ? angle + 360f : angle;
        }
    }

    public LineShape()
    {
        Name = "Line";
    }

    public LineShape(float startX, float startY, float startZ, float endX, float endY, float endZ)
    {
        Name = "Line";
        PositionX = startX;
        PositionY = startY;
        PositionZ = startZ;
        _endX = endX;
        _endY = endY;
        _endZ = endZ;
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints() =>
    [
        new(PositionX, PositionY, PositionZ),
        new(EndX, EndY, EndZ)
    ];

    public override void Translate(float deltaX, float deltaY, float deltaZ)
    {
        PositionX += deltaX;
        PositionY += deltaY;
        PositionZ += deltaZ;
        _endX += deltaX;
        _endY += deltaY;
        _endZ += deltaZ;
        OnPropertyChanged(nameof(EndX));
        OnPropertyChanged(nameof(EndY));
        OnPropertyChanged(nameof(EndZ));
        OnShapeModified();
    }

    public override void Scale(float factor, float originX, float originY)
    {
        PositionX = originX + (PositionX - originX) * factor;
        PositionY = originY + (PositionY - originY) * factor;
        EndX = originX + (EndX - originX) * factor;
        EndY = originY + (EndY - originY) * factor;
        OnShapeModified();
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        float ax = PositionX, ay = PositionY;
        float bx = EndX, by = EndY;

        float dx = bx - ax;
        float dy = by - ay;
        float lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-6f)
        {
            float dist = MathF.Sqrt(MathF.Pow(worldX - ax, 2) + MathF.Pow(worldY - ay, 2));
            return dist <= tolerance;
        }

        float t = Math.Clamp(((worldX - ax) * dx + (worldY - ay) * dy) / lenSq, 0f, 1f);
        float projX = ax + t * dx;
        float projY = ay + t * dy;

        float distSq = MathF.Pow(worldX - projX, 2) + MathF.Pow(worldY - projY, 2);
        return distSq <= tolerance * tolerance;
    }

    public override ToolpathShape Clone() => new LineShape(PositionX, PositionY, PositionZ, EndX, EndY, EndZ)
    {
        Name = $"{Name} Copy",
        LayerId = LayerId,
        CutType = CutType
    };
}
