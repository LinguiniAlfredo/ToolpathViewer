namespace AblationStudio.Core.Models;

public sealed class ToolpathStatistics
{
    public int TotalSegments { get; }
    public int CutSegmentsCount { get; }
    public int HatchSegmentsCount { get; }
    public int RapidSegmentsCount { get; }
    public float TotalCutLength { get; }
    public float TotalHatchLength { get; }
    public float TotalRapidLength { get; }
    public float TotalLaserLength => TotalCutLength + TotalHatchLength;
    public float TotalLength => TotalCutLength + TotalHatchLength + TotalRapidLength;
    public BoundingBox3D BoundingBox { get; }

    public ToolpathStatistics(
        int totalSegments,
        int cutSegmentsCount,
        int hatchSegmentsCount,
        int rapidSegmentsCount,
        float totalCutLength,
        float totalHatchLength,
        float totalRapidLength,
        BoundingBox3D boundingBox)
    {
        TotalSegments = totalSegments;
        CutSegmentsCount = cutSegmentsCount;
        HatchSegmentsCount = hatchSegmentsCount;
        RapidSegmentsCount = rapidSegmentsCount;
        TotalCutLength = totalCutLength;
        TotalHatchLength = totalHatchLength;
        TotalRapidLength = totalRapidLength;
        BoundingBox = boundingBox;
    }

    public static ToolpathStatistics Calculate(IReadOnlyList<ToolpathSegment> segments)
    {
        int total = segments.Count;
        int cutCount = 0;
        int hatchCount = 0;
        int rapidCount = 0;
        float cutLen = 0f;
        float hatchLen = 0f;
        float rapidLen = 0f;
        BoundingBox3D bbox = BoundingBox3D.Empty;

        for (int i = 0; i < segments.Count; i++)
        {
            ToolpathSegment seg = segments[i];
            bbox = bbox.Expand(seg.Start);
            bbox = bbox.Expand(seg.End);

            switch (seg.Type)
            {
                case SegmentType.Cut:
                    cutCount++;
                    cutLen += seg.Length;
                    break;
                case SegmentType.Hatch:
                    hatchCount++;
                    hatchLen += seg.Length;
                    break;
                case SegmentType.Rapid:
                default:
                    rapidCount++;
                    rapidLen += seg.Length;
                    break;
            }
        }

        return new ToolpathStatistics(total, cutCount, hatchCount, rapidCount, cutLen, hatchLen, rapidLen, bbox);
    }
}
