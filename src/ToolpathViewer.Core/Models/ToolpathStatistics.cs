namespace ToolpathViewer.Core.Models;

public sealed class ToolpathStatistics
{
    public int TotalSegments { get; }
    public int CutSegmentsCount { get; }
    public int RapidSegmentsCount { get; }
    public float TotalCutLength { get; }
    public float TotalRapidLength { get; }
    public float TotalLength => TotalCutLength + TotalRapidLength;
    public BoundingBox3D BoundingBox { get; }

    public ToolpathStatistics(
        int totalSegments,
        int cutSegmentsCount,
        int rapidSegmentsCount,
        float totalCutLength,
        float totalRapidLength,
        BoundingBox3D boundingBox)
    {
        TotalSegments = totalSegments;
        CutSegmentsCount = cutSegmentsCount;
        RapidSegmentsCount = rapidSegmentsCount;
        TotalCutLength = totalCutLength;
        TotalRapidLength = totalRapidLength;
        BoundingBox = boundingBox;
    }

    public static ToolpathStatistics Calculate(IReadOnlyList<ToolpathSegment> segments)
    {
        int total = segments.Count;
        int cutCount = 0;
        int rapidCount = 0;
        float cutLen = 0f;
        float rapidLen = 0f;
        BoundingBox3D bbox = BoundingBox3D.Empty;

        for (int i = 0; i < segments.Count; i++)
        {
            ToolpathSegment seg = segments[i];
            bbox = bbox.Expand(seg.Start);
            bbox = bbox.Expand(seg.End);

            if (seg.Type == SegmentType.Cut)
            {
                cutCount++;
                cutLen += seg.Length;
            }
            else
            {
                rapidCount++;
                rapidLen += seg.Length;
            }
        }

        return new ToolpathStatistics(total, cutCount, rapidCount, cutLen, rapidLen, bbox);
    }
}
