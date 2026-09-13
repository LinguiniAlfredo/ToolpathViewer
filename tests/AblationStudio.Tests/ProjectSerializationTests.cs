using System.IO;
using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using AblationStudio.Core.Projects;
using AblationStudio.Core.Shapes;
using Xunit;

namespace AblationStudio.Tests;

public sealed class ProjectSerializationTests
{
    [Fact]
    public void Project_SerializeAndDeserialize_PreservesAllShapePropertiesAndHatchSettings()
    {
        var doc = new ShapeDocument();

        // 1. Circle with ZigZag Hatch
        var circle = new CircleShape(10f, 20f, 0f, 15f, segments: 48)
        {
            Name = "TestCircle",
            LayerId = 2,
            CutType = SegmentType.Cut
        };
        circle.Hatch.IsEnabled = true;
        circle.Hatch.Pattern = HatchPatternType.ZigZag;
        circle.Hatch.Stepover = 0.25f;
        circle.Hatch.AngleDegrees = 45f;
        circle.Hatch.CrossHatch = true;
        circle.Hatch.KeepBoundary = true;
        circle.Hatch.LineSkip = 3;
        circle.Hatch.AutoLineSkip = false;
        doc.AddShape(circle);

        // 2. Rectangle with Spiral Hatch
        var rect = new RectangleShape(50f, 60f, 1f, 30f, 20f, rotation: 15f)
        {
            Name = "TestRect",
            LayerId = 1,
            CutType = SegmentType.Cut
        };
        rect.Hatch.IsEnabled = true;
        rect.Hatch.Pattern = HatchPatternType.Spiral;
        rect.Hatch.Stepover = 0.5f;
        rect.Hatch.KeepBoundary = false;
        rect.Hatch.SpiralInward = true;
        doc.AddShape(rect);

        // 3. Polygon with FollowProfile Hatch
        var poly = new PolygonShape(-20f, -30f, 0f, 12f, sides: 6, rotation: 30f)
        {
            Name = "TestHexagon",
            LayerId = 3,
            CutType = SegmentType.Cut
        };
        poly.Hatch.IsEnabled = true;
        poly.Hatch.Pattern = HatchPatternType.FollowProfile;
        poly.Hatch.Stepover = 0.4f;
        poly.Hatch.FollowProfileOutward = true;
        poly.Hatch.LineSkip = 2;
        doc.AddShape(poly);

        // 4. Line
        var line = new LineShape(0f, 0f, 0f, 100f, 200f, 5f)
        {
            Name = "TestLine",
            LayerId = 1,
            CutType = SegmentType.Rapid
        };
        doc.AddShape(line);

        // Convert to project
        ToolpathProject originalProject = doc.ToProject("ComprehensiveProject");
        originalProject.Description = "Unit test ablation project description.";
        originalProject.Settings.DefaultFeedrate = 120f;
        originalProject.Settings.Units = "mm";

        // Serialize to JSON
        string json = ProjectSerializer.SerializeToJson(originalProject);
        Assert.False(string.IsNullOrWhiteSpace(json));

        // Deserialize from JSON
        ToolpathProject deserializedProject = ProjectSerializer.DeserializeFromJson(json);

        Assert.Equal("1.0", deserializedProject.SchemaVersion);
        Assert.Equal("ComprehensiveProject", deserializedProject.Name);
        Assert.Equal("Unit test ablation project description.", deserializedProject.Description);
        Assert.Equal(120f, deserializedProject.Settings.DefaultFeedrate);
        Assert.Equal(4, deserializedProject.Shapes.Count);

        // Restore into new ShapeDocument
        var restoredDoc = new ShapeDocument();
        restoredDoc.LoadFromProject(deserializedProject);

        Assert.Equal(4, restoredDoc.Shapes.Count);

        // Verify Circle
        var restoredCircle = Assert.IsType<CircleShape>(restoredDoc.Shapes[0]);
        Assert.Equal("TestCircle", restoredCircle.Name);
        Assert.Equal(10f, restoredCircle.PositionX, precision: 3);
        Assert.Equal(20f, restoredCircle.PositionY, precision: 3);
        Assert.Equal(0f, restoredCircle.PositionZ, precision: 3);
        Assert.Equal(15f, restoredCircle.Radius, precision: 3);
        Assert.Equal(48, restoredCircle.SegmentsCount);
        Assert.Equal(2, restoredCircle.LayerId);
        Assert.Equal(SegmentType.Cut, restoredCircle.CutType);
        Assert.True(restoredCircle.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.ZigZag, restoredCircle.Hatch.Pattern);
        Assert.Equal(0.25f, restoredCircle.Hatch.Stepover, precision: 3);
        Assert.Equal(45f, restoredCircle.Hatch.AngleDegrees, precision: 3);
        Assert.True(restoredCircle.Hatch.CrossHatch);
        Assert.True(restoredCircle.Hatch.KeepBoundary);
        Assert.Equal(3, restoredCircle.Hatch.LineSkip);
        Assert.False(restoredCircle.Hatch.AutoLineSkip);

        // Verify Rectangle
        var restoredRect = Assert.IsType<RectangleShape>(restoredDoc.Shapes[1]);
        Assert.Equal("TestRect", restoredRect.Name);
        Assert.Equal(50f, restoredRect.PositionX, precision: 3);
        Assert.Equal(60f, restoredRect.PositionY, precision: 3);
        Assert.Equal(1f, restoredRect.PositionZ, precision: 3);
        Assert.Equal(30f, restoredRect.Width, precision: 3);
        Assert.Equal(20f, restoredRect.Height, precision: 3);
        Assert.Equal(15f, restoredRect.RotationDegrees, precision: 3);
        Assert.True(restoredRect.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.Spiral, restoredRect.Hatch.Pattern);
        Assert.False(restoredRect.Hatch.KeepBoundary);
        Assert.True(restoredRect.Hatch.SpiralInward);

        // Verify Polygon
        var restoredPoly = Assert.IsType<PolygonShape>(restoredDoc.Shapes[2]);
        Assert.Equal("TestHexagon", restoredPoly.Name);
        Assert.Equal(-20f, restoredPoly.PositionX, precision: 3);
        Assert.Equal(-30f, restoredPoly.PositionY, precision: 3);
        Assert.Equal(12f, restoredPoly.Radius, precision: 3);
        Assert.Equal(6, restoredPoly.Sides);
        Assert.Equal(30f, restoredPoly.RotationDegrees, precision: 3);
        Assert.True(restoredPoly.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.FollowProfile, restoredPoly.Hatch.Pattern);
        Assert.True(restoredPoly.Hatch.FollowProfileOutward);
        Assert.Equal(2, restoredPoly.Hatch.LineSkip);

        // Verify Line
        var restoredLine = Assert.IsType<LineShape>(restoredDoc.Shapes[3]);
        Assert.Equal("TestLine", restoredLine.Name);
        Assert.Equal(0f, restoredLine.PositionX, precision: 3);
        Assert.Equal(100f, restoredLine.EndX, precision: 3);
        Assert.Equal(200f, restoredLine.EndY, precision: 3);
        Assert.Equal(5f, restoredLine.EndZ, precision: 3);
        Assert.Equal(SegmentType.Rapid, restoredLine.CutType);
    }

    [Fact]
    public void ToolpathProject_ProjectExtension_IsAbs()
    {
        Assert.Equal(".abs", ToolpathProject.ProjectExtension);
        Assert.Equal("Ablation Studio Project (*.abs)|*.abs|All Files (*.*)|*.*", ToolpathProject.ProjectFileFilter);
    }

    [Fact]
    public async Task ProjectSerializer_SaveAndLoadFileAsync_RoundTripsSuccessfully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ablation_test_{Guid.NewGuid():N}.abs");

        try
        {
            var project = new ToolpathProject
            {
                Name = "TempProject",
                Description = "Async file I/O test"
            };
            project.Shapes.Add(new CircleShapeDto { Name = "C1", Radius = 8.5f, PositionX = 5f, PositionY = 10f });

            await ProjectSerializer.SaveProjectAsync(project, tempFile);

            Assert.True(File.Exists(tempFile));

            ToolpathProject loaded = await ProjectSerializer.LoadProjectAsync(tempFile);

            Assert.Equal("TempProject", loaded.Name);
            Assert.Equal("Async file I/O test", loaded.Description);
            Assert.Single(loaded.Shapes);
            var circle = Assert.IsType<CircleShapeDto>(loaded.Shapes[0]);
            Assert.Equal(8.5f, circle.Radius, precision: 3);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ExportToHCode_ProducesCleanMachineCode_WithoutMetadataComments()
    {
        var doc = new ShapeDocument();
        doc.AddShape(new CircleShape(15f, 25f, 0f, 10f, segments: 32) { Name = "LaserCircle" });
        doc.AddShape(new RectangleShape(0f, 0f, 0f, 20f, 10f) { Name = "LaserRect" });

        // Export clean machine instructions
        string hCode = doc.ExportToHCode("MachineJob.h");

        // 1. Validate that NO project metadata comments are embedded
        Assert.DoesNotContain("ABLATION_STUDIO_PROJECT_BEGIN", hCode);
        Assert.DoesNotContain("ABLATION_STUDIO_PROJECT_END", hCode);

        // 2. Validate that standard NC commands are present
        Assert.Contains("PFL 1 ; Profile", hCode);
        Assert.Contains("SL X", hCode);

        // 3. Verify that ToolpathParser parses the file without errors
        Toolpath parsedToolpath = ToolpathParser.ParseText(hCode, "MachineJob.h");
        Assert.True(parsedToolpath.Segments.Count > 0);
        Assert.True(parsedToolpath.Statistics.CutSegmentsCount > 0);
    }

    [Fact]
    public void ProjectSerializer_EmbedAndExtractMetadata_RoundTripsSuccessfully()
    {
        var doc = new ShapeDocument();
        doc.AddShape(new CircleShape(15f, 25f, 0f, 10f, segments: 32) { Name = "LaserCircle" });
        doc.AddShape(new RectangleShape(0f, 0f, 0f, 20f, 10f) { Name = "LaserRect" });

        string rawHCode = doc.ExportToHCode("MachineJob.h");
        ToolpathProject proj = doc.ToProject("MachineJob");

        // Embed metadata manually via serializer
        string embeddedHCode = ProjectSerializer.EmbedMetadataInHCode(rawHCode, proj);
        Assert.Contains("; ABLATION_STUDIO_PROJECT_BEGIN", embeddedHCode);
        Assert.Contains("; ABLATION_STUDIO_PROJECT_END", embeddedHCode);

        // Extract metadata
        bool extracted = ProjectSerializer.TryExtractMetadataFromHCode(embeddedHCode, out ToolpathProject? recoveredProject);
        Assert.True(extracted);
        Assert.NotNull(recoveredProject);
        Assert.Equal(2, recoveredProject.Shapes.Count);

        var shapeDoc2 = new ShapeDocument();
        shapeDoc2.LoadFromProject(recoveredProject);
        Assert.Equal(2, shapeDoc2.Shapes.Count);
        Assert.IsType<CircleShape>(shapeDoc2.Shapes[0]);
        Assert.IsType<RectangleShape>(shapeDoc2.Shapes[1]);
        Assert.Equal("LaserCircle", shapeDoc2.Shapes[0].Name);
        Assert.Equal("LaserRect", shapeDoc2.Shapes[1].Name);
    }

    [Fact]
    public void TryExtractMetadataFromHCode_RawFileWithoutMetadata_ReturnsFalseGracefully()
    {
        string rawHCode = @"
; Raw third-party toolpath
HCH 1 1 ;Layer 1
SL X0.0000 Y0.0000 Z0.0000 M05
SL X10.0000 Y10.0000 Z0.0000 M03
SL X0.0000 Y0.0000 Z0.0000 M05
";
        bool extracted = ProjectSerializer.TryExtractMetadataFromHCode(rawHCode, out ToolpathProject? project);
        Assert.False(extracted);
        Assert.Null(project);
    }

    [Fact]
    public void EmptyProject_RoundTripsCorrectly()
    {
        var doc = new ShapeDocument();
        ToolpathProject emptyProj = doc.ToProject("EmptyProject");
        string json = ProjectSerializer.SerializeToJson(emptyProj);

        ToolpathProject loaded = ProjectSerializer.DeserializeFromJson(json);
        Assert.Equal("EmptyProject", loaded.Name);
        Assert.Empty(loaded.Shapes);

        var doc2 = new ShapeDocument();
        doc2.LoadFromProject(loaded);
        Assert.Empty(doc2.Shapes);
    }

    [Fact]
    public void TryExtractMetadataFromHCode_CorruptBase64OrJson_ReturnsFalseGracefully()
    {
        string malformed = @"
; ABLATION_STUDIO_PROJECT_BEGIN
; NOT_VALID_BASE64_!@#$%^&*()
; ABLATION_STUDIO_PROJECT_END
SL X0 Y0 Z0 M05
";
        bool extracted = ProjectSerializer.TryExtractMetadataFromHCode(malformed, out ToolpathProject? project);
        Assert.False(extracted);
        Assert.Null(project);
    }
}
