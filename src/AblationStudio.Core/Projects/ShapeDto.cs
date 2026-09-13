using System.Text.Json.Serialization;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.Projects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CircleShapeDto), "circle")]
[JsonDerivedType(typeof(RectangleShapeDto), "rectangle")]
[JsonDerivedType(typeof(PolygonShapeDto), "polygon")]
[JsonDerivedType(typeof(LineShapeDto), "line")]
[JsonDerivedType(typeof(PathShapeDto), "path")]
[JsonDerivedType(typeof(TextShapeDto), "text")]
public abstract class ShapeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int LayerId { get; set; } = 1;
    public SegmentType CutType { get; set; } = SegmentType.Cut;
    public HatchSettingsDto Hatch { get; set; } = new();

    public abstract ToolpathShape ToShape();

    public static ShapeDto FromShape(ToolpathShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        ShapeDto dto = shape switch
        {
            CircleShape circle => new CircleShapeDto
            {
                Radius = circle.Radius,
                SegmentsCount = circle.SegmentsCount
            },
            RectangleShape rect => new RectangleShapeDto
            {
                Width = rect.Width,
                Height = rect.Height,
                RotationDegrees = rect.RotationDegrees
            },
            PolygonShape poly => new PolygonShapeDto
            {
                Radius = poly.Radius,
                Sides = poly.Sides,
                RotationDegrees = poly.RotationDegrees
            },
            LineShape line => new LineShapeDto
            {
                EndX = line.EndX,
                EndY = line.EndY,
                EndZ = line.EndZ
            },
            PathShape path => new PathShapeDto
            {
                Contours = path.Contours.Select(c => new PathContourDto
                {
                    LocalPoints = [.. c.LocalPoints],
                    IsClosed = c.IsClosed
                }).ToList()
            },
            TextShape text => new TextShapeDto
            {
                Text = text.Text,
                FontFamily = text.FontFamily,
                FontSize = text.FontSize,
                IsBold = text.IsBold,
                IsItalic = text.IsItalic,
                LetterSpacing = text.LetterSpacing,
                RotationDegrees = text.RotationDegrees
            },
            _ => throw new NotSupportedException($"Shape type '{shape.GetType().Name}' is not supported for serialization.")
        };

        dto.Id = shape.Id;
        dto.Name = shape.Name;
        dto.PositionX = shape.PositionX;
        dto.PositionY = shape.PositionY;
        dto.PositionZ = shape.PositionZ;
        dto.LayerId = shape.LayerId;
        dto.CutType = shape.CutType;
        dto.Hatch = HatchSettingsDto.FromSettings(shape.Hatch);

        return dto;
    }

    protected void PopulateCommonProperties(ToolpathShape shape)
    {
        shape.Name = Name;
        shape.PositionX = PositionX;
        shape.PositionY = PositionY;
        shape.PositionZ = PositionZ;
        shape.LayerId = LayerId;
        shape.CutType = CutType;
        Hatch.ApplyTo(shape.Hatch);
    }
}

public sealed class CircleShapeDto : ShapeDto
{
    public float Radius { get; set; } = 5.0f;
    public int SegmentsCount { get; set; } = 64;

    public override ToolpathShape ToShape()
    {
        var circle = new CircleShape(PositionX, PositionY, PositionZ, Radius, SegmentsCount);
        PopulateCommonProperties(circle);
        return circle;
    }
}

public sealed class RectangleShapeDto : ShapeDto
{
    public float Width { get; set; } = 10.0f;
    public float Height { get; set; } = 8.0f;
    public float RotationDegrees { get; set; }

    public override ToolpathShape ToShape()
    {
        var rect = new RectangleShape(PositionX, PositionY, PositionZ, Width, Height, RotationDegrees);
        PopulateCommonProperties(rect);
        return rect;
    }
}

public sealed class PolygonShapeDto : ShapeDto
{
    public float Radius { get; set; } = 6.0f;
    public int Sides { get; set; } = 5;
    public float RotationDegrees { get; set; }

    public override ToolpathShape ToShape()
    {
        var poly = new PolygonShape(PositionX, PositionY, PositionZ, Radius, Sides, RotationDegrees);
        PopulateCommonProperties(poly);
        return poly;
    }
}

public sealed class LineShapeDto : ShapeDto
{
    public float EndX { get; set; }
    public float EndY { get; set; }
    public float EndZ { get; set; }

    public override ToolpathShape ToShape()
    {
        var line = new LineShape(PositionX, PositionY, PositionZ, EndX, EndY, EndZ);
        PopulateCommonProperties(line);
        return line;
    }
}

public sealed class PathShapeDto : ShapeDto
{
    public List<PathContourDto> Contours { get; set; } = [];

    public override ToolpathShape ToShape()
    {
        var contours = Contours.Select(c => new PathContour(c.LocalPoints, c.IsClosed));
        var path = new PathShape(PositionX, PositionY, PositionZ, contours);
        PopulateCommonProperties(path);
        return path;
    }
}

public sealed class TextShapeDto : ShapeDto
{
    public string Text { get; set; } = "TEXT";
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 10.0f;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public float LetterSpacing { get; set; }
    public float RotationDegrees { get; set; }

    public override ToolpathShape ToShape()
    {
        var textShape = new TextShape(
            PositionX, PositionY, PositionZ,
            Text, FontFamily, FontSize, IsBold, IsItalic, LetterSpacing, RotationDegrees);
        PopulateCommonProperties(textShape);
        return textShape;
    }
}

public sealed class PathContourDto
{
    public List<ToolpathPoint> LocalPoints { get; set; } = [];
    public bool IsClosed { get; set; } = true;
}

public sealed class HatchSettingsDto
{
    public bool IsEnabled { get; set; }
    public HatchPatternType Pattern { get; set; } = HatchPatternType.ZigZag;
    public float Stepover { get; set; } = 0.5f;
    public float AngleDegrees { get; set; }
    public bool CrossHatch { get; set; }
    public bool KeepBoundary { get; set; } = true;
    public int LineSkip { get; set; } = 1;
    public bool AutoLineSkip { get; set; }
    public bool SpiralInward { get; set; }
    public bool FollowProfileOutward { get; set; }

    public static HatchSettingsDto FromSettings(HatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new HatchSettingsDto
        {
            IsEnabled = settings.IsEnabled,
            Pattern = settings.Pattern,
            Stepover = settings.Stepover,
            AngleDegrees = settings.AngleDegrees,
            CrossHatch = settings.CrossHatch,
            KeepBoundary = settings.KeepBoundary,
            LineSkip = settings.LineSkip,
            AutoLineSkip = settings.AutoLineSkip,
            SpiralInward = settings.SpiralInward,
            FollowProfileOutward = settings.FollowProfileOutward
        };
    }

    public void ApplyTo(HatchSettings target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.IsEnabled = IsEnabled;
        target.Pattern = Pattern;
        target.Stepover = Stepover;
        target.AngleDegrees = AngleDegrees;
        target.CrossHatch = CrossHatch;
        target.KeepBoundary = KeepBoundary;
        target.LineSkip = LineSkip;
        target.AutoLineSkip = AutoLineSkip;
        target.SpiralInward = SpiralInward;
        target.FollowProfileOutward = FollowProfileOutward;
    }
}
