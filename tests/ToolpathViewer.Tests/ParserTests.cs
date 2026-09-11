using ToolpathViewer.Core.Models;
using ToolpathViewer.Core.Parser;
using Xunit;

namespace ToolpathViewer.Tests;

public sealed class ParserTests
{
    private const string Cross1mmContent = """
        HCH 1 1 ;Hatch
        SL X0 Y0 Z0 M05
        SL X0.5 Y0 Z0 M05
        SL X-0.5 Y0 Z0 M03
        SL X0 Y0 Z0 M05
        SL X0 Y0.5 Z0 M05
        SL X0 Y-0.5 Z0 M03
        SL X0 Y0 Z0 M05
        """;

    private const string Box4mmContent = """
        HCH 1 1
        SL X-2 Y-2 M05
        SL X-2 Y-2 M03
        SL X2 Y-2 M03
        SL X2 Y2 M03
        SL X-2 Y-2 M05
        SL X-2 Y-2 M03
        SL X-2 Y2 M03
        SL X2 Y2 M03
        SL X2 Y2 M05
        """;

    [Fact]
    public void ParseText_Cross1mm_ParsesExpectedSegmentsAndDimensions()
    {
        Toolpath toolpath = ToolpathParser.ParseText(Cross1mmContent, "1mmCross.h");

        Assert.Equal("1mmCross.h", toolpath.Name);
        Assert.Equal(6, toolpath.Segments.Count);
        Assert.Equal(2, toolpath.Statistics.CutSegmentsCount);
        Assert.Equal(4, toolpath.Statistics.RapidSegmentsCount);

        // Cut moves should total exactly 2.0 mm (1mm horizontal + 1mm vertical)
        Assert.Equal(2.0f, toolpath.Statistics.TotalCutLength, precision: 3);

        // Rapid moves: (0,0)->(0.5,0) [0.5], (-0.5,0)->(0,0) [0.5], (0,0)->(0,0.5) [0.5], (0,-0.5)->(0,0) [0.5] = 2.0mm
        Assert.Equal(2.0f, toolpath.Statistics.TotalRapidLength, precision: 3);

        // Bounding box: X from -0.5 to 0.5 (width 1.0), Y from -0.5 to 0.5 (height 1.0)
        Assert.Equal(-0.5f, toolpath.BoundingBox.MinX, precision: 3);
        Assert.Equal(0.5f, toolpath.BoundingBox.MaxX, precision: 3);
        Assert.Equal(-0.5f, toolpath.BoundingBox.MinY, precision: 3);
        Assert.Equal(0.5f, toolpath.BoundingBox.MaxY, precision: 3);
        Assert.Equal(1.0f, toolpath.BoundingBox.SizeX, precision: 3);
        Assert.Equal(1.0f, toolpath.BoundingBox.SizeY, precision: 3);
    }

    [Fact]
    public void ParseText_Box4mm_ParsesBoxPerimeterAndRapidTransitions()
    {
        Toolpath toolpath = ToolpathParser.ParseText(Box4mmContent, "Box4mm.h");

        Assert.Equal("Box4mm.h", toolpath.Name);
        // 4 cut edges + 1 rapid diagonal move = 5 motion segments
        Assert.Equal(5, toolpath.Segments.Count);
        Assert.Equal(4, toolpath.Statistics.CutSegmentsCount);
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);

        // 4 edges of 4mm box: 4 * 4.0 = 16.0 mm
        Assert.Equal(16.0f, toolpath.Statistics.TotalCutLength, precision: 3);

        // Bounding box: [-2, 2] on both X and Y
        Assert.Equal(-2.0f, toolpath.BoundingBox.MinX, precision: 3);
        Assert.Equal(2.0f, toolpath.BoundingBox.MaxX, precision: 3);
        Assert.Equal(-2.0f, toolpath.BoundingBox.MinY, precision: 3);
        Assert.Equal(2.0f, toolpath.BoundingBox.MaxY, precision: 3);
        Assert.Equal(4.0f, toolpath.BoundingBox.SizeX, precision: 3);
        Assert.Equal(4.0f, toolpath.BoundingBox.SizeY, precision: 3);
    }

    [Fact]
    public void ParseText_HandlesCommentsAndEmptyLines_Gracefully()
    {
        const string input = """
            ; Leading comment
            HCH 2 0 ; Hatch header

            SL X10 Y20 Z5 M05 ; Rapid to 10,20,5

            ; Another comment line
            SL X30 Y20 Z5 M03 ; Cut to 30,20,5
            """;

        Toolpath toolpath = ToolpathParser.ParseText(input);

        Assert.Single(toolpath.Segments);
        ToolpathSegment segment = toolpath.Segments[0];
        Assert.Equal(SegmentType.Cut, segment.Type);
        Assert.Equal(2, segment.LayerId);
        Assert.Equal(10f, segment.Start.X);
        Assert.Equal(30f, segment.End.X);
        Assert.Equal(20f, segment.Length, precision: 3);
    }

    [Fact]
    public async Task ParseFileAsync_ReadsActualFilesFromDisk()
    {
        string crossPath = @"c:\Users\m_del\Source\vibe_test\example_toolpaths\1mmCross.h";
        if (File.Exists(crossPath))
        {
            Toolpath toolpath = await ToolpathParser.ParseFileAsync(crossPath);
            Assert.Equal("1mmCross.h", toolpath.Name);
            Assert.Equal(6, toolpath.Segments.Count);
            Assert.Equal(2, toolpath.Statistics.CutSegmentsCount);
        }

        string boxPath = @"c:\Users\m_del\Source\vibe_test\example_toolpaths\Box4mm.h";
        if (File.Exists(boxPath))
        {
            Toolpath toolpath = await ToolpathParser.ParseFileAsync(boxPath);
            Assert.Equal("Box4mm.h", toolpath.Name);
            Assert.Equal(5, toolpath.Segments.Count);
            Assert.Equal(4, toolpath.Statistics.CutSegmentsCount);
        }
    }
}
