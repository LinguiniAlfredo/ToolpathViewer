using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public sealed class ImageShape : ToolpathShape
{
    private byte[] _pixelData = [];
    private int _pixelWidth;
    private int _pixelHeight;
    private byte[]? _rawImageData;
    private string? _sourceFileName;

    private float _width = 20.0f;
    private float _height = 20.0f;
    private float _rotationDegrees;
    private bool _lockAspectRatio = true;

    private ImageRasterMode _rasterMode = ImageRasterMode.GrayscaleDensity;
    private float _stepover = 0.2f;
    private float _brightnessThreshold = 0.5f;
    private bool _invertImage;
    private bool _bidirectionalScan = true;
    private float _overlayOpacity = 0.4f;
    private bool _showOverlay = true;

    public override string ShapeType => "Image";
    public override bool IsClosed => true;

    public byte[] PixelData
    {
        get => _pixelData;
        set
        {
            _pixelData = value ?? [];
            InvalidateHatchCache();
            OnPropertyChanged();
            OnShapeModified();
        }
    }

    public int PixelWidth
    {
        get => _pixelWidth;
        set
        {
            if (SetProperty(ref _pixelWidth, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(AspectRatio));
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public int PixelHeight
    {
        get => _pixelHeight;
        set
        {
            if (SetProperty(ref _pixelHeight, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(AspectRatio));
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public byte[]? RawImageData
    {
        get => _rawImageData;
        set => SetProperty(ref _rawImageData, value);
    }

    public string? SourceFileName
    {
        get => _sourceFileName;
        set => SetProperty(ref _sourceFileName, value);
    }

    public float AspectRatio => _pixelHeight > 0 ? (float)_pixelWidth / _pixelHeight : 1.0f;

    public bool LockAspectRatio
    {
        get => _lockAspectRatio;
        set => SetProperty(ref _lockAspectRatio, value);
    }

    public float Width
    {
        get => _width;
        set
        {
            float val = MathF.Max(0.1f, value);
            if (SetProperty(ref _width, val))
            {
                if (_lockAspectRatio && AspectRatio > 0.001f)
                {
                    float expectedHeight = MathF.Max(0.1f, val / AspectRatio);
                    if (MathF.Abs(_height - expectedHeight) > 1e-4f)
                    {
                        _height = expectedHeight;
                        OnPropertyChanged(nameof(Height));
                    }
                }
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float Height
    {
        get => _height;
        set
        {
            float val = MathF.Max(0.1f, value);
            if (SetProperty(ref _height, val))
            {
                if (_lockAspectRatio && AspectRatio > 0.001f)
                {
                    float expectedWidth = MathF.Max(0.1f, val * AspectRatio);
                    if (MathF.Abs(_width - expectedWidth) > 1e-4f)
                    {
                        _width = expectedWidth;
                        OnPropertyChanged(nameof(Width));
                    }
                }
                InvalidateHatchCache();
                OnShapeModified();
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
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public ImageRasterMode RasterMode
    {
        get => _rasterMode;
        set
        {
            if (SetProperty(ref _rasterMode, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float Stepover
    {
        get => _stepover;
        set
        {
            float val = MathF.Max(0.01f, value);
            if (SetProperty(ref _stepover, val))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float BrightnessThreshold
    {
        get => _brightnessThreshold;
        set
        {
            float val = Math.Clamp(value, 0.0f, 1.0f);
            if (SetProperty(ref _brightnessThreshold, val))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public bool InvertImage
    {
        get => _invertImage;
        set
        {
            if (SetProperty(ref _invertImage, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public bool BidirectionalScan
    {
        get => _bidirectionalScan;
        set
        {
            if (SetProperty(ref _bidirectionalScan, value))
            {
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
    }

    public float OverlayOpacity
    {
        get => _overlayOpacity;
        set
        {
            float val = Math.Clamp(value, 0.0f, 1.0f);
            if (SetProperty(ref _overlayOpacity, val))
            {
                OnShapeModified();
            }
        }
    }

    public bool ShowOverlay
    {
        get => _showOverlay;
        set
        {
            if (SetProperty(ref _showOverlay, value))
            {
                OnShapeModified();
            }
        }
    }

    public ImageShape()
    {
        Name = "Image";
    }

    public ImageShape(
        float centerX,
        float centerY,
        float centerZ,
        float width,
        float height,
        byte[] pixelData,
        int pixelWidth,
        int pixelHeight,
        byte[]? rawImageData = null,
        string? sourceFileName = null,
        float rotationDegrees = 0f)
    {
        Name = !string.IsNullOrEmpty(sourceFileName) ? System.IO.Path.GetFileNameWithoutExtension(sourceFileName) : "Image";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        _width = MathF.Max(0.1f, width);
        _height = MathF.Max(0.1f, height);
        _pixelData = pixelData ?? [];
        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        _rawImageData = rawImageData;
        _sourceFileName = sourceFileName;
        _rotationDegrees = (rotationDegrees % 360f + 360f) % 360f;
    }

    public override IReadOnlyList<ToolpathPoint> GetPathPoints()
    {
        float hw = _width * 0.5f;
        float hh = _height * 0.5f;
        float rad = _rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        (float lx, float ly)[] localCorners =
        [
            (-hw, -hh),
            (hw, -hh),
            (hw, hh),
            (-hw, hh)
        ];

        var points = new ToolpathPoint[4];
        for (int i = 0; i < 4; i++)
        {
            float gx = PositionX + localCorners[i].lx * cos - localCorners[i].ly * sin;
            float gy = PositionY + localCorners[i].lx * sin + localCorners[i].ly * cos;
            points[i] = new ToolpathPoint(gx, gy, PositionZ);
        }

        return points;
    }

    public override IEnumerable<ToolpathSegment> GenerateSegments(ToolpathPoint? currentPosition = null, bool includeHatch = true)
    {
        if (_pixelWidth <= 0 || _pixelHeight <= 0 || _pixelData.Length < _pixelWidth * _pixelHeight)
        {
            yield break;
        }

        if (CachedHatchSegments is not null)
        {
            ToolpathPoint? prev = currentPosition;
            foreach (ToolpathSegment seg in CachedHatchSegments)
            {
                if (prev is not null && prev.Value.DistanceTo(seg.Start) > 0.001f)
                {
                    yield return new ToolpathSegment(prev.Value, seg.Start, SegmentType.Rapid, LayerId);
                }
                yield return seg;
                prev = seg.End;
            }
            yield break;
        }

        var generated = new List<ToolpathSegment>();
        float stepover = MathF.Max(0.01f, _stepover);
        int numScanlines = Math.Max(1, (int)MathF.Ceiling(_height / stepover));

        float hw = _width * 0.5f;
        float hh = _height * 0.5f;
        float rad = _rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        ToolpathPoint TransformPoint(float localX, float localY)
        {
            float gx = PositionX + localX * cos - localY * sin;
            float gy = PositionY + localX * sin + localY * cos;
            return new ToolpathPoint(gx, gy, PositionZ);
        }

        ToolpathPoint? runningPos = currentPosition;

        for (int lineIdx = 0; lineIdx < numScanlines; lineIdx++)
        {
            float localY = hh - (lineIdx + 0.5f) * (_height / numScanlines);
            float normY = (hh - localY) / _height;
            int pixelY = Math.Clamp((int)(normY * _pixelHeight), 0, _pixelHeight - 1);

            bool reverse = _bidirectionalScan && (lineIdx % 2 == 1);

            if (_rasterMode == ImageRasterMode.Threshold)
            {
                GenerateThresholdScanline(
                    generated, pixelY, localY, hw, reverse, TransformPoint, ref runningPos);
            }
            else
            {
                GenerateGrayscaleDensityScanline(
                    generated, pixelY, localY, hw, reverse, TransformPoint, ref runningPos);
            }
        }

        CachedHatchSegments = generated;

        foreach (ToolpathSegment seg in generated)
        {
            yield return seg;
        }
    }

    private void GenerateThresholdScanline(
        List<ToolpathSegment> segments,
        int pixelY,
        float localY,
        float hw,
        bool reverse,
        Func<float, float, ToolpathPoint> transform,
        ref ToolpathPoint? runningPos)
    {
        int rowStart = pixelY * _pixelWidth;
        float? runStartLocalX = null;

        for (int step = 0; step < _pixelWidth; step++)
        {
            int pixelX = reverse ? (_pixelWidth - 1 - step) : step;
            byte intensity = _pixelData[rowStart + pixelX];
            float brightness = intensity / 255.0f;

            bool isMark = (brightness < _brightnessThreshold) ^ _invertImage;

            float normXStart = (float)pixelX / _pixelWidth;
            float normXEnd = (float)(pixelX + 1) / _pixelWidth;
            float localXStart = -hw + normXStart * _width;
            float localXEnd = -hw + normXEnd * _width;

            float spanStart = reverse ? localXEnd : localXStart;
            float spanEnd = reverse ? localXStart : localXEnd;

            if (isMark)
            {
                runStartLocalX ??= spanStart;
                if (step == _pixelWidth - 1)
                {
                    EmitCut(segments, transform(runStartLocalX.Value, localY), transform(spanEnd, localY), ref runningPos);
                    runStartLocalX = null;
                }
            }
            else if (runStartLocalX is not null)
            {
                EmitCut(segments, transform(runStartLocalX.Value, localY), transform(spanStart, localY), ref runningPos);
                runStartLocalX = null;
            }
        }
    }

    private void GenerateGrayscaleDensityScanline(
        List<ToolpathSegment> segments,
        int pixelY,
        float localY,
        float hw,
        bool reverse,
        Func<float, float, ToolpathPoint> transform,
        ref ToolpathPoint? runningPos)
    {
        int rowStart = pixelY * _pixelWidth;
        float cellWidth = _width / _pixelWidth;

        for (int step = 0; step < _pixelWidth; step++)
        {
            int pixelX = reverse ? (_pixelWidth - 1 - step) : step;
            byte intensity = _pixelData[rowStart + pixelX];
            float brightness = intensity / 255.0f;

            float darkness = _invertImage ? brightness : (1.0f - brightness);

            if (darkness < 0.05f)
            {
                continue;
            }

            float normCellStart = (float)pixelX / _pixelWidth;
            float localCellX0 = -hw + normCellStart * _width;

            float markLength = darkness * cellWidth;
            float margin = (cellWidth - markLength) * 0.5f;

            float markX0 = localCellX0 + margin;
            float markX1 = markX0 + markLength;

            if (reverse)
            {
                EmitCut(segments, transform(markX1, localY), transform(markX0, localY), ref runningPos);
            }
            else
            {
                EmitCut(segments, transform(markX0, localY), transform(markX1, localY), ref runningPos);
            }
        }
    }

    private void EmitCut(
        List<ToolpathSegment> segments,
        ToolpathPoint start,
        ToolpathPoint end,
        ref ToolpathPoint? runningPos)
    {
        if (start.DistanceTo(end) < 1e-4f)
        {
            return;
        }

        if (runningPos is not null && runningPos.Value.DistanceTo(start) > 0.001f)
        {
            segments.Add(new ToolpathSegment(runningPos.Value, start, SegmentType.Rapid, LayerId));
        }

        segments.Add(new ToolpathSegment(start, end, CutType, LayerId));
        runningPos = end;
    }

    public override BoundingBox3D GetBounds()
    {
        IReadOnlyList<ToolpathPoint> points = GetPathPoints();
        if (points.Count == 0)
        {
            return new BoundingBox3D(PositionX, PositionY, PositionZ, PositionX, PositionY, PositionZ);
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (ToolpathPoint p in points)
        {
            minX = MathF.Min(minX, p.X);
            maxX = MathF.Max(maxX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxY = MathF.Max(maxY, p.Y);
            minZ = MathF.Min(minZ, p.Z);
            maxZ = MathF.Max(maxZ, p.Z);
        }

        return new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public override bool HitTest(float worldX, float worldY, float tolerance)
    {
        float dx = worldX - PositionX;
        float dy = worldY - PositionY;

        float rad = -_rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        float lx = MathF.Abs(dx * cos - dy * sin);
        float ly = MathF.Abs(dx * sin + dy * cos);

        return lx <= (_width * 0.5f + tolerance) && ly <= (_height * 0.5f + tolerance);
    }

    public override void Scale(float factor, float originX, float originY)
    {
        SuspendNotifications();
        try
        {
            PositionX = originX + (PositionX - originX) * factor;
            PositionY = originY + (PositionY - originY) * factor;
            _width = MathF.Max(0.1f, _width * factor);
            _height = MathF.Max(0.1f, _height * factor);
            InvalidateHatchCache();
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnShapeModified();
        }
        finally
        {
            ResumeNotifications();
        }
    }

    public override void Rotate(float deltaAngleDegrees, float originX, float originY)
    {
        SuspendNotifications();
        try
        {
            var (newX, newY) = RotatePoint(PositionX, PositionY, originX, originY, deltaAngleDegrees);
            PositionX = newX;
            PositionY = newY;
            RotationDegrees = (RotationDegrees + deltaAngleDegrees % 360f + 360f) % 360f;
            InvalidateHatchCache();
            OnShapeModified();
        }
        finally
        {
            ResumeNotifications();
        }
    }

    public override void CopyTransformFrom(ToolpathShape source)
    {
        SuspendNotifications();
        try
        {
            base.CopyTransformFrom(source);
            if (source is ImageShape img)
            {
                _width = img._width;
                _height = img._height;
                _rotationDegrees = img._rotationDegrees;
                _lockAspectRatio = img._lockAspectRatio;
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(RotationDegrees));
                OnPropertyChanged(nameof(LockAspectRatio));
                InvalidateHatchCache();
                OnShapeModified();
            }
        }
        finally
        {
            ResumeNotifications();
        }
    }

    public override void CopyAllFrom(ToolpathShape source)
    {
        SuspendNotifications();
        try
        {
            base.CopyAllFrom(source);
            if (source is ImageShape img)
            {
                _width = img._width;
                _height = img._height;
                _rotationDegrees = img._rotationDegrees;
                _lockAspectRatio = img._lockAspectRatio;
                _pixelData = (byte[])img._pixelData.Clone();
                _pixelWidth = img._pixelWidth;
                _pixelHeight = img._pixelHeight;
                _rawImageData = img._rawImageData is not null ? (byte[])img._rawImageData.Clone() : null;
                _sourceFileName = img._sourceFileName;
                _rasterMode = img._rasterMode;
                _stepover = img._stepover;
                _brightnessThreshold = img._brightnessThreshold;
                _invertImage = img._invertImage;
                _bidirectionalScan = img._bidirectionalScan;
                _overlayOpacity = img._overlayOpacity;
                _showOverlay = img._showOverlay;

                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(RotationDegrees));
                OnPropertyChanged(nameof(LockAspectRatio));
                OnPropertyChanged(nameof(PixelData));
                OnPropertyChanged(nameof(PixelWidth));
                OnPropertyChanged(nameof(PixelHeight));
                OnPropertyChanged(nameof(AspectRatio));
                OnPropertyChanged(nameof(SourceFileName));
                OnPropertyChanged(nameof(RasterMode));
                OnPropertyChanged(nameof(Stepover));
                OnPropertyChanged(nameof(BrightnessThreshold));
                OnPropertyChanged(nameof(InvertImage));
                OnPropertyChanged(nameof(BidirectionalScan));
                OnPropertyChanged(nameof(OverlayOpacity));
                OnPropertyChanged(nameof(ShowOverlay));

                InvalidateHatchCache();
                OnShapeModified();
            }
        }
        finally
        {
            ResumeNotifications();
        }
    }

    public override ToolpathShape Clone()
    {
        var copy = new ImageShape(
            PositionX, PositionY, PositionZ,
            Width, Height,
            (byte[])_pixelData.Clone(),
            _pixelWidth, _pixelHeight,
            _rawImageData is not null ? (byte[])_rawImageData.Clone() : null,
            _sourceFileName,
            _rotationDegrees)
        {
            Name = Name,
            LayerId = LayerId,
            CutType = CutType,
            LockAspectRatio = _lockAspectRatio,
            RasterMode = _rasterMode,
            Stepover = _stepover,
            BrightnessThreshold = _brightnessThreshold,
            InvertImage = _invertImage,
            BidirectionalScan = _bidirectionalScan,
            OverlayOpacity = _overlayOpacity,
            ShowOverlay = _showOverlay
        };
        copy.Hatch.CopyFrom(Hatch);
        return copy;
    }
}
