using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.History.Actions;

public sealed class DeleteShapeAction : IUndoableAction
{
    private readonly ShapeDocument _document;
    private readonly ToolpathShape _shape;
    private readonly int _index;
    private readonly bool _wasSelected;

    public string Description { get; }

    public DeleteShapeAction(
        ShapeDocument document,
        ToolpathShape shape,
        int index,
        string? description = null,
        bool wasSelected = true)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
        _index = index;
        _wasSelected = wasSelected;
        Description = string.IsNullOrWhiteSpace(description) ? $"Delete {shape.Name}" : description;
    }

    public void Undo()
    {
        _document.InsertShape(_index, _shape);
        if (_wasSelected)
        {
            _document.SelectedShape = _shape;
        }
    }

    public void Redo()
    {
        _document.RemoveShape(_shape);
    }
}
