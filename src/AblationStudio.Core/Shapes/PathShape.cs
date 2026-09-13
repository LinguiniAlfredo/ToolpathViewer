using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes.Hatching;

namespace AblationStudio.Core.Shapes;

public sealed class PathShape : ToolpathShape, IContourShape
{
    private readonly List<PathContour> _contours = [];

    public override string ShapeType => "Path";
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

    public PathShape()
    {
        Name = "Path";
    }

    public PathShape(float centerX, float centerY, float centerZ, IEnumerable<PathContour> contours)
    {
        Name = "Path";
        PositionX = centerX;
        PositionY = centerY;
        PositionZ = centerZ;
        ArgumentNullException.ThrowIfNull(contours);
        _contours.AddRange(contours);
    }

    public void AddContour(PathContour contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        _contours.Add(contour);
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

        // Generate contiguous inner hatch infill across all closed contours
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

        // Even-Odd point in loops test for closed contours
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

        foreach (PathContour contour in _contours)
        {
            contour.Scale(factor);
        }

        InvalidateHatchCache();
        OnPropertyChanged(nameof(TotalPerimeterLength));
        OnShapeModified();
    }

    public override void Rotate(float deltaAngleDegrees, float originX, float originY)
    {
        var (newX, newY) = RotatePoint(PositionX, PositionY, originX, originY, deltaAngleDegrees);
        PositionX = newX;
        PositionY = newY;

        foreach (PathContour contour in _contours)
        {
            contour.Rotate(deltaAngleDegrees);
        }

        InvalidateHatchCache();
        OnShapeModified();
    }

    public override void CopyTransformFrom(ToolpathShape source)
    {
        base.CopyTransformFrom(source);
        if (source is PathShape path)
        {
            _contours.Clear();
            foreach (PathContour c in path._contours)
            {
                _contours.Add(c.Clone());
            }
            InvalidateHatchCache();
            OnPropertyChanged(nameof(ContoursCount));
            OnPropertyChanged(nameof(TotalPointsCount));
            OnPropertyChanged(nameof(TotalPerimeterLength));
            OnPropertyChanged(nameof(IsClosed));
            OnShapeModified();
        }
    }

    public override void CopyAllFrom(ToolpathShape source)
    {
        base.CopyAllFrom(source);
        if (source is PathShape path)
        {
            _contours.Clear();
            foreach (PathContour c in path._contours)
            {
                _contours.Add(c.Clone());
            }
            InvalidateHatchCache();
            OnPropertyChanged(nameof(ContoursCount));
            OnPropertyChanged(nameof(TotalPointsCount));
            OnPropertyChanged(nameof(TotalPerimeterLength));
            OnPropertyChanged(nameof(IsClosed));
            OnShapeModified();
        }
    }

    public override ToolpathShape Clone()
    {
        var clonedContours = new List<PathContour>(_contours.Count);
        foreach (PathContour c in _contours)
        {
            clonedContours.Add(c.Clone());
        }

        var copy = new PathShape(PositionX, PositionY, PositionZ, clonedContours)
        {
            Name = Name,
            LayerId = LayerId,
            CutType = CutType
        };
        copy.Hatch.CopyFrom(Hatch);
        return copy;
    }

    public static PathShape FromWorldContours(
        IEnumerable<IReadOnlyList<ToolpathPoint>> worldContours,
        string name = "Path",
        int layerId = 1,
        SegmentType cutType = SegmentType.Cut)
    {
        ArgumentNullException.ThrowIfNull(worldContours);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        bool hasAnyPoint = false;

        var rawContours = new List<IReadOnlyList<ToolpathPoint>>();

        foreach (IReadOnlyList<ToolpathPoint> contour in worldContours)
        {
            if (contour.Count == 0)
            {
                continue;
            }

            hasAnyPoint = true;
            rawContours.Add(contour);

            foreach (ToolpathPoint p in contour)
            {
                minX = MathF.Min(minX, p.X);
                maxX = MathF.Max(maxX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxY = MathF.Max(maxY, p.Y);
                minZ = MathF.Min(minZ, p.Z);
                maxZ = MathF.Max(maxZ, p.Z);
            }
        }

        if (!hasAnyPoint)
        {
            return new PathShape(0f, 0f, 0f, []) { Name = name, LayerId = layerId, CutType = cutType };
        }

        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;

        var localContours = new List<PathContour>(rawContours.Count);

        foreach (IReadOnlyList<ToolpathPoint> worldPts in rawContours)
        {
            bool isClosed = worldPts.Count > 2 && worldPts[0].DistanceTo(worldPts[^1]) < 0.05f;
            int count = worldPts.Count;
            if (isClosed && worldPts[0].DistanceTo(worldPts[^1]) < 0.001f)
            {
                count--;
            }

            var pts = new List<ToolpathPoint>(count);
            for (int i = 0; i < count; i++)
            {
                ToolpathPoint p = worldPts[i];
                pts.Add(new ToolpathPoint(p.X - cx, p.Y - cy, p.Z - cz));
            }

            localContours.Add(new PathContour(pts, isClosed));
        }

        return new PathShape(cx, cy, cz, localContours)
        {
            Name = name,
            LayerId = layerId,
            CutType = cutType
        };
    }
}
