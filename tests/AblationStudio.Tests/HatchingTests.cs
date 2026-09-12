using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using AblationStudio.Core.Shapes;
using AblationStudio.Core.Shapes.Hatching;
using Xunit;

namespace AblationStudio.Tests;

public sealed class HatchingTests
{
    [Fact]
    public void ZigZagHatch_OnRectangle_ProducesCorrectSpacingAndBounds()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 20f, height: 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.ZigZag;
        rect.Hatch.Stepover = 2.0f;
        rect.Hatch.AngleDegrees = 0f;
        rect.Hatch.KeepBoundary = true;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();

        // Must contain both Cut (profile) and Hatch segments
        List<ToolpathSegment> cutSegments = segments.Where(s => s.Type == SegmentType.Cut).ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        List<ToolpathSegment> rapidSegments = segments.Where(s => s.Type == SegmentType.Rapid).ToList();

        Assert.Equal(4, cutSegments.Count); // 4 rectangle boundary edges
        Assert.True(hatchSegments.Count >= 4, $"Expected at least 4 hatch lines, got {hatchSegments.Count}");
        Assert.True(rapidSegments.Count >= 1, "Expected rapid transitions");

        // Verify all hatch lines are horizontal (Angle = 0°) and strictly within [-10, 10] x [-5, 5]
        foreach (ToolpathSegment hatch in hatchSegments)
        {
            Assert.Equal(hatch.Start.Y, hatch.End.Y, precision: 3); // Horizontal
            Assert.InRange(hatch.Start.X, -10.01f, 10.01f);
            Assert.InRange(hatch.End.X, -10.01f, 10.01f);
            Assert.InRange(hatch.Start.Y, -5.01f, 5.01f);
            Assert.Equal(20f, hatch.Length, precision: 2); // Span full width of rectangle
        }
    }

    [Fact]
    public void ZigZagHatch_WithCrossHatch_GeneratesOrthogonalPasses()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 10f, height: 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.ZigZag;
        rect.Hatch.Stepover = 2.0f;
        rect.Hatch.AngleDegrees = 0f;
        rect.Hatch.CrossHatch = true;
        rect.Hatch.KeepBoundary = false;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        // Check horizontal passes (Angle 0°) and vertical passes (Angle 90°)
        int horizontalPasses = hatchSegments.Count(s => MathF.Abs(s.Start.Y - s.End.Y) < 1e-3f);
        int verticalPasses = hatchSegments.Count(s => MathF.Abs(s.Start.X - s.End.X) < 1e-3f);

        Assert.True(horizontalPasses >= 4, $"Expected horizontal passes, got {horizontalPasses}");
        Assert.True(verticalPasses >= 4, $"Expected vertical passes, got {verticalPasses}");
        Assert.Equal(hatchSegments.Count, horizontalPasses + verticalPasses);
    }

    [Fact]
    public void ZigZagHatch_OnCircle_ClipsToCircularBoundary()
    {
        const float radius = 10.0f;
        var circle = new CircleShape(0f, 0f, 0f, radius, segments: 128);
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.ZigZag;
        circle.Hatch.Stepover = 1.0f;
        circle.Hatch.AngleDegrees = 45f;
        circle.Hatch.KeepBoundary = true;

        List<ToolpathSegment> segments = circle.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.True(hatchSegments.Count > 10, $"Expected multiple hatch lines in circle, got {hatchSegments.Count}");

        // Verify every hatch point lies on or inside the circle
        foreach (ToolpathSegment hatch in hatchSegments)
        {
            float distStart = MathF.Sqrt(hatch.Start.X * hatch.Start.X + hatch.Start.Y * hatch.Start.Y);
            float distEnd = MathF.Sqrt(hatch.End.X * hatch.End.X + hatch.End.Y * hatch.End.Y);

            Assert.True(distStart <= radius + 0.1f, $"Hatch start ({hatch.Start.X}, {hatch.Start.Y}) outside circle: {distStart}");
            Assert.True(distEnd <= radius + 0.1f, $"Hatch end ({hatch.End.X}, {hatch.End.Y}) outside circle: {distEnd}");
        }
    }

    [Fact]
    public void SpiralHatch_OnCircle_StartsAtCenterAndExpandsOutward()
    {
        const float radius = 10.0f;
        const float stepover = 1.0f;
        var circle = new CircleShape(20f, 30f, 5f, radius);
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.Spiral;
        circle.Hatch.Stepover = stepover;
        circle.Hatch.KeepBoundary = true;

        List<ToolpathSegment> segments = circle.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.NotEmpty(hatchSegments);

        // First hatch point must be at center (20, 30, 5)
        ToolpathPoint firstPt = hatchSegments[0].Start;
        Assert.Equal(20f, firstPt.X, precision: 3);
        Assert.Equal(30f, firstPt.Y, precision: 3);
        Assert.Equal(5f, firstPt.Z, precision: 3);

        // Radii must expand outward monotonically
        float prevR = 0f;
        foreach (ToolpathSegment seg in hatchSegments)
        {
            float r = MathF.Sqrt(MathF.Pow(seg.End.X - 20f, 2) + MathF.Pow(seg.End.Y - 30f, 2));
            Assert.True(r >= prevR - 1e-4f, $"Radius not expanding monotonically: prev={prevR}, curr={r}");
            Assert.True(r <= radius + 1e-3f, $"Radius exceeded circle boundary: {r}");
            prevR = r;
        }

        // Final radius must be near outer radius
        ToolpathPoint lastPt = hatchSegments[^1].End;
        float finalR = MathF.Sqrt(MathF.Pow(lastPt.X - 20f, 2) + MathF.Pow(lastPt.Y - 30f, 2));
        Assert.InRange(finalR, radius * 0.95f, radius * 1.01f);
    }

    [Fact]
    public void SpiralHatch_OnRectangle_ClipsToBoundary()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 20f, height: 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.Spiral;
        rect.Hatch.Stepover = 1.0f;
        rect.Hatch.KeepBoundary = false;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.NotEmpty(hatchSegments);

        // Verify all hatch points stay within rectangle boundary [-10, 10] x [-5, 5]
        foreach (ToolpathSegment seg in hatchSegments)
        {
            Assert.InRange(seg.Start.X, -10.05f, 10.05f);
            Assert.InRange(seg.Start.Y, -5.05f, 5.05f);
            Assert.InRange(seg.End.X, -10.05f, 10.05f);
            Assert.InRange(seg.End.Y, -5.05f, 5.05f);
        }
    }

    [Fact]
    public void FollowProfile_OnCircle_GeneratesConcentricInwardRings()
    {
        const float radius = 10.0f;
        const float stepover = 2.0f;
        var circle = new CircleShape(0f, 0f, 0f, radius, segments: 32);
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.FollowProfile;
        circle.Hatch.Stepover = stepover;
        circle.Hatch.KeepBoundary = true;

        List<ToolpathSegment> segments = circle.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        // Inward rings at r = 8, 6, 4, 2 (4 rings * 32 segments = 128 segments)
        Assert.Equal(4 * 32, hatchSegments.Count);

        // Verify rings decrease in radius
        for (int ring = 0; ring < 4; ring++)
        {
            float expectedR = radius - (ring + 1) * stepover;
            int startIndex = ring * 32;
            for (int i = 0; i < 32; i++)
            {
                ToolpathSegment seg = hatchSegments[startIndex + i];
                float r = MathF.Sqrt(seg.Start.X * seg.Start.X + seg.Start.Y * seg.Start.Y);
                Assert.Equal(expectedR, r, precision: 2);
            }
        }
    }

    [Fact]
    public void FollowProfile_OnRectangle_GeneratesConcentricRectangles()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 20f, height: 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.FollowProfile;
        rect.Hatch.Stepover = 2.0f;
        rect.Hatch.KeepBoundary = false;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        // 2 concentric rings:
        // Ring 1: width 16, height 6 (4 edges)
        // Ring 2: width 12, height 2 (4 edges)
        // Ring 3: width 8, height -2 -> halted
        Assert.Equal(8, hatchSegments.Count);

        // Ring 1 total perimeter = 2 * (16 + 6) = 44 mm
        float ring1Perimeter = hatchSegments.Take(4).Sum(s => s.Length);
        Assert.Equal(44.0f, ring1Perimeter, precision: 2);

        // Ring 2 total perimeter = 2 * (12 + 2) = 28 mm
        float ring2Perimeter = hatchSegments.Skip(4).Take(4).Sum(s => s.Length);
        Assert.Equal(28.0f, ring2Perimeter, precision: 2);
    }

    [Fact]
    public void KeepBoundary_Toggle_ControlsProfileContourEmission()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 10f, height: 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.ZigZag;
        rect.Hatch.Stepover = 2.0f;

        // 1. KeepBoundary = true -> Cut segments exist
        rect.Hatch.KeepBoundary = true;
        List<ToolpathSegment> segsWithBoundary = rect.GenerateSegments().ToList();
        Assert.Contains(segsWithBoundary, s => s.Type == SegmentType.Cut);
        Assert.Contains(segsWithBoundary, s => s.Type == SegmentType.Hatch);

        // 2. KeepBoundary = false -> Only Hatch and Rapid segments exist
        rect.Hatch.KeepBoundary = false;
        List<ToolpathSegment> segsWithoutBoundary = rect.GenerateSegments().ToList();
        Assert.DoesNotContain(segsWithoutBoundary, s => s.Type == SegmentType.Cut);
        Assert.Contains(segsWithoutBoundary, s => s.Type == SegmentType.Hatch);
    }

    [Fact]
    public void ToolpathParser_ParsesWinbroStylePflAndHchBlocks_Correctly()
    {
        const string sampleHCode = """
            SST Section#1
            PFL 1; Profile 
            SL X0.320 Y-0.414 Z1.890 M05
            SL X0.320 Y-0.414 Z1.890 M03
            SL X0.289 Y-0.422 Z1.890 M03
            HCH 1; Hatch 
            SL X0.296 Y-0.419 Z1.890 M05
            SL X0.297 Y-0.420 Z1.890 M03
            PFL 2; Profile 
            SL X0.329 Y-0.410 Z1.880 M05
            SL X0.329 Y-0.410 Z1.880 M03
            SL X0.297 Y-0.420 Z1.880 M03
            SL X0.264 Y-0.423 Z1.880 M03
            SL X0.262 Y-0.423 Z1.880 M03
            HCH 2; Hatch 
            SL X0.289 Y-0.418 Z1.880 M05
            SL X0.291 Y-0.420 Z1.880 M03
            """;

        Toolpath toolpath = ToolpathParser.ParseText(sampleHCode, "WinbroSample.h");

        // Verify segments were parsed
        Assert.NotEmpty(toolpath.Segments);
        Assert.Equal(4, toolpath.Statistics.CutSegmentsCount);
        Assert.Equal(2, toolpath.Statistics.HatchSegmentsCount);
        Assert.Equal(3, toolpath.Statistics.RapidSegmentsCount);

        // Verify that PFL segments have SegmentType.Cut and HCH segments have SegmentType.Hatch
        List<ToolpathSegment> cuts = toolpath.Segments.Where(s => s.Type == SegmentType.Cut).ToList();
        List<ToolpathSegment> hatches = toolpath.Segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.All(cuts, c => Assert.Equal(SegmentType.Cut, c.Type));
        Assert.All(hatches, h => Assert.Equal(SegmentType.Hatch, h.Type));
    }

    [Fact]
    public void ShapeDocument_ExportToHCode_OutputsHatchDirectivesAndM03()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f, segments: 16);
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.FollowProfile;
        circle.Hatch.Stepover = 1.5f;
        circle.Hatch.KeepBoundary = true;
        doc.AddShape(circle);

        string hCode = doc.ExportToHCode("HatchExport.h");

        // Verify presence of directives
        Assert.Contains("HCH", hCode);
        Assert.Contains("M03", hCode);
        Assert.Contains("M05", hCode);

        // Parse back and verify round-trip
        Toolpath parsed = ToolpathParser.ParseText(hCode, "HatchExport.h");
        Assert.True(parsed.Segments.Count > 0);
        Assert.True(parsed.Statistics.TotalLaserLength > 0f);
    }

    [Fact]
    public void Hatching_ZigZag_LineSkip_TraversesEveryNthLineAndCoversAllLines()
    {
        // 10x10 mm rectangle with 1mm stepover -> 10 scanlines
        var rect = new RectangleShape(0f, 0f, 0f, 10f, 10f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.ZigZag;
        rect.Hatch.Stepover = 1.0f;
        rect.Hatch.AngleDegrees = 0f;
        rect.Hatch.KeepBoundary = false;
        rect.Hatch.LineSkip = 2;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        // Should have 10 hatch strokes
        Assert.Equal(10, hatchSegments.Count);

        // Group into passes by checking rapid repositioning moves
        // Pass 0 should have Y coordinates with step ~2.0, Pass 1 should have remaining Y coordinates
        List<float> yCoords = hatchSegments.Select(s => MathF.Round(s.Start.Y, 2)).ToList();

        // First 5 strokes (Pass 0, even lines)
        for (int i = 0; i < 4; i++)
        {
            float diff = MathF.Abs(yCoords[i + 1] - yCoords[i]);
            Assert.True(diff >= 1.8f, $"Expected ~2.0mm separation within pass 0, got {diff}");
        }

        // Between stroke 4 and 5 (transition from pass 0 to pass 1): there should be a large jump
        // All 10 unique scanlines must be covered
        var uniqueY = yCoords.Distinct().ToList();
        Assert.Equal(10, uniqueY.Count);
    }

    [Fact]
    public void Hatching_ZigZag_AutoLineSkip_MaximizesMinimumSubsequentDistance()
    {
        // 20x20 mm rectangle with 1mm stepover -> 20 scanlines
        var rect = new RectangleShape(0f, 0f, 0f, 20f, 20f);
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.ZigZag;
        rect.Hatch.Stepover = 1.0f;
        rect.Hatch.AngleDegrees = 0f;
        rect.Hatch.KeepBoundary = false;
        rect.Hatch.AutoLineSkip = true;

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.Equal(20, hatchSegments.Count);

        // Check distance between all consecutive hatch lines in the traversal order
        float minSeparation = float.MaxValue;
        for (int i = 0; i < hatchSegments.Count - 1; i++)
        {
            float dist = MathF.Abs(hatchSegments[i + 1].Start.Y - hatchSegments[i].Start.Y);
            minSeparation = MathF.Min(minSeparation, dist);
        }

        // With 20 scanlines and auto maximization, the minimum separation between any two consecutive lines is >= 3mm
        Assert.True(minSeparation >= 3.0f, $"Auto line skip should provide >= 3.0mm separation, got {minSeparation}");
    }

    [Fact]
    public void Hatching_HatchSettings_CloneAndCopyFrom_PreservesLineSkipAndAuto()
    {
        var settings = new HatchSettings
        {
            IsEnabled = true,
            Pattern = HatchPatternType.ZigZag,
            Stepover = 0.8f,
            LineSkip = 5,
            AutoLineSkip = true
        };

        HatchSettings cloned = settings.Clone();
        Assert.Equal(5, cloned.LineSkip);
        Assert.True(cloned.AutoLineSkip);

        var target = new HatchSettings();
        target.CopyFrom(cloned);
        Assert.Equal(5, target.LineSkip);
        Assert.True(target.AutoLineSkip);
    }

    [Fact]
    public void Hatching_ZigZag_ThermalDispersion_DispersesGloballyWithoutLocalizedHotspots()
    {
        const int totalLines = 16;
        List<int> sequence = ZigZagHatchGenerator.GenerateThermalDispersionSequence(totalLines);

        // 1. All scanlines covered with zero duplicates
        Assert.Equal(totalLines, sequence.Count);
        Assert.Equal(totalLines, sequence.Distinct().Count());

        // 2. Initial strokes establish opposing boundaries
        Assert.Equal(0, sequence[0]);
        Assert.Equal(totalLines - 1, sequence[1]);

        // 3. Early strokes (first 4) must span bottom, top, and middle (no localized clustering)
        List<int> firstQuarter = sequence.Take(4).ToList();
        Assert.Contains(0, firstQuarter);
        Assert.Contains(15, firstQuarter);
        Assert.True(firstQuarter.Any(idx => idx >= 6 && idx <= 9), "Must visit center region in first 4 strokes");

        // 4. Mean distance between consecutive strokes must be large across the entire part
        float totalDist = 0f;
        for (int i = 0; i < sequence.Count - 1; i++)
        {
            totalDist += Math.Abs(sequence[i + 1] - sequence[i]);
        }
        float avgDist = totalDist / (sequence.Count - 1);
        Assert.True(avgDist >= 4.0f, $"Average step distance should be >= 4.0 lines, got {avgDist}");
    }
}

