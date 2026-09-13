namespace AblationStudio.Core.History.Actions;

public sealed class CompositeAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;

    public string Description { get; }

    public CompositeAction(IEnumerable<IUndoableAction> actions, string description)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = [.. actions];
        Description = description;
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }

    public void Redo()
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            _actions[i].Redo();
        }
    }
}
