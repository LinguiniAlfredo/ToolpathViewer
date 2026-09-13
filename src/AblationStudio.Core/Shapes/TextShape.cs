using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes.Hatching;

namespace AblationStudio.Core.Shapes;

public sealed class TextShape : ToolpathShape, IContourShape
{
    private string _text = "TEXT";
    private string _fontFamily = "Arial";
    private float _fontSize = 10.0f;
    private bool _isBold;
    private bool _isItalic;
    private float _letterSpacing;
    private float _rotationDegrees;

    private readonly List<PathContour> _baseContours = [];
    private readonly List<PathContour> _contours = [];

    public static ITextGeometryProvider? GeometryProvider { get; set; }

    public override string ShapeType => "Text";
    public override bool IsClosed => _contours.Count > 0 && _contours.Any(c => c.IsClosed);

    public IReadOnlyList<PathContour> Contours => _contours;

    public int ContoursCount => _contours.Count;

    public int TotalPointsCount
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _contours.Count; i++)
            {
                sum += _contours[i].PointsCount;
            }
            return sum;
        }
    }

    public float TotalPerimeterLength
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < _contours.Count; i++)
            {
                total += _contours[i].Length;
            }
            return total;
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                RebuildBaseContours();
            }
        }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (SetProperty(ref _fontFamily, string.IsNullOrWhiteSpace(value) ? "Arial" : value))
            {
                RebuildBaseContours();
            }
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            float val = MathF.Max(0.1f, value);
            if (SetProperty(ref _fontSize, val))
            {
                RebuildBaseContours();
            }
        }
    }

    public bool IsBold
    {
        get => _isBold;
        set
        {
            if (SetProperty(ref _isBold, value))
            {
                RebuildBaseContours();
            }
        }
    }

    public bool IsItalic
    {
        get => _isItalic;
        set
        {
            if (SetProperty(ref _isItalic, value))
            {
                RebuildBaseContours();
            }
        }
    }

    public float LetterSpacing
    {
        get => _letterSpacing;
        set
        {
            if (SetProperty(ref _letterSpacing, value))
            {
                RebuildBaseContours();
            }
        }
    }

    public float RotationDegrees
    {
        get => _rotationDegrees;
        set
        {
            float val = (value % 360f + 360f) % 360f;
            if (SetProperty(ref _rotationDegrees, val))
            {
                ApplyRotation();
            }
        }
    }

    public TextShape()
    {
        Name = "Text";
        RebuildBaseContours();
    }

    public TextShape(
        float centerX,
        float centerY,
        float centerZ,
        string text = "TEXT",
        string fontFamily = "Arial",
        float fontSize = 10.0f,
        bool isBold = false,
        bool isItalic = false,
        float letterSpacing = 0f,
        float rotationDegrees = 0f)
    {
        Name = "Text";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        _text = text ?? string.Empty;
        _fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Arial" : fontFamily;
        _fontSize = MathF.Max(0.1f, fontSize);
        _isBold = isBold;
        _isItalic = isItalic;
        _letterSpacing = letterSpacing;
        _rotationDegrees = (rotationDegrees % 360f + 360f) % 360f;

        RebuildBaseContours();
    }

    public void RebuildBaseContours()
    {
        _baseContours.Clear();

        if (GeometryProvider is not null && !string.IsNullOrEmpty(_text))
        {
            IReadOnlyList<PathContour> generated = GeometryProvider.GenerateContours(
                _text, _fontFamily, _fontSize, _isBold, _isItalic, _letterSpacing);

            foreach (PathContour c in generated)
            {
                _baseContours.Add(c.Clone());
            }
        }

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        _contours.Clear();

        float rad = _rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        foreach (PathContour baseContour in _baseContours)
        {
            var rotatedPts = new List<ToolpathPoint>(baseContour.PointsCount);
            foreach (ToolpathPoint p in baseContour.LocalPoints)
            {
                float rx = p.X * cos - p.Y * sin;
                float ry = p.X * sin + p.Y * cos;
                rotatedPts.Add(new ToolpathPoint(rx, ry, p.Z));
            }
            _contours.Add(new PathContour(rotatedPts, baseContour.IsClosed));
        }

        InvalidateHatchCache();
        OnPropertyChanged(nameof(ContoursCount));
        OnPropertyChanged(nameof(TotalPointsCount));
        OnPropertyChanged(nameof(TotalPerimeterLength));
        OnPropertyChanged(nameof(IsClosed));
        OnShapeModified();
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints()
    {
        var allPoints = new List<ToolpathPoint>(TotalPointsCount);
        float px = PositionX;
        float py = PositionY;
        float pz = PositionZ;

        foreach (PathContour contour in _contours)
        {
            foreach (ToolpathPoint p in contour.LocalPoints)
            {
                allPoints.Add(new ToolpathPoint(px + p.X, py + p.Y, pz + p.Z));
            }
        }

        return allPoints;
    }

    public override IEnumerable<ToolpathSegment> GenerateSegments(ToolpathPoint? currentPosition = null)
    {
        bool hasHatch = IsClosed && Hatch.IsEnabled && Hatch.Pattern != HatchPatternType.None;
        bool keepBoundary = !hasHatch || Hatch.KeepBoundary;

        ToolpathPoint? pos = currentPosition;
        float px = PositionX;
        float py = PositionY;
        float pz = PositionZ;

        if (keepBoundary)
        {
            foreach (PathContour contour in _contours)
            {
                IReadOnlyList<ToolpathPoint> pts = contour.LocalPoints;
                if (pts.Count < 2)
                {
                    continue;
                }

                var pStart = new ToolpathPoint(px + pts[0].X, py + pts[0].Y, pz + pts[0].Z);

                // Rapid transition from previous tool position to start of this contour
                if (pos is not null && pos.Value.DistanceTo(pStart) > 0.001f)
                {
                    yield return new ToolpathSegment(pos.Value, pStart, SegmentType.Rapid, LayerId);
                }

                // Trace contour perimeter
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p1 = new ToolpathPoint(px + pts[i].X, py + pts[i].Y, pz + pts[i].Z);
                    var p2 = new ToolpathPoint(px + pts[i + 1].X, py + pts[i + 1].Y, pz + pts[i + 1].Z);
                    yield return new ToolpathSegment(p1, p2, CutType, LayerId);
                }

                // Close contour if requested
                var pEnd = new ToolpathPoint(px + pts[^1].X, py + pts[^1].Y, pz + pts[^1].Z);
                if (contour.IsClosed && pts.Count > 2 && pEnd.DistanceTo(pStart) > 0.001f)
                {
                    yield return new ToolpathSegment(pEnd, pStart, CutType, LayerId);
                    pos = pStart;
                }
                else
                {
                    pos = pEnd;
                }
            }
        }

        // Generate contiguous inner hatch infill across all closed character contours
        if (hasHatch)
        {
            if (CachedHatchSegments is null)
            {
                ToolpathPoint? hatchPos = pos;
                CachedHatchSegments = HatchEngine.GenerateHatch(this, Hatch, ref hatchPos);
            }

            if (CachedHatchSegments.Count > 0)
            {
                if (pos is not null && pos.Value.DistanceTo(CachedHatchSegments[0].Start) > 0.001f)
                {
                    yield return new ToolpathSegment(pos.Value, CachedHatchSegments[0].Start, SegmentType.Rapid, LayerId);
                }

                for (int i = 0; i < CachedHatchSegments.Count; i++)
                {
                    yield return CachedHatchSegments[i];
                }

                pos = CachedHatchSegments[^1].End;
            }
        }
    }

    public override BoundingBox3D GetBounds()
    {
        if (_contours.Count == 0)
        {
            return new BoundingBox3D(PositionX, PositionY, PositionZ, PositionX, PositionY, PositionZ);
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (PathContour contour in _contours)
        {
            BoundingBox3D localB = contour.GetLocalBounds();
            minX = MathF.Min(minX, PositionX + localB.MinX);
            maxX = MathF.Max(maxX, PositionX + localB.MaxX);
            minY = MathF.Min(minY, PositionY + localB.MinY);
            maxY = MathF.Max(maxY, PositionY + localB.MaxY);
            minZ = MathF.Min(minZ, PositionZ + localB.MinZ);
            maxZ = MathF.Max(maxZ, PositionZ + localB.MaxZ);
        }

        return new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        BoundingBox3D bounds = GetBounds();
        if (worldX < bounds.MinX - tolerance || worldX > bounds.MaxX + tolerance ||
            worldY < bounds.MinY - tolerance || worldY > bounds.MaxY + tolerance)
        {
            return false;
        }

        float lx = worldX - PositionX;
        float ly = worldY - PositionY;

        // Even-Odd point in loops test for closed contours (detects click inside character strokes)
        var closedLoops = new List<List<HatchGeometry.Point2D>>();
        foreach (PathContour contour in _contours)
        {
            if (contour.IsClosed && contour.PointsCount >= 3)
            {
                var loop = new List<HatchGeometry.Point2D>(contour.PointsCount);
                foreach (ToolpathPoint p in contour.LocalPoints)
                {
                    loop.Add(new HatchGeometry.Point2D(p.X, p.Y));
                }
                closedLoops.Add(loop);
            }
        }

        if (closedLoops.Count > 0 && HatchGeometry.IsPointInLoops(new HatchGeometry.Point2D(lx, ly), closedLoops))
        {
            return true;
        }

        // Proximity test to any edge of any contour
        float tolSq = tolerance * tolerance;
        foreach (PathContour contour in _contours)
        {
            IReadOnlyList<ToolpathPoint> pts = contour.LocalPoints;
            if (pts.Count == 0)
            {
                continue;
            }

            int edgeCount = contour.IsClosed && pts.Count > 2 ? pts.Count : pts.Count - 1;
            for (int i = 0; i < edgeCount; i++)
            {
                ToolpathPoint p1 = pts[i];
                ToolpathPoint p2 = pts[(i + 1) % pts.Count];

                float dx = p2.X - p1.X;
                float dy = p2.Y - p1.Y;
                float lenSq = dx * dx + dy * dy;

                float distSq;
                if (lenSq < 1e-6f)
                {
                    distSq = MathF.Pow(lx - p1.X, 2) + MathF.Pow(ly - p1.Y, 2);
                }
                else
                {
                    float t = Math.Clamp(((lx - p1.X) * dx + (ly - p1.Y) * dy) / lenSq, 0f, 1f);
                    float projX = p1.X + t * dx;
                    float projY = p1.Y + t * dy;
                    distSq = MathF.Pow(lx - projX, 2) + MathF.Pow(ly - projY, 2);
                }

                if (distSq <= tolSq)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public override void Scale(float factor, float originX, float originY)
    {
        PositionX = originX + (PositionX - originX) * factor;
        PositionY = originY + (PositionY - originY) * factor;
        FontSize = MathF.Max(0.1f, FontSize * factor);
    }

    public override void Rotate(float deltaAngleDegrees, float originX, float originY)
    {
        var (newX, newY) = RotatePoint(PositionX, PositionY, originX, originY, deltaAngleDegrees);
        PositionX = newX;
        PositionY = newY;
        RotationDegrees = (RotationDegrees + deltaAngleDegrees % 360f + 360f) % 360f;
    }

    public override void CopyTransformFrom(ToolpathShape source)
    {
        base.CopyTransformFrom(source);
        if (source is TextShape text)
        {
            _text = text._text;
            _fontFamily = text._fontFamily;
            _fontSize = text._fontSize;
            _isBold = text._isBold;
            _isItalic = text._isItalic;
            _letterSpacing = text._letterSpacing;
            _rotationDegrees = text._rotationDegrees;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(IsBold));
            OnPropertyChanged(nameof(IsItalic));
            OnPropertyChanged(nameof(LetterSpacing));
            OnPropertyChanged(nameof(RotationDegrees));
            RebuildBaseContours();
        }
    }

    public override ToolpathShape Clone()
    {
        var copy = new TextShape(
            PositionX, PositionY, PositionZ,
            _text, _fontFamily, _fontSize, _isBold, _isItalic, _letterSpacing, _rotationDegrees)
        {
            Name = $"{Name} Copy",
            LayerId = LayerId,
            CutType = CutType
        };
        copy.Hatch.CopyFrom(Hatch);
        return copy;
    }
}
