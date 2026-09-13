using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.History.Actions;

public sealed class ClearShapesAction : IUndoableAction
{
    private readonly ShapeDocument _document;
    private readonly List<(ToolpathShape Shape, int Index)> _savedShapes;
    private readonly ToolpathShape? _previousSelectedShape;

    public string Description { get; }

    public ClearShapesAction(
        ShapeDocument document,
        IEnumerable<ToolpathShape> shapes,
        ToolpathShape? previousSelectedShape,
        string description = "Clear All Shapes")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentNullException.ThrowIfNull(shapes);

        int idx = 0;
        _savedShapes = shapes.Select(s => (s, idx++)).ToList();
        _previousSelectedShape = previousSelectedShape;
        Description = description;
    }

    public void Undo()
    {
        foreach (var (shape, index) in _savedShapes)
        {
            _document.InsertShape(index, shape);
        }

        if (_previousSelectedShape is not null && _document.Shapes.Contains(_previousSelectedShape))
        {
            _document.SelectedShape = _previousSelectedShape;
        }
    }

    public void Redo()
    {
        _document.Clear();
    }
}
