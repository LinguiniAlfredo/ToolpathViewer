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
        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(shape);
        List<HatchGeometry.LoopData> loops = HatchGeometry.BuildLoopDataList(rawLoops);
        if (loops.Count == 0)
        {
            return segments;
        }

        // Find max distance from center to any boundary vertex across all loops
        float maxR = 0.001f;
        var center2D = new HatchGeometry.Point2D(cx, cy);
        for (int l = 0; l < loops.Count; l++)
        {
            IReadOnlyList<HatchGeometry.Point2D> pts = loops[l].Points;
            for (int i = 0; i < pts.Count; i++)
            {
                maxR = MathF.Max(maxR, center2D.DistanceTo(pts[i]));
            }
        }

        float b = stepover / (2.0f * MathF.PI);
        float maxChord = Math.Clamp(stepover * 3.0f, 0.05f, 0.25f);
        const float minDTheta = (2.0f * MathF.PI) / 72f;

        var strokes = new List<List<ToolpathPoint>>();
        List<ToolpathPoint>? currentStroke = null;
        var scratchIntervals = new List<HatchGeometry.SegmentInterval>();
        var scratchT = new List<float>(8);

        float theta = 0f;
        HatchGeometry.Point2D prevPt = center2D;

        while (true)
        {
            float rPrev = b * theta;
            if (rPrev > maxR * 1.05f)
            {
                break;
            }

            // Adaptive angular step to bound chord length at large radii while keeping smooth curvature at small radii
            float dTheta = MathF.Min(minDTheta, maxChord / MathF.Max(0.01f, rPrev));
            theta += dTheta;
            float r = b * theta;

            float angle = theta + startAngleRad;
            var currPt = new HatchGeometry.Point2D(cx + r * MathF.Cos(angle), cy + r * MathF.Sin(angle));

            scratchIntervals.Clear();
            HatchGeometry.ClipSegmentToLoops(prevPt, currPt, loops, scratchIntervals, scratchT);

            if (scratchIntervals.Count == 0)
            {
                if (currentStroke is { Count: >= 2 })
                {
                    strokes.Add(currentStroke);
                }
                currentStroke = null;
            }
            else
            {
                for (int s = 0; s < scratchIntervals.Count; s++)
                {
                    HatchGeometry.SegmentInterval interval = scratchIntervals[s];
                    var pStart = new ToolpathPoint(interval.Start.X, interval.Start.Y, z);
                    var pEnd = new ToolpathPoint(interval.End.X, interval.End.Y, z);

                    if (currentStroke is not null && currentStroke[^1].DistanceTo(pStart) < 0.001f)
                    {
                        currentStroke.Add(pEnd);
                    }
                    else
                    {
                        if (currentStroke is { Count: >= 2 })
                        {
                            strokes.Add(currentStroke);
                        }
                        currentStroke = [pStart, pEnd];
                    }
                }
            }

            prevPt = currPt;
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
}
