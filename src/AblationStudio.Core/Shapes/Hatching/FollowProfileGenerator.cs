using AblationStudio.Core.Models;
using Clipper2Lib;

namespace AblationStudio.Core.Shapes.Hatching;

public static class FollowProfileGenerator
{
    private sealed record ProfileRing(IReadOnlyList<ToolpathPoint> Points, bool IsClosed);

    public static List<ToolpathSegment> Generate(
        ToolpathShape shape,
        HatchSettings settings,
        ref ToolpathPoint? currentPosition)
    {
        var segments = new List<ToolpathSegment>();
        float stepover = MathF.Max(HatchSettings.MinStepover, settings.Stepover);
        int layerId = shape.LayerId;
        float z = shape.PositionZ;

        List<ProfileRing> rings = shape switch
        {
            CircleShape circle => GetCircleRings(circle, stepover, z),
            RectangleShape rect => GetRectangleRings(rect, stepover, z),
            PolygonShape poly => GetPolygonRings(poly, stepover, z),
            IContourShape contourShape => GetContourShapeRings(contourShape, shape.PositionX, shape.PositionY, stepover, z),
            _ => GetGenericRings(shape, stepover, z)
        };

        if (rings.Count == 0)
        {
            return segments;
        }

        if (settings.FollowProfileOutward)
        {
            rings.Reverse();
        }

        List<int> sequence = ZigZagHatchGenerator.GetLineSequence(rings.Count, settings.LineSkip, settings.AutoLineSkip);

        foreach (int idx in sequence)
        {
            EmitRing(segments, rings[idx], layerId, ref currentPosition);
        }

        return segments;
    }

    private static List<ProfileRing> GetCircleRings(
        CircleShape circle,
        float stepover,
        float z)
    {
        var rings = new List<ProfileRing>();
        float outerR = circle.Radius;
        int count = circle.SegmentsCount;
        float stepTheta = (2.0f * MathF.PI) / count;
        float cx = circle.PositionX;
        float cy = circle.PositionY;

        for (float r = outerR - stepover; r > 0.0005f; r -= stepover)
        {
            var ringPoints = new ToolpathPoint[count];
            for (int i = 0; i < count; i++)
            {
                float theta = i * stepTheta;
                ringPoints[i] = new ToolpathPoint(cx + r * MathF.Cos(theta), cy + r * MathF.Sin(theta), z);
            }

            rings.Add(new ProfileRing(ringPoints, true));
        }

        return rings;
    }

    private static List<ProfileRing> GetRectangleRings(
        RectangleShape rect,
        float stepover,
        float z)
    {
        var rings = new List<ProfileRing>();
        float w = rect.Width;
        float h = rect.Height;
        float cx = rect.PositionX;
        float cy = rect.PositionY;
        float rad = rect.RotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        for (int k = 1; ; k++)
        {
            float wk = w - 2f * k * stepover;
            float hk = h - 2f * k * stepover;

            if (wk <= 0.001f || hk <= 0.001f)
            {
                break;
            }

            float hw = wk * 0.5f;
            float hh = hk * 0.5f;

            (float lx, float ly)[] localCorners =
            [
                (-hw, -hh),
                (hw, -hh),
                (hw, hh),
                (-hw, hh)
            ];

            var ringPoints = new ToolpathPoint[4];
            for (int i = 0; i < 4; i++)
            {
                float gx = cx + localCorners[i].lx * cos - localCorners[i].ly * sin;
                float gy = cy + localCorners[i].lx * sin + localCorners[i].ly * cos;
                ringPoints[i] = new ToolpathPoint(gx, gy, z);
            }

            rings.Add(new ProfileRing(ringPoints, true));
        }

        return rings;
    }

    private static List<ProfileRing> GetPolygonRings(
        PolygonShape poly,
        float stepover,
        float z)
    {
        var rings = new List<ProfileRing>();
        int sides = poly.Sides;
        float outerR = poly.Radius;
        float cx = poly.PositionX;
        float cy = poly.PositionY;
        float baseRad = poly.RotationDegrees * (MathF.PI / 180f);
        float stepTheta = (2.0f * MathF.PI) / sides;

        // Exact perpendicular edge offset shrinkage factor
        float cosHalfAngle = MathF.Cos(MathF.PI / sides);
        float deltaR = stepover / MathF.Max(0.01f, cosHalfAngle);

        for (float r = outerR - deltaR; r > 0.001f; r -= deltaR)
        {
            var ringPoints = new ToolpathPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                float theta = baseRad + i * stepTheta;
                ringPoints[i] = new ToolpathPoint(cx + r * MathF.Cos(theta), cy + r * MathF.Sin(theta), z);
            }

            rings.Add(new ProfileRing(ringPoints, true));
        }

        return rings;
    }

    private static List<ProfileRing> GetGenericRings(
        ToolpathShape shape,
        float stepover,
        float z)
    {
        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(shape);
        if (rawLoops.Count == 0)
        {
            return [];
        }

        var rawPaths = new PathsD(rawLoops.Count);
        foreach (List<HatchGeometry.Point2D> loop in rawLoops)
        {
            if (loop.Count < 3)
            {
                continue;
            }

            var path = new PathD(loop.Count);
            foreach (HatchGeometry.Point2D pt in loop)
            {
                path.Add(new PointD(pt.X, pt.Y));
            }
            rawPaths.Add(path);
        }

        return GetClipperRings(rawPaths, stepover, z);
    }

    private static List<ProfileRing> GetContourShapeRings(
        IContourShape contourShape,
        float px,
        float py,
        float stepover,
        float z)
    {
        var rawPaths = new PathsD();

        foreach (PathContour contour in contourShape.Contours)
        {
            if (!contour.IsClosed || contour.PointsCount < 3)
            {
                continue;
            }

            var path = new PathD(contour.PointsCount);
            foreach (ToolpathPoint pt in contour.LocalPoints)
            {
                path.Add(new PointD(px + pt.X, py + pt.Y));
            }
            rawPaths.Add(path);
        }

        return GetClipperRings(rawPaths, stepover, z);
    }

    private const int ClipperPrecision = 6;

    private static List<ProfileRing> GetClipperRings(
        PathsD rawPaths,
        float stepover,
        float z)
    {
        var allRings = new List<ProfileRing>();
        if (rawPaths.Count == 0)
        {
            return allRings;
        }

        // Normalize loops and hierarchy with EvenOdd fill rule at sub-micron precision
        PathsD basePaths = Clipper.Union(rawPaths, new PathsD(), FillRule.EvenOdd, ClipperPrecision);
        if (basePaths.Count == 0)
        {
            return allRings;
        }

        const int maxIterations = 100_000;
        for (int step = 1; step <= maxIterations; step++)
        {
            double delta = -step * (double)stepover;
            PathsD solution = Clipper.InflatePaths(basePaths, delta, JoinType.Miter, EndType.Polygon, 2.0, ClipperPrecision);
            if (solution.Count == 0)
            {
                break;
            }

            foreach (PathD ring in solution)
            {
                int n = ring.Count;
                if (n < 3)
                {
                    continue;
                }

                var ringPts = new ToolpathPoint[n];
                for (int i = 0; i < n; i++)
                {
                    ringPts[i] = new ToolpathPoint((float)ring[i].x, (float)ring[i].y, z);
                }

                allRings.Add(new ProfileRing(ringPts, true));
            }
        }

        return allRings;
    }

    private static void EmitRing(
        List<ToolpathSegment> segments,
        ProfileRing ring,
        int layerId,
        ref ToolpathPoint? currentPosition)
    {
        IReadOnlyList<ToolpathPoint> points = ring.Points;
        if (points.Count < 2)
        {
            return;
        }

        // Rapid to the start of this loop
        if (currentPosition is null || currentPosition.Value.DistanceTo(points[0]) > 0.0001f)
        {
            if (currentPosition is not null)
            {
                segments.Add(new ToolpathSegment(currentPosition.Value, points[0], SegmentType.Rapid, layerId));
            }
        }

        // Trace the loop with Hatch segments
        for (int i = 0; i < points.Count - 1; i++)
        {
            segments.Add(new ToolpathSegment(points[i], points[i + 1], SegmentType.Hatch, layerId));
        }

        // Close the loop if requested
        if (ring.IsClosed && points[^1].DistanceTo(points[0]) > 0.0001f)
        {
            segments.Add(new ToolpathSegment(points[^1], points[0], SegmentType.Hatch, layerId));
            currentPosition = points[0];
        }
        else
        {
            currentPosition = points[^1];
        }
    }
}
