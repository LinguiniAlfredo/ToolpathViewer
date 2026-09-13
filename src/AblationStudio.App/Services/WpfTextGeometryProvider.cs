using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes;
using Clipper2Lib;

namespace AblationStudio.App.Services;

public sealed class WpfTextGeometryProvider : ITextGeometryProvider
{
    private const double MillimetersPerInch = 25.4;
    private const double DipsPerInch = 96.0;
    private const double DipsPerMm = DipsPerInch / MillimetersPerInch; // ~3.779527559

    public IReadOnlyList<PathContour> GenerateContours(
        string text,
        string fontFamily,
        float fontSizeMm,
        bool isBold,
        bool isItalic,
        float letterSpacingMm = 0f,
        float toleranceMm = 0.025f)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        double fontSizeDip = Math.Max(0.5, fontSizeMm * DipsPerMm);
        double toleranceDip = Math.Max(0.01, toleranceMm * DipsPerMm);

        var typeface = new Typeface(
            new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Arial" : fontFamily),
            isItalic ? FontStyles.Italic : FontStyles.Normal,
            isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);

        Geometry? geom;

        if (Math.Abs(letterSpacingMm) < 0.001f || text.Length <= 1)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSizeDip,
                Brushes.Black,
                pixelsPerDip: 1.0);

            geom = formattedText.BuildGeometry(new Point(0, 0));
        }
        else
        {
            var group = new GeometryGroup();
            double xOffset = 0;
            double spacingDip = letterSpacingMm * DipsPerMm;

            for (int i = 0; i < text.Length; i++)
            {
                string ch = text.Substring(i, 1);
                var chFormatted = new FormattedText(
                    ch,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSizeDip,
                    Brushes.Black,
                    pixelsPerDip: 1.0);

                Geometry chGeom = chFormatted.BuildGeometry(new Point(xOffset, 0));
                if (!chGeom.IsEmpty())
                {
                    group.Children.Add(chGeom);
                }

                xOffset += chFormatted.WidthIncludingTrailingWhitespace + spacingDip;
            }

            geom = group;
        }

        if (geom is null || geom.IsEmpty())
        {
            return [];
        }

        // Flatten Bezier curves to line segments with requested tolerance
        PathGeometry flattened = geom.GetFlattenedPathGeometry(toleranceDip, ToleranceType.Absolute);
        if (flattened is null || flattened.Figures.Count == 0)
        {
            return [];
        }

        // Convert WPF figures to raw 2D paths in millimeters, inverting Y so +Y is UP (Cartesian / CAD)
        var rawPaths = new PathsD();
        Rect wpfBounds = flattened.Bounds;
        double wpfCenterY = wpfBounds.Y + wpfBounds.Height * 0.5;
        double wpfCenterX = wpfBounds.X + wpfBounds.Width * 0.5;

        foreach (PathFigure figure in flattened.Figures)
        {
            var rawPath = new PathD();

            Point startPt = figure.StartPoint;
            double sx = (startPt.X - wpfCenterX) / DipsPerMm;
            double sy = -(startPt.Y - wpfCenterY) / DipsPerMm;
            rawPath.Add(new PointD(sx, sy));

            foreach (PathSegment seg in figure.Segments)
            {
                if (seg is PolyLineSegment poly)
                {
                    foreach (Point pt in poly.Points)
                    {
                        double px = (pt.X - wpfCenterX) / DipsPerMm;
                        double py = -(pt.Y - wpfCenterY) / DipsPerMm;
                        rawPath.Add(new PointD(px, py));
                    }
                }
                else if (seg is LineSegment line)
                {
                    double px = (line.Point.X - wpfCenterX) / DipsPerMm;
                    double py = -(line.Point.Y - wpfCenterY) / DipsPerMm;
                    rawPath.Add(new PointD(px, py));
                }
            }

            if (rawPath.Count >= 3)
            {
                rawPaths.Add(rawPath);
            }
        }

        if (rawPaths.Count == 0)
        {
            return [];
        }

        // Clean up self-intersections / overlapping characters with Clipper2 NonZero rule
        const int clipperPrecision = 6;
        PathsD cleanedPaths = Clipper.Union(rawPaths, new PathsD(), Clipper2Lib.FillRule.NonZero, clipperPrecision);
        if (cleanedPaths.Count == 0)
        {
            cleanedPaths = rawPaths;
        }

        // Convert back to PathContour objects
        var contours = new List<PathContour>(cleanedPaths.Count);
        foreach (PathD path in cleanedPaths)
        {
            int count = path.Count;
            if (count < 3)
            {
                continue;
            }

            var pts = new List<ToolpathPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var nextPt = new ToolpathPoint((float)path[i].x, (float)path[i].y, 0f);
                if (pts.Count == 0 || pts[^1].DistanceTo(nextPt) > 0.0005f)
                {
                    pts.Add(nextPt);
                }
            }

            if (pts.Count > 2 && pts[^1].DistanceTo(pts[0]) < 0.001f)
            {
                pts.RemoveAt(pts.Count - 1);
            }

            if (pts.Count >= 3)
            {
                contours.Add(new PathContour(pts, isClosed: true));
            }
        }

        return contours;
    }

    public static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        try
        {
            return Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name)
                .ToList();
        }
        catch
        {
            return ["Arial", "Segoe UI", "Times New Roman", "Consolas", "Verdana", "Tahoma", "Courier New"];
        }
    }
}
