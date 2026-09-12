using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes.Hatching;

public static class SpiralHatchGenerator
{
    private const int SamplesPerRevolution = 64;

    public static List<ToolpathSegment> Generate(
        ToolpathShape shape,
        HatchSettings settings,
        ref ToolpathPoint? currentPosition)
    {
        var segments = new List<ToolpathSegment>();
        float stepover = MathF.Max(HatchSettings.MinStepover, settings.Stepover);
        float z = shape.PositionZ;
        int layerId = shape.LayerId;
        float cx = shape.PositionX;
        float cy = shape.PositionY;
        float startAngleRad = settings.AngleDegrees * (MathF.PI / 180f);

        // Specialized fast path for CircleShape
        if (shape is CircleShape circle)
        {
            GenerateCircleSpiral(segments, circle, stepover, startAngleRad, settings.SpiralInward, ref currentPosition);
            return segments;
        }

        // Universal boundary-clipped Archimedean spiral for general polygons/rectangles
        List<HatchGeometry.Point2D> poly = HatchGeometry.GetPolygon2D(shape);
        if (poly.Count < 3)
        {
            return segments;
        }

        // Find max distance from center to any boundary vertex
        float maxR = 0.001f;
        var center2D = new HatchGeometry.Point2D(cx, cy);
        for (int i = 0; i < poly.Count; i++)
        {
            maxR = MathF.Max(maxR, center2D.DistanceTo(poly[i]));
        }

        float totalTurns = maxR / stepover;
        int totalSteps = Math.Max(SamplesPerRevolution, (int)MathF.Ceiling(totalTurns * SamplesPerRevolution));
        float dTheta = (2.0f * MathF.PI) / SamplesPerRevolution;
        float b = stepover / (2.0f * MathF.PI);

        var strokes = new List<List<ToolpathPoint>>();
        List<ToolpathPoint>? currentStroke = null;

        HatchGeometry.Point2D prevPt = center2D;
        bool prevInside = HatchGeometry.IsPointInPolygon(prevPt, poly);
        if (prevInside)
        {
            currentStroke = [new ToolpathPoint(prevPt.X, prevPt.Y, z)];
        }

        for (int step = 1; step <= totalSteps; step++)
        {
            float theta = step * dTheta;
            float r = b * theta;
            if (r > maxR * 1.05f)
            {
                break;
            }

            float angle = theta + startAngleRad;
            var currPt = new HatchGeometry.Point2D(cx + r * MathF.Cos(angle), cy + r * MathF.Sin(angle));
            bool currInside = HatchGeometry.IsPointInPolygon(currPt, poly);

            if (prevInside && currInside)
            {
                currentStroke ??= [new ToolpathPoint(prevPt.X, prevPt.Y, z)];
                currentStroke.Add(new ToolpathPoint(currPt.X, currPt.Y, z));
            }
            else if (prevInside && !currInside)
            {
                // Crossing from inside to outside: clip at boundary exit
                if (TryFindBoundaryIntersection(poly, prevPt, currPt, out HatchGeometry.Point2D exitPt))
                {
                    currentStroke ??= [new ToolpathPoint(prevPt.X, prevPt.Y, z)];
                    currentStroke.Add(new ToolpathPoint(exitPt.X, exitPt.Y, z));
                }

                if (currentStroke is { Count: >= 2 })
                {
                    strokes.Add(currentStroke);
                }
                currentStroke = null;
            }
            else if (!prevInside && currInside)
            {
                // Crossing from outside to inside: clip at boundary entry
                if (TryFindBoundaryIntersection(poly, prevPt, currPt, out HatchGeometry.Point2D entryPt))
                {
                    currentStroke = [new ToolpathPoint(entryPt.X, entryPt.Y, z), new ToolpathPoint(currPt.X, currPt.Y, z)];
                }
                else
                {
                    currentStroke = [new ToolpathPoint(currPt.X, currPt.Y, z)];
                }
            }

            prevPt = currPt;
            prevInside = currInside;
        }

        if (currentStroke is { Count: >= 2 })
        {
            strokes.Add(currentStroke);
        }

        if (settings.SpiralInward)
        {
            strokes.Reverse();
            foreach (List<ToolpathPoint> stroke in strokes)
            {
                stroke.Reverse();
            }
        }

        foreach (List<ToolpathPoint> stroke in strokes)
        {
            if (stroke.Count < 2)
            {
                continue;
            }

            if (currentPosition is null || currentPosition.Value.DistanceTo(stroke[0]) > 0.0001f)
            {
                if (currentPosition is not null)
                {
                    segments.Add(new ToolpathSegment(currentPosition.Value, stroke[0], SegmentType.Rapid, layerId));
                }
            }
            currentPosition = stroke[0];

            for (int i = 0; i < stroke.Count - 1; i++)
            {
                segments.Add(new ToolpathSegment(stroke[i], stroke[i + 1], SegmentType.Hatch, layerId));
            }
            currentPosition = stroke[^1];
        }

        return segments;
    }

    private static void GenerateCircleSpiral(
        List<ToolpathSegment> segments,
        CircleShape circle,
        float stepover,
        float startAngleRad,
        bool inward,
        ref ToolpathPoint? currentPosition)
    {
        float rMax = circle.Radius;
        if (rMax <= 0.001f)
        {
            return;
        }

        float cx = circle.PositionX;
        float cy = circle.PositionY;
        float cz = circle.PositionZ;
        int layerId = circle.LayerId;

        float totalTurns = rMax / stepover;
        int totalSteps = Math.Max(SamplesPerRevolution, (int)MathF.Ceiling(totalTurns * SamplesPerRevolution));
        float dTheta = (2.0f * MathF.PI) / SamplesPerRevolution;
        float b = stepover / (2.0f * MathF.PI);

        var center = new ToolpathPoint(cx, cy, cz);
        var points = new List<ToolpathPoint>(totalSteps + 1) { center };

        for (int step = 1; step <= totalSteps; step++)
        {
            float theta = step * dTheta;
            float r = MathF.Min(rMax, b * theta);
            float angle = theta + startAngleRad;

            float x = cx + r * MathF.Cos(angle);
            float y = cy + r * MathF.Sin(angle);
            points.Add(new ToolpathPoint(x, y, cz));

            if (r >= rMax - 1e-4f)
            {
                break;
            }
        }

        if (inward)
        {
            points.Reverse();
        }

        if (points.Count < 2)
        {
            return;
        }

        if (currentPosition is null || currentPosition.Value.DistanceTo(points[0]) > 0.0001f)
        {
            if (currentPosition is not null)
            {
                segments.Add(new ToolpathSegment(currentPosition.Value, points[0], SegmentType.Rapid, layerId));
            }
        }
        currentPosition = points[0];

        for (int i = 0; i < points.Count - 1; i++)
        {
            segments.Add(new ToolpathSegment(points[i], points[i + 1], SegmentType.Hatch, layerId));
        }
        currentPosition = points[^1];
    }

    private static bool TryFindBoundaryIntersection(
        IReadOnlyList<HatchGeometry.Point2D> polygon,
        HatchGeometry.Point2D p1,
        HatchGeometry.Point2D p2,
        out HatchGeometry.Point2D nearestIntersection)
    {
        nearestIntersection = default;
        float minDistance = float.MaxValue;
        bool found = false;

        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            if (HatchGeometry.TryIntersectSegments(p1, p2, polygon[j], polygon[i], out HatchGeometry.Point2D hit))
            {
                float dist = p1.DistanceTo(hit);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestIntersection = hit;
                    found = true;
                }
            }
        }

        return found;
    }
}
