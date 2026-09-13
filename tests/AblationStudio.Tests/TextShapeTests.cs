using AblationStudio.App.Services;
using AblationStudio.Core.Models;
using AblationStudio.Core.Projects;
using AblationStudio.Core.Shapes;
using AblationStudio.Core.Shapes.Hatching;

namespace AblationStudio.Tests;

public class TextShapeTests
{
    public TextShapeTests()
    {
        TextShape.GeometryProvider = new WpfTextGeometryProvider();
    }

    [Fact]
    public void TextShape_GeneratesContours_ForStandardCharactersWithHoles()
    {
        // 'O' in Arial has 2 contours: outer perimeter and inner hole
        var text = new TextShape(0, 0, 0, "O", "Arial", 10.0f);

        Assert.Equal("Text", text.ShapeType);
        Assert.True(text.IsClosed);
        Assert.Equal(2, text.ContoursCount);
        Assert.True(text.TotalPointsCount > 10);
        Assert.True(text.TotalPerimeterLength > 10.0f);

        BoundingBox3D bounds = text.GetBounds();
        Assert.True(bounds.SizeY > 5.0f && bounds.SizeY < 15.0f);
        Assert.True(bounds.SizeX > 5.0f && bounds.SizeX < 15.0f);
    }

    [Fact]
    public void TextShape_ProfileMarking_TracesAllContoursWithRapidTransitions()
    {
        // "AB" has 4 contours: A outer, A inner hole, B outer, B upper hole, B lower hole = 5 contours
        var text = new TextShape(0, 0, 0, "AB", "Arial", 10.0f)
        {
            CutType = SegmentType.Cut,
            LayerId = 2
        };
        text.Hatch.IsEnabled = false;

        List<ToolpathSegment> segments = text.GenerateSegments().ToList();

        Assert.NotEmpty(segments);
        Assert.Contains(segments, s => s.Type == SegmentType.Cut);
        Assert.Contains(segments, s => s.Type == SegmentType.Rapid);
        Assert.All(segments.Where(s => s.Type == SegmentType.Cut), s => Assert.Equal(2, s.LayerId));

        // Total profile length should match sum of contour perimeters
        float cutLength = segments.Where(s => s.Type == SegmentType.Cut).Sum(s => s.Length);
        Assert.InRange(cutLength, text.TotalPerimeterLength * 0.95f, text.TotalPerimeterLength * 1.05f);
    }

    [Fact]
    public void TextShape_WithZigZagHatch_GeneratesInfill_OmittingCounterSpaces()
    {
        var text = new TextShape(0, 0, 0, "O", "Arial", 10.0f);
        text.Hatch.IsEnabled = true;
        text.Hatch.Pattern = HatchPatternType.ZigZag;
        text.Hatch.Stepover = 0.5f;
        text.Hatch.KeepBoundary = true;

        List<ToolpathSegment> segments = text.GenerateSegments().ToList();

        List<ToolpathSegment> hatchMoves = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchMoves);

        // Verify midpoints of all hatch segments are inside the character loops
        List<List<HatchGeometry.Point2D>> loops = HatchGeometry.GetPolygonLoops(text);
        foreach (ToolpathSegment seg in hatchMoves)
        {
            var mid = new HatchGeometry.Point2D((seg.Start.X + seg.End.X) * 0.5f, (seg.Start.Y + seg.End.Y) * 0.5f);
            Assert.True(HatchGeometry.IsPointInLoops(mid, loops));
        }
    }

    [Fact]
    public void TextShape_WithFollowProfile_GeneratesConcentricInfillRings()
    {
        var text = new TextShape(0, 0, 0, "C", "Arial", 10.0f);
        text.Hatch.IsEnabled = true;
        text.Hatch.Pattern = HatchPatternType.FollowProfile;
        text.Hatch.Stepover = 0.4f;

        List<ToolpathSegment> segments = text.GenerateSegments().ToList();

        List<ToolpathSegment> hatchMoves = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchMoves);
    }

    [Fact]
    public void TextShape_WithSpiralHatch_ClipsArchimedeanSpiralToGlyphs()
    {
        var text = new TextShape(0, 0, 0, "O", "Arial", 10.0f);
        text.Hatch.IsEnabled = true;
        text.Hatch.Pattern = HatchPatternType.Spiral;
        text.Hatch.Stepover = 0.4f;

        List<ToolpathSegment> segments = text.GenerateSegments().ToList();

        List<ToolpathSegment> hatchMoves = segments.Where(s => s.Type == SegmentType.Hatch).ToList();
        Assert.NotEmpty(hatchMoves);

        List<List<HatchGeometry.Point2D>> loops = HatchGeometry.GetPolygonLoops(text);
        foreach (ToolpathSegment seg in hatchMoves)
        {
            var mid = new HatchGeometry.Point2D((seg.Start.X + seg.End.X) * 0.5f, (seg.Start.Y + seg.End.Y) * 0.5f);
            Assert.True(HatchGeometry.IsPointInLoops(mid, loops));
        }
    }

    [Fact]
    public void TextShape_KeepBoundary_TogglesProfilePass()
    {
        var text = new TextShape(0, 0, 0, "E", "Arial", 10.0f);
        text.Hatch.IsEnabled = true;
        text.Hatch.Pattern = HatchPatternType.ZigZag;
        text.Hatch.Stepover = 0.5f;

        // KeepBoundary = true: includes Cut moves
        text.Hatch.KeepBoundary = true;
        List<ToolpathSegment> segsWithBoundary = text.GenerateSegments().ToList();
        Assert.Contains(segsWithBoundary, s => s.Type == SegmentType.Cut);
        Assert.Contains(segsWithBoundary, s => s.Type == SegmentType.Hatch);

        // KeepBoundary = false: NO Cut moves (hatch only)
        text.Hatch.KeepBoundary = false;
        List<ToolpathSegment> segsNoBoundary = text.GenerateSegments().ToList();
        Assert.DoesNotContain(segsNoBoundary, s => s.Type == SegmentType.Cut);
        Assert.Contains(segsNoBoundary, s => s.Type == SegmentType.Hatch);
    }

    [Fact]
    public void TextShape_HitTest_DetectsClicksOnCharactersAndHoles()
    {
        var text = new TextShape(0, 0, 0, "O", "Arial", 10.0f);
        BoundingBox3D bounds = text.GetBounds();

        // Far outside -> false
        Assert.False(text.HitTest(100f, 100f, 0.5f));

        // Click right on top of outer border of the 'O' -> true
        Assert.True(text.HitTest(0f, bounds.MaxY, 0.5f));

        // Click on the left side of the 'O' stroke -> true
        Assert.True(text.HitTest(bounds.MinX + 0.5f, 0f, 0.5f));

        // Center hole of 'O' with very small tolerance (0.01mm) -> false (point is in the void)
        Assert.False(text.HitTest(0f, 0f, 0.01f));
    }

    [Fact]
    public void TextShape_Transformation_TranslateAndScale()
    {
        var text = new TextShape(0, 0, 0, "T", "Arial", 10.0f);
        BoundingBox3D initialBounds = text.GetBounds();

        // Translate
        text.Translate(20f, -10f, 2f);
        Assert.Equal(20f, text.PositionX);
        Assert.Equal(-10f, text.PositionY);
        Assert.Equal(2f, text.PositionZ);

        BoundingBox3D movedBounds = text.GetBounds();
        Assert.Equal(initialBounds.MinX + 20f, movedBounds.MinX, 2);
        Assert.Equal(initialBounds.MinY - 10f, movedBounds.MinY, 2);

        // Scale
        text.Scale(1.5f, text.PositionX, text.PositionY);
        Assert.Equal(15.0f, text.FontSize, 2);
        BoundingBox3D scaledBounds = text.GetBounds();
        Assert.Equal(initialBounds.SizeY * 1.5f, scaledBounds.SizeY, 1);
    }

    [Fact]
    public void TextShape_ProjectSerialization_RoundTripsJson()
    {
        var doc = new ShapeDocument();
        var originalText = new TextShape(10f, 20f, 1.5f, "Ablation", "Arial", 12.5f, isBold: true, isItalic: true, letterSpacing: 0.5f, rotationDegrees: 45f)
        {
            Name = "LogoText",
            LayerId = 3,
            CutType = SegmentType.Cut
        };
        originalText.Hatch.IsEnabled = true;
        originalText.Hatch.Pattern = HatchPatternType.ZigZag;
        originalText.Hatch.Stepover = 0.35f;
        originalText.Hatch.CrossHatch = true;

        doc.AddShape(originalText);

        ToolpathProject project = doc.ToProject("SerializationTest");
        string json = ProjectSerializer.SerializeToJson(project);

        Assert.Contains("\"$type\": \"text\"", json);
        Assert.Contains("\"text\": \"Ablation\"", json);
        Assert.Contains("\"isBold\": true", json);

        ToolpathProject restoredProject = ProjectSerializer.DeserializeFromJson(json);
        var restoredDoc = new ShapeDocument();
        restoredDoc.LoadFromProject(restoredProject);

        Assert.Single(restoredDoc.Shapes);
        TextShape restoredShape = Assert.IsType<TextShape>(restoredDoc.Shapes[0]);

        Assert.Equal("LogoText", restoredShape.Name);
        Assert.Equal("Ablation", restoredShape.Text);
        Assert.Equal("Arial", restoredShape.FontFamily);
        Assert.Equal(12.5f, restoredShape.FontSize);
        Assert.True(restoredShape.IsBold);
        Assert.True(restoredShape.IsItalic);
        Assert.Equal(0.5f, restoredShape.LetterSpacing);
        Assert.Equal(45f, restoredShape.RotationDegrees);
        Assert.Equal(10f, restoredShape.PositionX);
        Assert.Equal(20f, restoredShape.PositionY);
        Assert.Equal(1.5f, restoredShape.PositionZ);
        Assert.Equal(3, restoredShape.LayerId);

        Assert.True(restoredShape.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.ZigZag, restoredShape.Hatch.Pattern);
        Assert.Equal(0.35f, restoredShape.Hatch.Stepover);
        Assert.True(restoredShape.Hatch.CrossHatch);
        Assert.True(restoredShape.ContoursCount > 0);
    }
}
