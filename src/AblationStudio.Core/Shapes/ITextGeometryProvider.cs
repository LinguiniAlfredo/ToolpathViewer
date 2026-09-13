namespace AblationStudio.Core.Shapes;

public interface ITextGeometryProvider
{
    IReadOnlyList<PathContour> GenerateContours(
        string text,
        string fontFamily,
        float fontSizeMm,
        bool isBold,
        bool isItalic,
        float letterSpacingMm = 0f,
        float toleranceMm = 0.025f);
}
