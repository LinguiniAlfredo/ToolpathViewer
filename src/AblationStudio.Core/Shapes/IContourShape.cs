namespace AblationStudio.Core.Shapes;

public interface IContourShape
{
    IReadOnlyList<PathContour> Contours { get; }
}
