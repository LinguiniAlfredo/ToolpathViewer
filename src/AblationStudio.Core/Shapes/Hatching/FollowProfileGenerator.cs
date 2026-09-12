using AblationStudio.Core.Models;

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
            PathShape pathShape => GetPathShapeRings(pathShape, stepover, z),
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
        var allRings = new List<ProfileRing>();
        IReadOnlyList<ToolpathPoint> pts = shape.GetPathPoints();
        if (pts.Count < 3)
        {
            return allRings;
        }

        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(shape);
        List<HatchGeometry.LoopData> allLoops = HatchGeometry.BuildLoopDataList(rawLoops);
        if (allLoops.Count == 0)
        {
            return allRings;
        }

        var worldPts = new HatchGeometry.Point2D[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            worldPts[i] = new HatchGeometry.Point2D(pts[i].X, pts[i].Y);
        }

        GenerateContourOffsetRings(worldPts, stepover, z, allLoops, allRings);
        return allRings;
    }

    private static List<ProfileRing> GetPathShapeRings(
        PathShape pathShape,
        float stepover,
        float z)
    {
        var allRings = new List<ProfileRing>();
        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(pathShape);
        List<HatchGeometry.LoopData> allLoops = HatchGeometry.BuildLoopDataList(rawLoops);
        if (allLoops.Count == 0)
        {
            return allRings;
        }

        float px = pathShape.PositionX;
        float py = pathShape.PositionY;

        foreach (PathContour contour in pathShape.Contours)
        {
            if (!contour.IsClosed || contour.PointsCount < 3)
            {
                continue;
            }

            IReadOnlyList<ToolpathPoint> localPts = contour.LocalPoints;
            var worldPts = new HatchGeometry.Point2D[localPts.Count];
            for (int i = 0; i < localPts.Count; i++)
            {
                worldPts[i] = new HatchGeometry.Point2D(px + localPts[i].X, py + localPts[i].Y);
            }

            GenerateContourOffsetRings(worldPts, stepover, z, allLoops, allRings);
        }

        return allRings;
    }

    private static void GenerateContourOffsetRings(
        IReadOnlyList<HatchGeometry.Point2D> inputPts,
        float stepover,
        float z,
        IReadOnlyList<HatchGeometry.LoopData> allLoops,
        List<ProfileRing> outputRings)
    {
        // 1. Clean input vertices: remove adjacent duplicates and closing duplicate
        var pts = new List<HatchGeometry.Point2D>(inputPts.Count);
        for (int i = 0; i < inputPts.Count; i++)
        {
            if (pts.Count == 0 || pts[^1].DistanceTo(inputPts[i]) > 1e-5f)
            {
                pts.Add(inputPts[i]);
            }
        }
        if (pts.Count > 2 && pts[^1].DistanceTo(pts[0]) < 1e-5f)
        {
            pts.RemoveAt(pts.Count - 1);
        }

        int n = pts.Count;
        if (n < 3)
        {
            return;
        }

        // 2. Compute signed area
        float origArea = 0f;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            origArea += (pts[j].X * pts[i].Y - pts[i].X * pts[j].Y);
        }
        origArea *= 0.5f;
        if (MathF.Abs(origArea) < 1e-6f)
        {
            return;
        }

        bool isCcw = origArea > 0f;

        // 3. Compute edge unit tangents and inward normals
        var tangents = new HatchGeometry.Point2D[n];
        var normals = new HatchGeometry.Point2D[n];

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float dx = pts[next].X - pts[i].X;
            float dy = pts[next].Y - pts[i].Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6f)
            {
                tangents[i] = new HatchGeometry.Point2D(1f, 0f);
                normals[i] = isCcw ? new HatchGeometry.Point2D(0f, 1f) : new HatchGeometry.Point2D(0f, -1f);
            }
            else
            {
                float tx = dx / len;
                float ty = dy / len;
                tangents[i] = new HatchGeometry.Point2D(tx, ty);
                normals[i] = isCcw ? new HatchGeometry.Point2D(-ty, tx) : new HatchGeometry.Point2D(ty, -tx);
            }
        }

        // 4. Precompute miter displacement directions at each vertex
        var miterVectors = new HatchGeometry.Point2D[n];
        for (int i = 0; i < n; i++)
        {
            int prev = (i + n - 1) % n;
            HatchGeometry.Point2D nPrev = normals[prev];
            HatchGeometry.Point2D nCurr = normals[i];

            float dot = nPrev.X * nCurr.X + nPrev.Y * nCurr.Y;
            float denom = 1f + dot;

            if (denom < 1e-4f)
            {
                miterVectors[i] = nCurr;
            }
            else
            {
                float mx = (nPrev.X + nCurr.X) / denom;
                float my = (nPrev.Y + nCurr.Y) / denom;
                float lenSq = mx * mx + my * my;
                const float maxMiter = 2.5f;
                if (lenSq > maxMiter * maxMiter)
                {
                    float s = maxMiter / MathF.Sqrt(lenSq);
                    mx *= s;
                    my *= s;
                }
                miterVectors[i] = new HatchGeometry.Point2D(mx, my);
            }
        }

        // 5. Inward offset iterations
        var scratchIntervals = new List<HatchGeometry.SegmentInterval>();
        var scratchT = new List<float>(8);
        var offsetPts = new HatchGeometry.Point2D[n];

        for (int step = 1; ; step++)
        {
            float d = step * stepover;

            for (int i = 0; i < n; i++)
            {
                offsetPts[i] = new HatchGeometry.Point2D(
                    pts[i].X + d * miterVectors[i].X,
                    pts[i].Y + d * miterVectors[i].Y);
            }

            // Area check: stop if collapsed or inverted
            float offsetArea = 0f;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                offsetArea += (offsetPts[j].X * offsetPts[i].Y - offsetPts[i].X * offsetPts[j].Y);
            }
            offsetArea *= 0.5f;

            if ((offsetArea > 0f) != isCcw || MathF.Abs(offsetArea) < 1e-6f || MathF.Abs(offsetArea) > MathF.Abs(origArea))
            {
                break;
            }

            // Check if entire ring is inside
            bool allInside = true;
            for (int i = 0; i < n; i++)
            {
                if (!HatchGeometry.IsPointInLoops(offsetPts[i], allLoops))
                {
                    allInside = false;
                    break;
                }
                int next = (i + 1) % n;
                var mid = new HatchGeometry.Point2D(
                    (offsetPts[i].X + offsetPts[next].X) * 0.5f,
                    (offsetPts[i].Y + offsetPts[next].Y) * 0.5f);
                if (!HatchGeometry.IsPointInLoops(mid, allLoops))
                {
                    allInside = false;
                    break;
                }
            }

            if (allInside)
            {
                var ringPts = new ToolpathPoint[n];
                for (int i = 0; i < n; i++)
                {
                    ringPts[i] = new ToolpathPoint(offsetPts[i].X, offsetPts[i].Y, z);
                }
                outputRings.Add(new ProfileRing(ringPts, true));
            }
            else
            {
                // Partial ring: clip each segment and add valid inside strokes
                bool hasAnyStroke = false;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    scratchIntervals.Clear();
                    HatchGeometry.ClipSegmentToLoops(offsetPts[i], offsetPts[next], allLoops, scratchIntervals, scratchT);

                    for (int s = 0; s < scratchIntervals.Count; s++)
                    {
                        var seg = scratchIntervals[s];
                        outputRings.Add(new ProfileRing(
                            [
                                new ToolpathPoint(seg.Start.X, seg.Start.Y, z),
                                new ToolpathPoint(seg.End.X, seg.End.Y, z)
                            ],
                            false));
                        hasAnyStroke = true;
                    }
                }

                if (!hasAnyStroke)
                {
                    break;
                }
            }
        }
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
