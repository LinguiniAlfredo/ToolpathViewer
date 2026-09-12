using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes.Hatching;

public static class FollowProfileGenerator
{
    public static List<ToolpathSegment> Generate(
        ToolpathShape shape,
        HatchSettings settings,
        ref ToolpathPoint? currentPosition)
    {
        var segments = new List<ToolpathSegment>();
        float stepover = MathF.Max(HatchSettings.MinStepover, settings.Stepover);
        int layerId = shape.LayerId;
        float z = shape.PositionZ;

        var rings = shape switch
        {
            CircleShape circle => GetCircleRings(circle, stepover, z),
            RectangleShape rect => GetRectangleRings(rect, stepover, z),
            PolygonShape poly => GetPolygonRings(poly, stepover, z),
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
            EmitClosedLoop(segments, rings[idx], layerId, ref currentPosition);
        }

        return segments;
    }

    private static List<IReadOnlyList<ToolpathPoint>> GetCircleRings(
        CircleShape circle,
        float stepover,
        float z)
    {
        var rings = new List<IReadOnlyList<ToolpathPoint>>();
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

            rings.Add(ringPoints);
        }

        return rings;
    }

    private static List<IReadOnlyList<ToolpathPoint>> GetRectangleRings(
        RectangleShape rect,
        float stepover,
        float z)
    {
        var rings = new List<IReadOnlyList<ToolpathPoint>>();
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

            rings.Add(ringPoints);
        }

        return rings;
    }

    private static List<IReadOnlyList<ToolpathPoint>> GetPolygonRings(
        PolygonShape poly,
        float stepover,
        float z)
    {
        var rings = new List<IReadOnlyList<ToolpathPoint>>();
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

            rings.Add(ringPoints);
        }

        return rings;
    }

    private static List<IReadOnlyList<ToolpathPoint>> GetGenericRings(
        ToolpathShape shape,
        float stepover,
        float z)
    {
        var rings = new List<IReadOnlyList<ToolpathPoint>>();
        IReadOnlyList<ToolpathPoint> pts = shape.GetPathPoints();
        if (pts.Count < 3)
        {
            return rings;
        }

        float cx = shape.PositionX;
        float cy = shape.PositionY;

        float maxR = 0.001f;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = MathF.Sqrt(MathF.Pow(pts[i].X - cx, 2) + MathF.Pow(pts[i].Y - cy, 2));
            maxR = MathF.Max(maxR, d);
        }

        for (float offset = stepover; offset < maxR - 0.001f; offset += stepover)
        {
            float scale = 1.0f - (offset / maxR);
            if (scale <= 0.001f)
            {
                break;
            }

            var ringPoints = new ToolpathPoint[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                float x = cx + (pts[i].X - cx) * scale;
                float y = cy + (pts[i].Y - cy) * scale;
                ringPoints[i] = new ToolpathPoint(x, y, z);
            }

            rings.Add(ringPoints);
        }

        return rings;
    }

    private static void EmitClosedLoop(
        List<ToolpathSegment> segments,
        IReadOnlyList<ToolpathPoint> points,
        int layerId,
        ref ToolpathPoint? currentPosition)
    {
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

        // Close the loop
        if (points[^1].DistanceTo(points[0]) > 0.0001f)
        {
            segments.Add(new ToolpathSegment(points[^1], points[0], SegmentType.Hatch, layerId));
        }

        currentPosition = points[0];
    }
}
