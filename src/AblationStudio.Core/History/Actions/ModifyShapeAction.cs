using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.History.Actions;

public sealed class ModifyShapeAction : IUndoableAction
{
    private readonly ToolpathShape _target;
    private readonly ToolpathShape _beforeSnapshot;
    private ToolpathShape _afterSnapshot;
    private readonly ShapeDocument? _document;
    private readonly bool _wasSelected;

    public string Description { get; }
    public string? CoalesceKey { get; private set; }
    public DateTime Timestamp { get; private set; }
    public ToolpathShape AfterSnapshot => _afterSnapshot;

    public ModifyShapeAction(
        ToolpathShape target,
        ToolpathShape beforeSnapshot,
        ToolpathShape afterSnapshot,
        string description,
        string? coalesceKey = null,
        ShapeDocument? document = null,
        bool wasSelected = true)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _beforeSnapshot = beforeSnapshot ?? throw new ArgumentNullException(nameof(beforeSnapshot));
        _afterSnapshot = afterSnapshot ?? throw new ArgumentNullException(nameof(afterSnapshot));
        Description = description;
        CoalesceKey = coalesceKey;
        Timestamp = DateTime.UtcNow;
        _document = document;
        _wasSelected = wasSelected;
    }

    public void UpdateAfterSnapshot(ToolpathShape newAfterSnapshot)
    {
        _afterSnapshot = newAfterSnapshot ?? throw new ArgumentNullException(nameof(newAfterSnapshot));
        Timestamp = DateTime.UtcNow;
    }

    public void ClearCoalesceKey()
    {
        CoalesceKey = null;
    }

    public void Undo()
    {
        _target.CopyAllFrom(_beforeSnapshot);
        if (_wasSelected && _document is not null)
        {
            _document.SelectedShape = _target;
        }
    }

    public void Redo()
    {
        _target.CopyAllFrom(_afterSnapshot);
        if (_wasSelected && _document is not null)
        {
            _document.SelectedShape = _target;
        }
    }
}
