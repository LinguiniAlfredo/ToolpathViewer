using System.Text.Json;
using AblationStudio.Core.Models;
using AblationStudio.Core.Projects;
using AblationStudio.Core.Shapes;

namespace AblationStudio.Tests;

public class ImageShapeTests
{
    [Fact]
    public void ImageShape_Defaults_AreValid()
    {
        var shape = new ImageShape();
        Assert.Equal("Image", shape.ShapeType);
        Assert.True(shape.IsClosed);
        Assert.Equal(ImageRasterMode.GrayscaleDensity, shape.RasterMode);
        Assert.True(shape.LockAspectRatio);
        Assert.Equal(0.2f, shape.Stepover);
        Assert.Equal(0.5f, shape.BrightnessThreshold);
        Assert.False(shape.InvertImage);
        Assert.True(shape.BidirectionalScan);
        Assert.True(shape.ShowOverlay);
        Assert.Equal(0.4f, shape.OverlayOpacity);
    }

    [Fact]
    public void Raster_AllWhite_EmitsNoCutSegments()
    {
        // 4x4 all white (255) pixels
        int w = 4, h = 4;
        byte[] pixels = new byte[w * h];
        Array.Fill(pixels, (byte)255);

        var shape = new ImageShape(0, 0, 0, 10, 10, pixels, w, h)
        {
            RasterMode = ImageRasterMode.Threshold,
            BrightnessThreshold = 0.5f,
            InvertImage = false
        };

        var segments = shape.GenerateSegments().ToList();
        var cutSegments = segments.Where(s => s.Type == SegmentType.Cut).ToList();

        Assert.Empty(cutSegments);
    }

    [Fact]
    public void Raster_AllBlack_EmitsFullCutSegments()
    {
        // 4x4 all black (0) pixels
        int w = 4, h = 4;
        byte[] pixels = new byte[w * h];
        Array.Fill(pixels, (byte)0);

        var shape = new ImageShape(0, 0, 0, 10, 10, pixels, w, h)
        {
            RasterMode = ImageRasterMode.Threshold,
            BrightnessThreshold = 0.5f,
            InvertImage = false,
            Stepover = 2.5f // 4 scanlines across 10mm
        };

        var segments = shape.GenerateSegments().ToList();
        var cutSegments = segments.Where(s => s.Type == SegmentType.Cut).ToList();

        Assert.NotEmpty(cutSegments);
        float totalCutLength = cutSegments.Sum(s => s.Length);
        // Each of the 4 scanlines should span the full 10mm width -> total ~40mm
        Assert.True(totalCutLength >= 35.0f);
    }

    [Fact]
    public void Raster_Invert_SwapsMarkAndSpace()
    {
        int w = 4, h = 4;
        byte[] pixels = new byte[w * h];
        Array.Fill(pixels, (byte)255); // All white

        var shape = new ImageShape(0, 0, 0, 10, 10, pixels, w, h)
        {
            RasterMode = ImageRasterMode.Threshold,
            BrightnessThreshold = 0.5f,
            InvertImage = true, // Inverted -> white should mark!
            Stepover = 2.5f
        };

        var segments = shape.GenerateSegments().ToList();
        var cutSegments = segments.Where(s => s.Type == SegmentType.Cut).ToList();

        Assert.NotEmpty(cutSegments);
    }

    [Fact]
    public void Raster_GrayscaleGradient_VaryingDensity()
    {
        // 2-pixel row: left pixel is light gray (200), right pixel is black (0)
        int w = 2, h = 1;
        byte[] pixels = [200, 0];

        var shape = new ImageShape(0, 0, 0, 10, 5, pixels, w, h)
        {
            RasterMode = ImageRasterMode.GrayscaleDensity,
            Stepover = 5.0f,
            InvertImage = false,
            BidirectionalScan = false
        };

        var segments = shape.GenerateSegments().ToList();
        var cuts = segments.Where(s => s.Type == SegmentType.Cut).ToList();

        // There should be cuts generated for the dark cell (and possibly small/none for very light)
        Assert.NotEmpty(cuts);
    }

    [Fact]
    public void Raster_Stepover_ControlsLineCount()
    {
        int w = 4, h = 4;
        byte[] pixels = new byte[w * h]; // black

        var shapeSparse = new ImageShape(0, 0, 0, 10, 10, pixels, w, h)
        {
            RasterMode = ImageRasterMode.Threshold,
            Stepover = 5.0f // 2 scanlines
        };

        var shapeDense = new ImageShape(0, 0, 0, 10, 10, pixels, w, h)
        {
            RasterMode = ImageRasterMode.Threshold,
            Stepover = 2.5f // 4 scanlines
        };

        var cutsSparse = shapeSparse.GenerateSegments().Where(s => s.Type == SegmentType.Cut).ToList();
        var cutsDense = shapeDense.GenerateSegments().Where(s => s.Type == SegmentType.Cut).ToList();

        Assert.True(cutsDense.Count > cutsSparse.Count);
    }

    [Fact]
    public void ImageShape_Clone_PreservesProperties()
    {
        byte[] pixels = [10, 20, 30, 40];
        byte[] rawBytes = [1, 2, 3];
        var original = new ImageShape(5, 10, 2, 25, 30, pixels, 2, 2, rawBytes, "sample.png", 45f)
        {
            RasterMode = ImageRasterMode.Threshold,
            Stepover = 0.5f,
            BrightnessThreshold = 0.7f,
            InvertImage = true,
            BidirectionalScan = false,
            OverlayOpacity = 0.8f,
            ShowOverlay = false
        };

        var clone = (ImageShape)original.Clone();

        Assert.Equal(original.PositionX, clone.PositionX);
        Assert.Equal(original.PositionY, clone.PositionY);
        Assert.Equal(original.PositionZ, clone.PositionZ);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.Height, clone.Height);
        Assert.Equal(original.PixelWidth, clone.PixelWidth);
        Assert.Equal(original.PixelHeight, clone.PixelHeight);
        Assert.Equal(original.RotationDegrees, clone.RotationDegrees);
        Assert.Equal(original.RasterMode, clone.RasterMode);
        Assert.Equal(original.Stepover, clone.Stepover);
        Assert.Equal(original.BrightnessThreshold, clone.BrightnessThreshold);
        Assert.Equal(original.InvertImage, clone.InvertImage);
        Assert.Equal(original.BidirectionalScan, clone.BidirectionalScan);
        Assert.Equal(original.OverlayOpacity, clone.OverlayOpacity);
        Assert.Equal(original.ShowOverlay, clone.ShowOverlay);
        Assert.Equal(original.SourceFileName, clone.SourceFileName);
        Assert.Equal(original.PixelData, clone.PixelData);
        Assert.Equal(original.RawImageData, clone.RawImageData);
    }

    [Fact]
    public void ImageShape_HitTest_AccurateWithinBounds()
    {
        var shape = new ImageShape(10, 20, 0, 10, 10, [0], 1, 1);

        // Center hit
        Assert.True(shape.HitTest(10, 20, 0.5f));
        // Corner hit
        Assert.True(shape.HitTest(14, 24, 0.5f));
        // Outside hit
        Assert.False(shape.HitTest(30, 50, 0.5f));
    }

    [Fact]
    public void ImageShape_Serialization_RoundTrip()
    {
        byte[] pixels = [50, 100, 150, 200];
        var original = new ImageShape(1, 2, 3, 15, 20, pixels, 2, 2, [9, 8, 7], "test.png", 15f)
        {
            RasterMode = ImageRasterMode.Threshold,
            Stepover = 0.3f,
            BrightnessThreshold = 0.65f,
            InvertImage = true,
            BidirectionalScan = false,
            OverlayOpacity = 0.55f,
            ShowOverlay = true
        };

        ShapeDto dto = ShapeDto.FromShape(original);
        Assert.IsType<ImageShapeDto>(dto);

        string json = JsonSerializer.Serialize<ShapeDto>(dto);
        ShapeDto? deserializedDto = JsonSerializer.Deserialize<ShapeDto>(json);
        Assert.NotNull(deserializedDto);
        Assert.IsType<ImageShapeDto>(deserializedDto);

        var reconstructed = (ImageShape)deserializedDto.ToShape();

        Assert.Equal(original.PositionX, reconstructed.PositionX);
        Assert.Equal(original.PositionY, reconstructed.PositionY);
        Assert.Equal(original.PositionZ, reconstructed.PositionZ);
        Assert.Equal(original.Width, reconstructed.Width);
        Assert.Equal(original.Height, reconstructed.Height);
        Assert.Equal(original.PixelWidth, reconstructed.PixelWidth);
        Assert.Equal(original.PixelHeight, reconstructed.PixelHeight);
        Assert.Equal(original.RotationDegrees, reconstructed.RotationDegrees);
        Assert.Equal(original.RasterMode, reconstructed.RasterMode);
        Assert.Equal(original.Stepover, reconstructed.Stepover);
        Assert.Equal(original.BrightnessThreshold, reconstructed.BrightnessThreshold);
        Assert.Equal(original.InvertImage, reconstructed.InvertImage);
        Assert.Equal(original.BidirectionalScan, reconstructed.BidirectionalScan);
        Assert.Equal(original.OverlayOpacity, reconstructed.OverlayOpacity);
        Assert.Equal(original.ShowOverlay, reconstructed.ShowOverlay);
        Assert.Equal(original.PixelData, reconstructed.PixelData);
        Assert.Equal(original.RawImageData, reconstructed.RawImageData);
    }
}
