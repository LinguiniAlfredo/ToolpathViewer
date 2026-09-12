using System.Text.Json;
using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using AblationStudio.Core.Projects;
using AblationStudio.Core.Shapes;
using AblationStudio.Core.Shapes.Hatching;
using Xunit;

namespace AblationStudio.Tests;

public sealed class PathShapeTests
{
    private const string QLogoCls = """
        TOOL PATH/EXTERNAL_TRIM
        TLDATA/WEDM,0.2000,25.0000,0.0000
        MSYS/5.1250,4.3250,0.5000,1.0000000,0.0000000,0.0000000,0.0000000,1.0000000,0.0000000
        $$ centerline data
        PAINT/PATH
        PAINT/COLOR,186
        GOTO/-5.1250,3.4613,0.0000,0.0000000,0.0000000,1.0000000
        PAINT/COLOR,31
        CIRCLE/-4.2750,3.4613,0.0000,0.0000000,0.0000000,1.0000000,0.8500,0.0600,0.5000,0.0000,0.0000
        GOTO/-4.2750,4.3113,0.0000
        GOTO/4.8775,4.3113,0.0000
        GOTO/4.8775,3.4472,0.0000
        GOTO/2.1750,0.7355,0.0000
        GOTO/2.1750,1.8250,0.0000
        GOTO/-2.1750,1.8250,0.0000
        GOTO/-2.1750,-1.8250,0.0000
        GOTO/0.1006,-1.8250,0.0000
        GOTO/4.8775,2.9680,0.0000
        GOTO/4.8775,-0.6405,0.0000
        GOTO/1.1930,-4.3250,0.0000
        GOTO/-4.2750,-4.3250,0.0000
        CIRCLE/-4.2750,-3.4750,0.0000,0.0000000,0.0000000,1.0000000,0.8500,0.0600,0.5000,0.0000,0.0000
        GOTO/-5.1250,-3.4750,0.0000
        GOTO/-5.1250,3.4613,0.0000
        CUTCOM/LEFT,0
        PAINT/COLOR,186
        GOTO/1.6775,-4.3250,0.0000
        PAINT/COLOR,31
        GOTO/4.8775,-1.1249,0.0000
        GOTO/4.8775,-3.7250,0.0000
        CIRCLE/4.2775,-3.7250,0.0000,0.0000000,0.0000000,1.0000000,0.6000,0.0600,0.5000,0.0000,0.0000
        GOTO/4.2775,-4.3250,0.0000
        GOTO/1.6775,-4.3250,0.0000
        PAINT/TOOL,NOMORE
        END-OF-PATH
        """;

    private const string TwoSquaresCls = """
        TOOL PATH/TWO_SQUARES
        $$ Square 1 at (-5, 0)
        PAINT/COLOR,186
        GOTO/-6,-1,0
        PAINT/COLOR,31
        GOTO/-4,-1,0
        GOTO/-4,1,0
        GOTO/-6,1,0
        GOTO/-6,-1,0
        $$ Square 2 at (+5, 0)
        PAINT/COLOR,186
        GOTO/4,-1,0
        PAINT/COLOR,31
        GOTO/6,-1,0
        GOTO/6,1,0
        GOTO/4,1,0
        GOTO/4,-1,0
        END-OF-PATH
        """;

    [Fact]
    public void ExtractShape_QLogo_ExtractsCompoundPathWithTwoClosedContours()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(QLogoCls, "Q_Logo.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);

        Assert.NotNull(shape);
        Assert.Equal(2, shape.ContoursCount);
        Assert.True(shape.Contours[0].IsClosed);
        Assert.True(shape.Contours[1].IsClosed);
        Assert.True(shape.IsClosed);

        // Bounding box matches original toolpath bounds
        BoundingBox3D shapeBounds = shape.GetBounds();
        Assert.Equal(-5.125f, shapeBounds.MinX, precision: 2);
        Assert.Equal(4.878f, shapeBounds.MaxX, precision: 2);
        Assert.Equal(-4.325f, shapeBounds.MinY, precision: 2);
        Assert.Equal(4.311f, shapeBounds.MaxY, precision: 2);
    }

    [Fact]
    public void ExtractShape_TwoSquares_BuildsSingleCompoundShape()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);

        Assert.NotNull(shape);
        Assert.Equal(2, shape.ContoursCount);
        Assert.True(shape.IsClosed);
        // Center is at (0, 0, 0) between -6 and +6
        Assert.Equal(0f, shape.PositionX, precision: 3);
        Assert.Equal(0f, shape.PositionY, precision: 3);
    }

    [Fact]
    public void SpiralHatch_TwoSquares_GeneratesSingleUnifiedSpiralCoveringBothSquares()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        // Enable Spiral hatch on the compound shape
        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.Spiral;
        shape.Hatch.Stepover = 0.5f;

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        // Must generate hatch segments
        List<ToolpathSegment> hatchSegs = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.True(hatchSegs.Count > 10, "Expected spiral hatch segments across the two squares");

        // Verify that hatch strokes appear in both the left square (X < 0) and the right square (X > 0)
        bool hasLeftHatch = hatchSegs.Any(s => s.Start.X < -3.5f && s.Start.X > -6.5f);
        bool hasRightHatch = hatchSegs.Any(s => s.Start.X > 3.5f && s.Start.X < 6.5f);
        Assert.True(hasLeftHatch, "Spiral fill should cut inside the left square");
        Assert.True(hasRightHatch, "Spiral fill should cut inside the right square");

        // Verify that in the gap between the squares (-4 to +4), there are no cutting hatch segments
        bool hasCenterHatch = hatchSegs.Any(s => (s.Start.X > -3.5f && s.Start.X < 3.5f) && (s.End.X > -3.5f && s.End.X < 3.5f));
        Assert.False(hasCenterHatch, "Spiral fill should NOT cut in the empty space between disjoint squares");
    }

    [Fact]
    public void ZigZagHatch_TwoSquares_GeneratesScanlinesCrossingBothSquaresWithRapidGaps()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.ZigZag;
        shape.Hatch.Stepover = 0.5f;
        shape.Hatch.AngleDegrees = 0f; // Horizontal scanlines

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        List<ToolpathSegment> hatchSegs = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.True(hatchSegs.Count >= 6, "Expected horizontal raster strokes across both squares");

        // Should have hatch in both squares
        Assert.Contains(hatchSegs, s => s.Start.X < -3f);
        Assert.Contains(hatchSegs, s => s.Start.X > 3f);
    }

    [Fact]
    public void Translate_PathShape_TranslatesAllContoursTogether()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        float initialX = shape.PositionX;
        float initialY = shape.PositionY;

        // Move the shape by (10, 20, 0)
        shape.Translate(10f, 20f, 0f);

        Assert.Equal(initialX + 10f, shape.PositionX, precision: 3);
        Assert.Equal(initialY + 20f, shape.PositionY, precision: 3);

        // Bounding box should also be shifted by (10, 20)
        BoundingBox3D bounds = shape.GetBounds();
        Assert.Equal(4f, bounds.MinX, precision: 2);   // was -6 + 10 = 4
        Assert.Equal(16f, bounds.MaxX, precision: 2);  // was 6 + 10 = 16
        Assert.Equal(19f, bounds.MinY, precision: 2);  // was -1 + 20 = 19
        Assert.Equal(21f, bounds.MaxY, precision: 2);  // was 1 + 20 = 21
    }

    [Fact]
    public void HitTest_PathShape_ReturnsTrueInsideContour_AndFalseInEmptySpace()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        // Inside left square (-5, 0)
        Assert.True(shape.HitTest(-5f, 0f, 0.1f));

        // Inside right square (+5, 0)
        Assert.True(shape.HitTest(5f, 0f, 0.1f));

        // Empty space between squares at (0, 0)
        Assert.False(shape.HitTest(0f, 0f, 0.1f));

        // Far outside
        Assert.False(shape.HitTest(20f, 20f, 0.1f));
    }

    [Fact]
    public void Serialization_PathShape_RoundTripsAccurately()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(TwoSquaresCls, "TwoSquares.cls");
        PathShape? original = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(original);

        original.Hatch.IsEnabled = true;
        original.Hatch.Pattern = HatchPatternType.Spiral;
        original.Hatch.Stepover = 0.35f;

        ShapeDto dto = ShapeDto.FromShape(original);
        string json = JsonSerializer.Serialize(dto);

        ShapeDto? deserializedDto = JsonSerializer.Deserialize<ShapeDto>(json);
        Assert.NotNull(deserializedDto);
        Assert.IsType<PathShapeDto>(deserializedDto);

        ToolpathShape restoredShape = deserializedDto.ToShape();
        var restoredPath = Assert.IsType<PathShape>(restoredShape);

        Assert.Equal(original.ContoursCount, restoredPath.ContoursCount);
        Assert.Equal(original.PositionX, restoredPath.PositionX, precision: 4);
        Assert.Equal(original.PositionY, restoredPath.PositionY, precision: 4);
        Assert.True(restoredPath.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.Spiral, restoredPath.Hatch.Pattern);
        Assert.Equal(0.35f, restoredPath.Hatch.Stepover, precision: 3);
    }

    [Fact]
    public void ExtractShape_Box4mm_ExtractsSingleClosedContour()
    {
        const string box4mm = """
            HCH 1 1
            SL X-2 Y-2 M05
            SL X-2 Y-2 M03
            SL X2 Y-2 M03
            SL X2 Y2 M03
            SL X-2 Y2 M03
            SL X-2 Y-2 M03
            """;

        Toolpath toolpath = ToolpathParser.ParseText(box4mm, "Box4mm.h");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);

        Assert.NotNull(shape);
        Assert.Single(shape.Contours);
        Assert.True(shape.Contours[0].IsClosed);
        Assert.True(shape.IsClosed);
        Assert.Equal(4.0f, shape.GetBounds().SizeX, precision: 3);
        Assert.Equal(4.0f, shape.GetBounds().SizeY, precision: 3);
    }

    [Theory]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.cls")]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.txt")]
    public async Task ExtractShape_FlagFile_Extracts57Contours_AndContiguousSpiralFill(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);
        PathShape? flagShape = ToolpathShapeConverter.ExtractShape(toolpath);

        Assert.NotNull(flagShape);
        Assert.Equal(57, flagShape.ContoursCount);
        Assert.True(flagShape.IsClosed);

        // Enable Spiral Hatch on the entire flag
        flagShape.Hatch.IsEnabled = true;
        flagShape.Hatch.Pattern = HatchPatternType.Spiral;
        flagShape.Hatch.Stepover = 0.2f;

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = flagShape.GenerateSegments(pos).ToList();

        List<ToolpathSegment> hatchSegments = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.True(hatchSegments.Count > 100, "Expected contiguous spiral hatch segments across the flag");

        // Verify that hatch strokes span across multiple distinct regions of the flag
        Assert.Contains(hatchSegments, s => s.Start.X < -2.0f && s.Start.Y > 0.5f); // Upper left stars area
        Assert.Contains(hatchSegments, s => s.Start.Y < -0.5f);                     // Lower stripes area
    }

    [Fact]
    public void SpiralHatch_QLogo_NeverEntersCutoutHole()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(QLogoCls, "Q_Logo.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.Spiral;
        shape.Hatch.Stepover = 0.1f;

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        List<ToolpathSegment> hatchSegs = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchSegs);

        // The inner cutout core of Q logo is bounded by X in [-2.1, 0.0] and Y in [-1.7, 1.7]
        // (to the right of X=0.1 is the channel connecting outward).
        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(shape);
        List<HatchGeometry.LoopData> loops = HatchGeometry.BuildLoopDataList(rawLoops);

        foreach (ToolpathSegment s in hatchSegs)
        {
            var mid = new ToolpathPoint((s.Start.X + s.End.X) * 0.5f, (s.Start.Y + s.End.Y) * 0.5f, s.Start.Z);
            var pMid = new HatchGeometry.Point2D(mid.X, mid.Y);

            // Midpoint must strictly be inside the shape's polygon loops
            Assert.True(HatchGeometry.IsPointInLoops(pMid, loops), $"Hatch midpoint ({mid.X:F3},{mid.Y:F3}) is outside the polygon!");

            bool startInCoreHole = s.Start.X > -2.1f && s.Start.X < 0.0f && s.Start.Y > -1.7f && s.Start.Y < 1.7f;
            bool endInCoreHole = s.End.X > -2.1f && s.End.X < 0.0f && s.End.Y > -1.7f && s.End.Y < 1.7f;
            bool midInCoreHole = mid.X > -2.1f && mid.X < 0.0f && mid.Y > -1.7f && mid.Y < 1.7f;

            Assert.False(startInCoreHole && endInCoreHole, $"Hatch segment ({s.Start.X:F2},{s.Start.Y:F2}) -> ({s.End.X:F2},{s.End.Y:F2}) entered the Q core hole!");
            Assert.False(midInCoreHole, $"Hatch midpoint ({mid.X:F2},{mid.Y:F2}) crossed through the Q core hole!");
        }
    }

    [Fact]
    public void FollowProfile_QLogo_NeverEntersCutoutHole()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(QLogoCls, "Q_Logo.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.FollowProfile;
        shape.Hatch.Stepover = 0.15f;

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        List<ToolpathSegment> hatchSegs = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchSegs);

        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(shape);
        List<HatchGeometry.LoopData> loops = HatchGeometry.BuildLoopDataList(rawLoops);

        foreach (ToolpathSegment s in hatchSegs)
        {
            var mid = new ToolpathPoint((s.Start.X + s.End.X) * 0.5f, (s.Start.Y + s.End.Y) * 0.5f, s.Start.Z);
            var pMid = new HatchGeometry.Point2D(mid.X, mid.Y);

            // Midpoint must strictly be inside the shape's polygon loops
            Assert.True(HatchGeometry.IsPointInLoops(pMid, loops), $"FollowProfile midpoint ({mid.X:F3},{mid.Y:F3}) is outside the polygon!");

            bool startInCoreHole = s.Start.X > -2.1f && s.Start.X < 0.0f && s.Start.Y > -1.7f && s.Start.Y < 1.7f;
            bool endInCoreHole = s.End.X > -2.1f && s.End.X < 0.0f && s.End.Y > -1.7f && s.End.Y < 1.7f;
            bool midInCoreHole = mid.X > -2.1f && mid.X < 0.0f && mid.Y > -1.7f && mid.Y < 1.7f;

            Assert.False(startInCoreHole && endInCoreHole, $"FollowProfile segment ({s.Start.X:F2},{s.Start.Y:F2}) -> ({s.End.X:F2},{s.End.Y:F2}) entered the Q core hole!");
            Assert.False(midInCoreHole, $"FollowProfile midpoint ({mid.X:F2},{mid.Y:F2}) crossed into the Q core hole!");
        }
    }

    [Fact]
    public void FollowProfile_QLogo_HasNoSelfIntersections()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(QLogoCls, "Q_Logo.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.FollowProfile;
        shape.Hatch.Stepover = 0.15f;

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        // Extract hatch loops (each closed loop starts with Rapid or discontinuity)
        var hatchLoops = new List<List<ToolpathPoint>>();
        var currentLoop = new List<ToolpathPoint>();

        foreach (ToolpathSegment s in segments)
        {
            if (s.Type == SegmentType.Rapid)
            {
                if (currentLoop.Count > 2)
                {
                    hatchLoops.Add([.. currentLoop]);
                }
                currentLoop.Clear();
                continue;
            }

            if (s.Type == SegmentType.Hatch)
            {
                if (currentLoop.Count == 0)
                {
                    currentLoop.Add(s.Start);
                }
                currentLoop.Add(s.End);
            }
        }
        if (currentLoop.Count > 2)
        {
            hatchLoops.Add([.. currentLoop]);
        }

        Assert.NotEmpty(hatchLoops);

        // Check each loop for self-intersections (wrap-overs)
        int selfIntersectionCount = 0;
        foreach (List<ToolpathPoint> loop in hatchLoops)
        {
            int n = loop.Count;
            // Remove closing duplicate if present
            if (n > 2 && loop[0].DistanceTo(loop[^1]) < 1e-4f)
            {
                n--;
            }

            for (int i = 0; i < n; i++)
            {
                var a1 = new HatchGeometry.Point2D(loop[i].X, loop[i].Y);
                var a2 = new HatchGeometry.Point2D(loop[(i + 1) % n].X, loop[(i + 1) % n].Y);

                // Compare with non-adjacent edges
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1)
                    {
                        continue; // adjacent across wrap
                    }

                    var b1 = new HatchGeometry.Point2D(loop[j].X, loop[j].Y);
                    var b2 = new HatchGeometry.Point2D(loop[(j + 1) % n].X, loop[(j + 1) % n].Y);

                    if (HatchGeometry.TryIntersectSegments(a1, a2, b1, b2, out _))
                    {
                        selfIntersectionCount++;
                    }
                }
            }
        }

        Assert.Equal(0, selfIntersectionCount);
    }











    [Fact]
    public void FollowProfile_Star_DoesNotEscapeAndTerminatesCleanly()
    {
        const string starCls = """
            TOOL PATH/STAR
            PAINT/COLOR,186
            GOTO/-3.6066,1.7966,0.0000
            PAINT/COLOR,31
            GOTO/-3.5374,1.7465,0.0000
            GOTO/-3.5639,1.6652,0.0000
            GOTO/-3.4947,1.7155,0.0000
            GOTO/-3.4256,1.6652,0.0000
            GOTO/-3.4521,1.7465,0.0000
            GOTO/-3.3829,1.7966,0.0000
            GOTO/-3.4684,1.7966,0.0000
            GOTO/-3.4947,1.8779,0.0000
            GOTO/-3.5211,1.7966,0.0000
            GOTO/-3.6066,1.7966,0.0000
            END-OF-PATH
            """;

        Toolpath toolpath = ClsToolpathParser.ParseText(starCls, "Star.cls");
        PathShape? shape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(shape);

        shape.Hatch.IsEnabled = true;
        shape.Hatch.Pattern = HatchPatternType.FollowProfile;
        shape.Hatch.Stepover = 0.01f; // Very small stepover

        ToolpathPoint? pos = null;
        List<ToolpathSegment> segments = shape.GenerateSegments(pos).ToList();

        List<ToolpathSegment> hatchSegs = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchSegs);

        // Group into loops
        var loops = new List<List<ToolpathPoint>>();
        var cur = new List<ToolpathPoint>();
        foreach (ToolpathSegment s in segments)
        {
            if (s.Type == SegmentType.Rapid)
            {
                if (cur.Count > 0) loops.Add([.. cur]);
                cur.Clear();
                continue;
            }
            if (s.Type == SegmentType.Hatch)
            {
                if (cur.Count == 0) cur.Add(s.Start);
                cur.Add(s.End);
            }
        }
        if (cur.Count > 0) loops.Add([.. cur]);

        // Verify rings are clean and well-proportioned
        Assert.True(loops.Count >= 3, $"Expected >=3 rings for star, got {loops.Count}");

        // Innermost ring should maintain all 10 star vertices and be centered
        List<ToolpathPoint> innermostLoop = loops[^1];
        // Remove closing point if present
        int nInner = innermostLoop.Count;
        if (nInner > 2 && innermostLoop[0].DistanceTo(innermostLoop[^1]) < 1e-4f)
        {
            nInner--;
        }
        Assert.Equal(10, nInner);

        // Verify innermost ring center is not skewed (star center is approx (-3.495, 1.772))
        float innerMinX = innermostLoop.Take(nInner).Min(p => p.X);
        float innerMaxX = innermostLoop.Take(nInner).Max(p => p.X);
        float innerMinY = innermostLoop.Take(nInner).Min(p => p.Y);
        float innerMaxY = innermostLoop.Take(nInner).Max(p => p.Y);
        float innerCx = (innerMinX + innerMaxX) * 0.5f;
        float innerCy = (innerMinY + innerMaxY) * 0.5f;

        Assert.Equal(-3.495f, innerCx, precision: 2);
        Assert.Equal(1.762f, innerCy, precision: 2);

        // All hatch segment endpoints and midpoints must hit-test inside the star
        foreach (ToolpathSegment s in hatchSegs)
        {
            Assert.True(shape.HitTest(s.Start.X, s.Start.Y, 0.005f), $"Star hatch start point ({s.Start.X},{s.Start.Y}) escaped outside!");
            Assert.True(shape.HitTest(s.End.X, s.End.Y, 0.005f), $"Star hatch end point ({s.End.X},{s.End.Y}) escaped outside!");
            var mid = new ToolpathPoint((s.Start.X + s.End.X) * 0.5f, (s.Start.Y + s.End.Y) * 0.5f, s.Start.Z);
            Assert.True(shape.HitTest(mid.X, mid.Y, 0.005f), $"Star hatch midpoint ({mid.X},{mid.Y}) escaped outside!");
        }
    }



    [Fact]
    public void ShapeTranslation_PreservesAndOffsetsCachedHatch_WithoutRecomputing()
    {
        var circle = new CircleShape(0f, 0f, 0f, 5.0f);
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.Spiral;
        circle.Hatch.Stepover = 0.1f;

        // First compile: generates and caches hatch
        List<ToolpathSegment> initialSegments = circle.GenerateSegments(null).ToList();
        List<ToolpathSegment> initialHatch = initialSegments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(initialHatch);

        ToolpathPoint firstStartOrig = initialHatch[0].Start;
        ToolpathPoint firstEndOrig = initialHatch[0].End;

        // Translate shape by (5, -3, 0)
        circle.Translate(5f, -3f, 0f);

        // Second compile: should reuse and offset cached hatch segments
        List<ToolpathSegment> translatedSegments = circle.GenerateSegments(null).ToList();
        List<ToolpathSegment> translatedHatch = translatedSegments.Where(s => s.Type == SegmentType.Hatch).ToList();

        Assert.Equal(initialHatch.Count, translatedHatch.Count);
        Assert.Equal(firstStartOrig.X + 5f, translatedHatch[0].Start.X, precision: 4);
        Assert.Equal(firstStartOrig.Y - 3f, translatedHatch[0].Start.Y, precision: 4);
        Assert.Equal(firstEndOrig.X + 5f, translatedHatch[0].End.X, precision: 4);
        Assert.Equal(firstEndOrig.Y - 3f, translatedHatch[0].End.Y, precision: 4);
    }

    [Theory]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.cls")]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.txt")]
    public async Task Flag_SpiralAndFollowProfile_ZeroEscapingPoints(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);
        PathShape? flagShape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(flagShape);

        List<List<HatchGeometry.Point2D>> rawLoops = HatchGeometry.GetPolygonLoops(flagShape);
        List<HatchGeometry.LoopData> loops = HatchGeometry.BuildLoopDataList(rawLoops);

        // 1. Test Spiral
        flagShape.Hatch.IsEnabled = true;
        flagShape.Hatch.Pattern = HatchPatternType.Spiral;
        flagShape.Hatch.Stepover = 0.05f;

        List<ToolpathSegment> spiralSegments = flagShape.GenerateSegments(null).Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(spiralSegments);

        foreach (ToolpathSegment s in spiralSegments)
        {
            var pMid = new HatchGeometry.Point2D((s.Start.X + s.End.X) * 0.5f, (s.Start.Y + s.End.Y) * 0.5f);
            Assert.True(HatchGeometry.IsPointInLoops(pMid, loops), $"Spiral hatch midpoint ({pMid.X:F3}, {pMid.Y:F3}) escaped flag boundary!");
        }

        // 2. Test Follow Profile
        flagShape.Hatch.Pattern = HatchPatternType.FollowProfile;
        flagShape.Hatch.Stepover = 0.05f;

        List<ToolpathSegment> fpSegments = flagShape.GenerateSegments(null).Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(fpSegments);

        foreach (ToolpathSegment s in fpSegments)
        {
            var pMid = new HatchGeometry.Point2D((s.Start.X + s.End.X) * 0.5f, (s.Start.Y + s.End.Y) * 0.5f);
            Assert.True(HatchGeometry.IsPointInLoops(pMid, loops), $"Follow Profile hatch midpoint ({pMid.X:F3}, {pMid.Y:F3}) escaped flag boundary!");
        }
    }

    [Theory]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.cls")]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.txt")]
    public async Task Flag_FollowProfile_SmallStepover_StarsInnermostRingsAreSymmetrical(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);
        PathShape? flagShape = ToolpathShapeConverter.ExtractShape(toolpath);
        Assert.NotNull(flagShape);

        flagShape.Hatch.IsEnabled = true;
        flagShape.Hatch.Pattern = HatchPatternType.FollowProfile;
        flagShape.Hatch.Stepover = 0.01f;

        List<ToolpathSegment> segments = flagShape.GenerateSegments(null).ToList();

        // Extract closed loops in star region (X < -2.0, Y > 0.4)
        var starLoops = new List<List<ToolpathPoint>>();
        var curLoop = new List<ToolpathPoint>();

        foreach (ToolpathSegment s in segments)
        {
            if (s.Type == SegmentType.Rapid)
            {
                if (curLoop.Count > 2)
                {
                    if (curLoop.All(p => p.X < -1.8f && p.Y > 0.3f))
                    {
                        starLoops.Add([.. curLoop]);
                    }
                }
                curLoop.Clear();
                continue;
            }

            if (s.Type == SegmentType.Hatch)
            {
                if (curLoop.Count == 0) curLoop.Add(s.Start);
                curLoop.Add(s.End);
            }
        }
        if (curLoop.Count > 2 && curLoop.All(p => p.X < -1.8f && p.Y > 0.3f))
        {
            starLoops.Add([.. curLoop]);
        }

        // We expect plenty of star rings across the 50 stars at 0.01 stepover
        Assert.True(starLoops.Count >= 50, $"Expected >=50 star loops, got {starLoops.Count}");

        // Find the smallest (innermost) loops with width < 0.08 mm
        var innermostStarLoops = starLoops.Where(l => (l.Max(p => p.X) - l.Min(p => p.X)) < 0.08f).ToList();
        Assert.NotEmpty(innermostStarLoops);

        // Every innermost star loop must have 10 vertices (5 points, 5 valleys), not misshapen or skewed
        foreach (List<ToolpathPoint> loop in innermostStarLoops)
        {
            int n = loop.Count;
            if (n > 2 && loop[0].DistanceTo(loop[^1]) < 1e-4f) n--;

            // A properly formed 5-point star ring has 10 vertices
            Assert.Equal(10, n);

            // Check symmetry: center of X bounds and center of Y bounds should have aspect ratio approx 1.0
            float w = loop.Take(n).Max(p => p.X) - loop.Take(n).Min(p => p.X);
            float h = loop.Take(n).Max(p => p.Y) - loop.Take(n).Min(p => p.Y);
            float aspect = w / h;
            // Star width/height ratio is ~1.05
            Assert.InRange(aspect, 0.90f, 1.25f);
        }
    }
}

