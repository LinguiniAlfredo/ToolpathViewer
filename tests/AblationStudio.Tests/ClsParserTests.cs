using System.IO;
using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using Xunit;

namespace AblationStudio.Tests;

public sealed class ClsParserTests
{
    private const string QLogoContent = """
        TOOL PATH/EXTERNAL_TRIM
        TLDATA/WEDM,0.2000,25.0000,0.0000
        MSYS/5.1250,4.3250,0.5000,1.0000000,0.0000000,0.0000000,0.0000000,1.0000000,0.0000000
        $$ centerline data
        PAINT/PATH
        PAINT/SPEED,10
        PAINT/PATH,DASH
        SET/UPPER,25.0000,LOWER,0.0000
        LOAD/WIRE
        PAINT/PATH
        CUTCOM/LEFT,0
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
        PAINT/SPEED,10
        PAINT/TOOL,NOMORE
        END-OF-PATH
        """;

    private const string StarClsContent = """
        TOOL PATH/STAR_TEST
        MSYS/0,0,0,1,0,0,0,1,0
        $$ Single star test
        PAINT/COLOR,186
        GOTO/-3.6066,1.7966,0.0000,0,0,1
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
        PAINT/COLOR,186
        GOTO/-3.1091,1.7966,0.0000
        PAINT/COLOR,31
        GOTO/-3.0399,1.7465,0.0000
        END-OF-PATH
        """;

    [Fact]
    public void ParseText_QLogo_ParsesExpectedSegmentsAndArcs()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(QLogoContent, "Q_Logo.cls");

        Assert.Equal("Q_Logo.cls", toolpath.Name);
        Assert.True(toolpath.Segments.Count > 20, $"Expected > 20 segments due to arc tessellation, got {toolpath.Segments.Count}");

        // There should be exactly 1 rapid transition between the outer Q profile and the inner cutout
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);
        Assert.True(toolpath.Statistics.CutSegmentsCount >= 20);

        // Bounding box checks
        Assert.Equal(-5.125f, toolpath.BoundingBox.MinX, precision: 3);
        Assert.Equal(4.878f, toolpath.BoundingBox.MaxX, precision: 3);
        Assert.Equal(-4.325f, toolpath.BoundingBox.MinY, precision: 3);
        Assert.Equal(4.311f, toolpath.BoundingBox.MaxY, precision: 3);
    }

    [Fact]
    public void ParseText_AutoDetectsClsContentInTxt()
    {
        // When parsed via generic ToolpathParser.ParseText with no explicit .cls name,
        // it should detect CLSF content and parse correctly
        Toolpath toolpath = ToolpathParser.ParseText(QLogoContent);

        Assert.Equal("EXTERNAL_TRIM", toolpath.Name);
        Assert.True(toolpath.Segments.Count > 20);
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);
    }

    [Fact]
    public void ParseText_StarAndRapidTransition_IdentifiesSegmentTypes()
    {
        Toolpath toolpath = ClsToolpathParser.ParseText(StarClsContent, "Star.cls");

        // Star has 10 cut moves, then 1 rapid reposition to next star, then 1 cut move = 11 cut, 1 rapid
        Assert.Equal(11, toolpath.Statistics.CutSegmentsCount);
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);
        Assert.Equal(12, toolpath.Segments.Count);

        // Check the rapid segment between stars
        ToolpathSegment rapidSeg = toolpath.Segments[10];
        Assert.Equal(SegmentType.Rapid, rapidSeg.Type);
        Assert.Equal(-3.6066f, rapidSeg.Start.X, precision: 3);
        Assert.Equal(-3.1091f, rapidSeg.End.X, precision: 3);
    }

    [Fact]
    public void ParseText_CircleTessellation_GeneratesSmoothContinuousArc()
    {
        // Quarter circle arc from (-1, 0, 0) around (0, 0, 0) to (0, 1, 0)
        const string circleContent = """
            TOOL PATH/ARC_TEST
            PAINT/COLOR,31
            GOTO/-1,0,0
            CIRCLE/0,0,0,0,0,1,1.0,0.01
            GOTO/0,1,0
            """;

        Toolpath toolpath = ClsToolpathParser.ParseText(circleContent);

        Assert.True(toolpath.Segments.Count >= 4);

        // Verify continuity: each segment starts where the previous ended
        for (int i = 0; i < toolpath.Segments.Count; i++)
        {
            ToolpathSegment seg = toolpath.Segments[i];
            Assert.Equal(SegmentType.Cut, seg.Type);

            if (i > 0)
            {
                ToolpathSegment prev = toolpath.Segments[i - 1];
                Assert.Equal(prev.End.X, seg.Start.X, precision: 4);
                Assert.Equal(prev.End.Y, seg.Start.Y, precision: 4);
                Assert.Equal(prev.End.Z, seg.Start.Z, precision: 4);
            }

            // Verify points lie on circle of radius 1.0
            float startDist = MathF.Sqrt(seg.Start.X * seg.Start.X + seg.Start.Y * seg.Start.Y);
            float endDist = MathF.Sqrt(seg.End.X * seg.End.X + seg.End.Y * seg.End.Y);
            Assert.Equal(1.0f, startDist, precision: 3);
            Assert.Equal(1.0f, endDist, precision: 3);
        }

        // Final point must be exactly (0, 1, 0)
        ToolpathSegment lastSeg = toolpath.Segments[^1];
        Assert.Equal(0f, lastSeg.End.X, precision: 4);
        Assert.Equal(1f, lastSeg.End.Y, precision: 4);
        Assert.Equal(0f, lastSeg.End.Z, precision: 4);
    }

    [Fact]
    public void IsClsContent_ReturnsTrueForClsData_AndFalseForLaserData()
    {
        Assert.True(ClsToolpathParser.IsClsContent(QLogoContent));
        Assert.True(ClsToolpathParser.IsClsContent(StarClsContent));

        const string laserContent = """
            HCH 1 1
            SL X0 Y0 Z0 M05
            SL X10 Y10 Z0 M03
            """;

        Assert.False(ClsToolpathParser.IsClsContent(laserContent));
    }

    [Theory]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Q_Logo.cls")]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Q_Logo.txt")]
    public async Task ParseFileAsync_QLogo_ParsesAccuratelyFromDisk(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);

        Assert.True(toolpath.Segments.Count > 20);
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);
        Assert.Equal(-5.125f, toolpath.BoundingBox.MinX, precision: 3);
        Assert.Equal(4.878f, toolpath.BoundingBox.MaxX, precision: 3);
    }

    [Theory]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.cls")]
    [InlineData(@"c:\Users\m_del\Source\vibe_test\example_toolpaths\Flag.txt")]
    public async Task ParseFileAsync_Flag_ParsesAccuratelyFromDisk(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);

        Assert.True(toolpath.Segments.Count > 500);
        Assert.True(toolpath.Statistics.CutSegmentsCount > 500);
        Assert.True(toolpath.Statistics.RapidSegmentsCount >= 50); // 50 stars + stripes
        Assert.Equal(-3.750f, toolpath.BoundingBox.MinX, precision: 3);
        Assert.Equal(3.750f, toolpath.BoundingBox.MaxX, precision: 3);
        Assert.Equal(-1.974f, toolpath.BoundingBox.MinY, precision: 3);
        Assert.Equal(1.974f, toolpath.BoundingBox.MaxY, precision: 3);
    }
}
