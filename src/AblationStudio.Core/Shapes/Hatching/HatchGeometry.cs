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

    public sealed class LoopData
    {
        public IReadOnlyList<Point2D> Points { get; }
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float SignedArea { get; }

        public LoopData(IReadOnlyList<Point2D> points)
        {
            Points = points;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            float area = 0f;
            int count = points.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Point2D p = points[i];
                Point2D prev = points[j];
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
                area += (prev.X * p.Y - p.X * prev.Y);
            }

            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            SignedArea = area * 0.5f;
        }
    }

    public static List<LoopData> BuildLoopDataList(IReadOnlyList<IReadOnlyList<Point2D>> loops)
    {
        var list = new List<LoopData>(loops.Count);
        for (int i = 0; i < loops.Count; i++)
        {
            if (loops[i].Count >= 3)
            {
                list.Add(new LoopData(loops[i]));
            }
        }
        return list;
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

    public static List<List<Point2D>> GetPolygonLoops(ToolpathShape shape)
    {
        if (shape is PathShape pathShape)
        {
            var loops = new List<List<Point2D>>(pathShape.Contours.Count);
            float px = pathShape.PositionX;
            float py = pathShape.PositionY;

            foreach (PathContour contour in pathShape.Contours)
            {
                if (!contour.IsClosed || contour.PointsCount < 3)
                {
                    continue;
                }

                var loop = new List<Point2D>(contour.PointsCount);
                foreach (ToolpathPoint p in contour.LocalPoints)
                {
                    loop.Add(new Point2D(px + p.X, py + p.Y));
                }
                loops.Add(loop);
            }

            return loops;
        }

        return [GetPolygon2D(shape)];
    }

    public static bool IsPointInLoops(Point2D pt, IReadOnlyList<IReadOnlyList<Point2D>> loops)
    {
        bool inside = false;

        foreach (IReadOnlyList<Point2D> loop in loops)
        {
            int count = loop.Count;
            if (count < 3)
            {
                continue;
            }

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Point2D pi = loop[i];
                Point2D pj = loop[j];

                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-12f) + pi.X))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    public static bool IsPointInLoops(Point2D pt, IReadOnlyList<LoopData> loops)
    {
        bool inside = false;

        for (int k = 0; k < loops.Count; k++)
        {
            LoopData loop = loops[k];
            // Fast bounding box rejection: if pt.Y is outside loop Y-extents or pt.X is beyond MaxX, skip loop
            if (pt.Y < loop.MinY || pt.Y > loop.MaxY || pt.X > loop.MaxX)
            {
                continue;
            }

            IReadOnlyList<Point2D> pts = loop.Points;
            int count = pts.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Point2D pi = pts[i];
                Point2D pj = pts[j];

                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-12f) + pi.X))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    public readonly record struct SegmentInterval(Point2D Start, Point2D End);

    public static void ClipSegmentToLoops(
        Point2D p0,
        Point2D p1,
        IReadOnlyList<LoopData> loops,
        List<SegmentInterval> outputSegments,
        List<float>? scratchT = null)
    {
        float dxA = p1.X - p0.X;
        float dyA = p1.Y - p0.Y;
        float segLenSq = dxA * dxA + dyA * dyA;

        if (segLenSq < 1e-12f)
        {
            return;
        }

        float segMinX = MathF.Min(p0.X, p1.X);
        float segMaxX = MathF.Max(p0.X, p1.X);
        float segMinY = MathF.Min(p0.Y, p1.Y);
        float segMaxY = MathF.Max(p0.Y, p1.Y);

        List<float> tList = scratchT ?? [];
        tList.Clear();

        // 1. Gather all boundary edge intersections along segment p0 -> p1
        for (int l = 0; l < loops.Count; l++)
        {
            LoopData loop = loops[l];

            // Bounding box overlap rejection
            if (segMaxX < loop.MinX || segMinX > loop.MaxX ||
                segMaxY < loop.MinY || segMinY > loop.MaxY)
            {
                continue;
            }

            IReadOnlyList<Point2D> pts = loop.Points;
            int count = pts.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Point2D b1 = pts[j];
                Point2D b2 = pts[i];

                float edgeMinX = MathF.Min(b1.X, b2.X);
                float edgeMaxX = MathF.Max(b1.X, b2.X);
                float edgeMinY = MathF.Min(b1.Y, b2.Y);
                float edgeMaxY = MathF.Max(b1.Y, b2.Y);

                if (segMaxX < edgeMinX || segMinX > edgeMaxX ||
                    segMaxY < edgeMinY || segMinY > edgeMaxY)
                {
                    continue;
                }

                float dxB = b2.X - b1.X;
                float dyB = b2.Y - b1.Y;

                float denom = dxA * dyB - dyA * dxB;
                if (MathF.Abs(denom) < 1e-9f)
                {
                    continue;
                }

                float dxBA = b1.X - p0.X;
                float dyBA = b1.Y - p0.Y;

                float t = (dxBA * dyB - dyBA * dxB) / denom;
                float u = (dxBA * dyA - dyBA * dxA) / denom;

                if (t > 1e-5f && t < 0.99999f && u >= -1e-5f && u <= 1.00001f)
                {
                    tList.Add(Math.Clamp(t, 0f, 1f));
                }
            }
        }

        // 2. Case: No intersections along chord
        if (tList.Count == 0)
        {
            var mid = new Point2D((p0.X + p1.X) * 0.5f, (p0.Y + p1.Y) * 0.5f);
            if (IsPointInLoops(mid, loops))
            {
                outputSegments.Add(new SegmentInterval(p0, p1));
            }
            return;
        }

        // 3. Sort intersections
        tList.Sort();

        // 4. Build sub-intervals [0, t1], [t1, t2], ..., [tm, 1]
        float prevT = 0f;
        for (int i = 0; i <= tList.Count; i++)
        {
            float currT = (i < tList.Count) ? tList[i] : 1.0f;

            if (currT - prevT > 1e-5f)
            {
                float midT = (prevT + currT) * 0.5f;
                var midPoint = new Point2D(p0.X + midT * dxA, p0.Y + midT * dyA);

                if (IsPointInLoops(midPoint, loops))
                {
                    var startPt = new Point2D(p0.X + prevT * dxA, p0.Y + prevT * dyA);
                    var endPt = new Point2D(p0.X + currT * dxA, p0.Y + currT * dyA);
                    outputSegments.Add(new SegmentInterval(startPt, endPt));
                }
            }

            prevT = currT;
        }
    }
}
