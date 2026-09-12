using AblationStudio.Core.Models;

namespace AblationStudio.Core.Shapes;

public static class ToolpathShapeConverter
{
    private const float SpatialDiscontinuityEpsilon = 0.005f;

    public static PathShape? ExtractShape(Toolpath toolpath)
    {
        ArgumentNullException.ThrowIfNull(toolpath);

        if (toolpath.Segments.Count == 0)
        {
            return null;
        }

        var contours = new List<List<ToolpathPoint>>();
        var currentPoints = new List<ToolpathPoint>();
        int primaryLayerId = 1;
        SegmentType primaryCutType = SegmentType.Cut;
        bool hasSeenCut = false;

        void FlushContour()
        {
            if (currentPoints.Count < 2)
            {
                currentPoints.Clear();
                return;
            }

            contours.Add([.. currentPoints]);
            currentPoints.Clear();
        }

        foreach (ToolpathSegment seg in toolpath.Segments)
        {
            if (seg.Type == SegmentType.Rapid)
            {
                FlushContour();
                continue;
            }

            if (!hasSeenCut)
            {
                primaryLayerId = seg.LayerId;
                primaryCutType = seg.Type;
                hasSeenCut = true;
            }

            if (currentPoints.Count == 0)
            {
                currentPoints.Add(seg.Start);
                currentPoints.Add(seg.End);
            }
            else
            {
                // If there is a spatial discontinuity from the previous point, flush and start a new contour
                if (currentPoints[^1].DistanceTo(seg.Start) > SpatialDiscontinuityEpsilon)
                {
                    FlushContour();
                    currentPoints.Add(seg.Start);
                    currentPoints.Add(seg.End);
                }
                else
                {
                    currentPoints.Add(seg.End);
                }
            }
        }

        FlushContour();

        if (contours.Count == 0)
        {
            return null;
        }

        string shapeName = !string.IsNullOrWhiteSpace(toolpath.Name) ? toolpath.Name : "ImportedPath";
        return PathShape.FromWorldContours(contours, shapeName, primaryLayerId, primaryCutType);
    }
}
