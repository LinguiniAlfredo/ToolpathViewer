using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes.Hatching;

public static class HatchEngine
{
    public static List<ToolpathSegment> GenerateHatch(
        ToolpathShape shape,
        HatchSettings settings,
        ref ToolpathPoint? currentPosition)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled || settings.Pattern == HatchPatternType.None || !shape.IsClosed)
        {
            return [];
        }

        return settings.Pattern switch
        {
            HatchPatternType.ZigZag => ZigZagHatchGenerator.Generate(shape, settings, ref currentPosition),
            HatchPatternType.Spiral => SpiralHatchGenerator.Generate(shape, settings, ref currentPosition),
            HatchPatternType.FollowProfile => FollowProfileGenerator.Generate(shape, settings, ref currentPosition),
            _ => []
        };
    }
}
