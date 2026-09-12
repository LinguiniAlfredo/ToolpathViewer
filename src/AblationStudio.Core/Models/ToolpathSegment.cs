namespace AblationStudio.Core.Models;

public sealed class ToolpathSegment
{
    public ToolpathPoint Start { get; }
    public ToolpathPoint End { get; }
    public SegmentType Type { get; }
    public int LayerId { get; }
    public float Length { get; }

    public ToolpathSegment(ToolpathPoint start, ToolpathPoint end, SegmentType type, int layerId = 0)
    {
        Start = start;
        End = end;
        Type = type;
        LayerId = layerId;
        Length = start.DistanceTo(end);
    }

    public override string ToString() =>
        $"{Type}: {Start} -> {End} (Length: {Length:F3} mm, Layer: {LayerId})";
}
