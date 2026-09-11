namespace ToolpathViewer.Core.Models;

public sealed class Toolpath
{
    public string Name { get; }
    public string FilePath { get; }
    public IReadOnlyList<ToolpathSegment> Segments { get; }
    public ToolpathStatistics Statistics { get; }
    public BoundingBox3D BoundingBox => Statistics.BoundingBox;

    public Toolpath(string name, string filePath, IReadOnlyList<ToolpathSegment> segments)
    {
        Name = name;
        FilePath = filePath;
        Segments = segments;
        Statistics = ToolpathStatistics.Calculate(segments);
    }

    public static Toolpath Empty => new(string.Empty, string.Empty, []);
}
