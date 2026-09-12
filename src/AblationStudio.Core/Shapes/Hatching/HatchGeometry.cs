using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes.Hatching;

public static class HatchGeometry
{
    public readonly record struct Point2D(float X, float Y)
    {
        public float DistanceTo(Point2D other) =>
            MathF.Sqrt(MathF.Pow(X - other.X, 2) + MathF.Pow(Y - other.Y, 2));

        public Point2D Rotate(float cos, float sin) =>
            new(X * cos - Y * sin, X * sin + Y * cos);
    }

    public static bool IsPointInPolygon(Point2D pt, IReadOnlyList<Point2D> polygon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        int count = polygon.Count;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Point2D pi = polygon[i];
            Point2D pj = polygon[j];

            if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-12f) + pi.X))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool TryIntersectSegments(
        Point2D a1, Point2D a2,
        Point2D b1, Point2D b2,
        out Point2D intersection)
    {
        intersection = default;

        float dxA = a2.X - a1.X;
        float dyA = a2.Y - a1.Y;
        float dxB = b2.X - b1.X;
        float dyB = b2.Y - b1.Y;

        float denom = dxA * dyB - dyA * dxB;
        if (MathF.Abs(denom) < 1e-9f)
        {
            return false;
        }

        float dxBA = b1.X - a1.X;
        float dyBA = b1.Y - a1.Y;

        float t = (dxBA * dyB - dyBA * dxB) / denom;
        float u = (dxBA * dyA - dyBA * dxA) / denom;

        if (t >= -1e-5f && t <= 1.00001f && u >= -1e-5f && u <= 1.00001f)
        {
            float tClamped = Math.Clamp(t, 0f, 1f);
            intersection = new Point2D(a1.X + tClamped * dxA, a1.Y + tClamped * dyA);
            return true;
        }

        return false;
    }

    public static List<Point2D> GetPolygon2D(ToolpathShape shape)
    {
        IReadOnlyList<ToolpathPoint> pts = shape.GetPathPoints();
        var poly = new List<Point2D>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            poly.Add(new Point2D(pts[i].X, pts[i].Y));
        }
        return poly;
    }
}
