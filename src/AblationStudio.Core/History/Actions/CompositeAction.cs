using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.History.Actions;

public sealed class CompositeAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;
    private readonly ShapeDocument? _document;
    private readonly IReadOnlyList<ToolpathShape>? _selectionBefore;
    private readonly IReadOnlyList<ToolpathShape>? _selectionAfter;

    public string Description { get; }

    public CompositeAction(
        IEnumerable<IUndoableAction> actions,
        string description,
        ShapeDocument? document = null,
        IEnumerable<ToolpathShape>? selectionBefore = null,
        IEnumerable<ToolpathShape>? selectionAfter = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = [.. actions];
        Description = description;
        _document = document;
        _selectionBefore = selectionBefore is not null ? [.. selectionBefore] : null;
        _selectionAfter = selectionAfter is not null ? [.. selectionAfter] : null;
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }

        if (_document is not null && _selectionBefore is not null)
        {
            _document.SetSelection(_selectionBefore);
        }
    }

    public void Redo()
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            _actions[i].Redo();
        }

        if (_document is not null && _selectionAfter is not null)
        {
            _document.SetSelection(_selectionAfter);
        }
    }
}
