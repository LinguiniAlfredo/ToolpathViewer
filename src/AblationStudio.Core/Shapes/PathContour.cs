using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public sealed class PathContour
{
    private readonly List<ToolpathPoint> _localPoints = [];
    private bool _isClosed;

    public IReadOnlyList<ToolpathPoint> LocalPoints => _localPoints;

    public bool IsClosed
    {
        get => _isClosed;
        set => _isClosed = value;
    }

    public int PointsCount => _localPoints.Count;

    public float Length
    {
        get
        {
            if (_localPoints.Count < 2)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < _localPoints.Count - 1; i++)
            {
                total += _localPoints[i].DistanceTo(_localPoints[i + 1]);
            }

            if (_isClosed && _localPoints.Count > 2 && _localPoints[^1].DistanceTo(_localPoints[0]) > 0.001f)
            {
                total += _localPoints[^1].DistanceTo(_localPoints[0]);
            }

            return total;
        }
    }

    public PathContour(IEnumerable<ToolpathPoint> localPoints, bool isClosed)
    {
        ArgumentNullException.ThrowIfNull(localPoints);
        _localPoints.AddRange(localPoints);
        _isClosed = isClosed;
    }

    public void AddPoint(in ToolpathPoint point) => _localPoints.Add(point);

    public void Scale(float factor)
    {
        for (int i = 0; i < _localPoints.Count; i++)
        {
            ToolpathPoint p = _localPoints[i];
            _localPoints[i] = new ToolpathPoint(p.X * factor, p.Y * factor, p.Z * factor);
        }
    }

    public void Rotate(float angleDegrees)
    {
        if (MathF.Abs(angleDegrees) < 1e-6f)
        {
            return;
        }

        float rad = angleDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        for (int i = 0; i < _localPoints.Count; i++)
        {
            ToolpathPoint p = _localPoints[i];
            float rx = p.X * cos - p.Y * sin;
            float ry = p.X * sin + p.Y * cos;
            _localPoints[i] = new ToolpathPoint(rx, ry, p.Z);
        }
    }

    public BoundingBox3D GetLocalBounds()
    {
        if (_localPoints.Count == 0)
        {
            return new BoundingBox3D(0f, 0f, 0f, 0f, 0f, 0f);
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (ToolpathPoint p in _localPoints)
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

    public PathContour Clone() => new(_localPoints, _isClosed);
}
