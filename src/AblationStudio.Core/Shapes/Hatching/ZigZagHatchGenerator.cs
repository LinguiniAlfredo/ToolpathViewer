using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes.Hatching;

public static class ZigZagHatchGenerator
{
    public static List<ToolpathSegment> Generate(
        ToolpathShape shape,
        HatchSettings settings,
        ref ToolpathPoint? currentPosition)
    {
        var segments = new List<ToolpathSegment>();
        List<HatchGeometry.Point2D> poly = HatchGeometry.GetPolygon2D(shape);
        if (poly.Count < 3)
        {
            return segments;
        }

        float z = shape.PositionZ;
        int layerId = shape.LayerId;

        // 1. Primary pass at AngleDegrees
        GeneratePass(segments, poly, settings, settings.AngleDegrees, z, layerId, ref currentPosition);

        // 2. Optional Orthogonal Cross-Hatch pass
        if (settings.CrossHatch)
        {
            float crossAngle = (settings.AngleDegrees + 90f) % 360f;
            GeneratePass(segments, poly, settings, crossAngle, z, layerId, ref currentPosition);
        }

        return segments;
    }

    private sealed record Scanline(float Y, List<float> XIntersections);

    private static void GeneratePass(
        List<ToolpathSegment> segments,
        IReadOnlyList<HatchGeometry.Point2D> polygon,
        HatchSettings settings,
        float angleDegrees,
        float z,
        int layerId,
        ref ToolpathPoint? currentPosition)
    {
        float stepover = MathF.Max(HatchSettings.MinStepover, settings.Stepover);
        float rad = angleDegrees * (MathF.PI / 180f);
        float cosFwd = MathF.Cos(rad);
        float sinFwd = MathF.Sin(rad);

        // Rotate polygon by -angle to align scanlines horizontally along the X-axis
        float cosInv = MathF.Cos(-rad);
        float sinInv = MathF.Sin(-rad);

        var rotatedPoly = new HatchGeometry.Point2D[polygon.Count];
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < polygon.Count; i++)
        {
            HatchGeometry.Point2D r = polygon[i].Rotate(cosInv, sinInv);
            rotatedPoly[i] = r;
            minY = MathF.Min(minY, r.Y);
            maxY = MathF.Max(maxY, r.Y);
        }

        float totalHeight = maxY - minY;
        if (totalHeight <= stepover * 0.1f)
        {
            return;
        }

        // 1. Precompute all valid scanlines and their X intersections
        float startY = minY + stepover * 0.5f;
        var scanlines = new List<Scanline>();
        var tempIntersections = new List<float>();

        for (float y = startY; y < maxY; y += stepover)
        {
            tempIntersections.Clear();
            int count = rotatedPoly.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                HatchGeometry.Point2D p1 = rotatedPoly[j];
                HatchGeometry.Point2D p2 = rotatedPoly[i];

                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    float dy = p2.Y - p1.Y;
                    if (MathF.Abs(dy) > 1e-9f)
                    {
                        float t = (y - p1.Y) / dy;
                        float x = p1.X + t * (p2.X - p1.X);
                        tempIntersections.Add(x);
                    }
                }
            }

            if (tempIntersections.Count >= 2)
            {
                tempIntersections.Sort();
                scanlines.Add(new Scanline(y, [.. tempIntersections]));
            }
        }

        int totalLines = scanlines.Count;
        if (totalLines == 0)
        {
            return;
        }

        // 2. Determine ordered sequence of scanline indices
        List<int> lineSequence = GetLineSequence(totalLines, settings.LineSkip, settings.AutoLineSkip);

        // 3. Traverse scanlines in the planned thermal dispersion order
        foreach (int lineIdx in lineSequence)
        {
            Scanline scanline = scanlines[lineIdx];
            List<float> xInts = scanline.XIntersections;
            if (xInts.Count < 2)
            {
                continue;
            }

            // Determine traversal direction: choose the endpoint closer to currentPosition to minimize rapid travel
            var ptFirst2D = new HatchGeometry.Point2D(xInts[0], scanline.Y).Rotate(cosFwd, sinFwd);
            var ptLast2D = new HatchGeometry.Point2D(xInts[^1], scanline.Y).Rotate(cosFwd, sinFwd);

            bool reverse = false;
            if (currentPosition is not null)
            {
                var ptFirst = new ToolpathPoint(ptFirst2D.X, ptFirst2D.Y, z);
                var ptLast = new ToolpathPoint(ptLast2D.X, ptLast2D.Y, z);
                if (currentPosition.Value.DistanceTo(ptLast) < currentPosition.Value.DistanceTo(ptFirst))
                {
                    reverse = true;
                }
            }

            if (reverse)
            {
                for (int k = xInts.Count - 2; k >= 0; k -= 2)
                {
                    EmitStroke(segments, xInts[k + 1], xInts[k], scanline.Y, cosFwd, sinFwd, z, layerId, ref currentPosition);
                }
            }
            else
            {
                for (int k = 0; k < xInts.Count - 1; k += 2)
                {
                    EmitStroke(segments, xInts[k], xInts[k + 1], scanline.Y, cosFwd, sinFwd, z, layerId, ref currentPosition);
                }
            }
        }
    }

    private static void EmitStroke(
        List<ToolpathSegment> segments,
        float xA,
        float xB,
        float y,
        float cosFwd,
        float sinFwd,
        float z,
        int layerId,
        ref ToolpathPoint? currentPosition)
    {
        if (MathF.Abs(xB - xA) < 1e-4f)
        {
            return;
        }

        var ptStart2D = new HatchGeometry.Point2D(xA, y).Rotate(cosFwd, sinFwd);
        var ptEnd2D = new HatchGeometry.Point2D(xB, y).Rotate(cosFwd, sinFwd);

        var startPoint = new ToolpathPoint(ptStart2D.X, ptStart2D.Y, z);
        var endPoint = new ToolpathPoint(ptEnd2D.X, ptEnd2D.Y, z);

        if (currentPosition is null || currentPosition.Value.DistanceTo(startPoint) > 0.0001f)
        {
            if (currentPosition is not null)
            {
                segments.Add(new ToolpathSegment(currentPosition.Value, startPoint, SegmentType.Rapid, layerId));
            }
        }

        segments.Add(new ToolpathSegment(startPoint, endPoint, SegmentType.Hatch, layerId));
        currentPosition = endPoint;
    }

    public static List<int> GetLineSequence(int totalLines, int lineSkip, bool autoLineSkip)
    {
        if (totalLines <= 0)
        {
            return [];
        }

        if (autoLineSkip)
        {
            return GenerateThermalDispersionSequence(totalLines);
        }

        int n = Math.Max(1, lineSkip);
        if (n == 1)
        {
            return Enumerable.Range(0, totalLines).ToList();
        }

        var sequence = new List<int>(totalLines);
        for (int p = 0; p < n && p < totalLines; p++)
        {
            for (int i = p; i < totalLines; i += n)
            {
                sequence.Add(i);
            }
        }

        return sequence;
    }

    /// <summary>
    /// Generates a scanline ordering using 1D Furthest Point Sampling and a decaying thermal potential field.
    /// At each step, places the next cut in the coldest available region on the workpiece, maximizing distance
    /// to all recently marked lines to prevent localized heating zones.
    /// Uses an O(N^2) thermal recurrence with precomputed distance kernel for high-throughput performance.
    /// </summary>
    public static List<int> GenerateThermalDispersionSequence(int totalLines)
    {
        if (totalLines <= 0)
        {
            return [];
        }

        if (totalLines == 1)
        {
            return [0];
        }

        if (totalLines == 2)
        {
            return [0, 1];
        }

        var sequence = new List<int>(totalLines);
        const float coolingFactor = 0.85f;

        // Precompute spatial heat kernel: kernel[d] = 1.0f / (d * d + 0.1f)
        // Eliminates repeated division and square calculations in the inner loop
        float[] kernel = new float[totalLines];
        for (int d = 0; d < totalLines; d++)
        {
            kernel[d] = 1.0f / (d * (float)d + 0.1f);
        }

        // 1. Initial boundaries: mark extremes first to establish global boundary
        sequence.Add(0);
        sequence.Add(totalLines - 1);

        int remainingCount = totalLines - 2;
        int[] remaining = new int[remainingCount];
        for (int i = 0; i < remainingCount; i++)
        {
            remaining[i] = i + 1;
        }

        // Track accumulated thermal energy field for each candidate
        // Step 0 was line 0 (decayed by coolingFactor^1 at step 2)
        // Step 1 was line (totalLines - 1) (decayed by coolingFactor^0 at step 2)
        float[] heat = new float[totalLines];
        int lastLine = totalLines - 1;
        for (int i = 0; i < remainingCount; i++)
        {
            int c = remaining[i];
            heat[c] = coolingFactor * kernel[c] + kernel[lastLine - c];
        }

        // 2. Greedily pick candidate with minimum residual thermal energy from all previous lines
        while (remainingCount > 0)
        {
            int bestIdx = 0;
            int bestCandidate = remaining[0];
            float minHeat = heat[bestCandidate];

            for (int i = 1; i < remainingCount; i++)
            {
                int candidate = remaining[i];
                float h = heat[candidate];
                if (h < minHeat || (MathF.Abs(h - minHeat) < 1e-9f && candidate < bestCandidate))
                {
                    minHeat = h;
                    bestCandidate = candidate;
                    bestIdx = i;
                }
            }

            sequence.Add(bestCandidate);

            // Swap-remove chosen candidate in O(1)
            remaining[bestIdx] = remaining[remainingCount - 1];
            remainingCount--;

            if (remainingCount == 0)
            {
                break;
            }

            // Update residual heat for all remaining candidates in O(remainingCount):
            // Each existing line's thermal contribution decays by coolingFactor,
            // plus the newly added line's heat impulse kernel[|c - bestCandidate|].
            for (int i = 0; i < remainingCount; i++)
            {
                int candidate = remaining[i];
                int dist = Math.Abs(candidate - bestCandidate);
                heat[candidate] = heat[candidate] * coolingFactor + kernel[dist];
            }
        }

        return sequence;
    }
}
