using AblationStudio.Core.History.Actions;

namespace AblationStudio.Core.History;

public sealed class UndoRedoManager
{
    private readonly List<IUndoableAction> _undoStack = [];
    private readonly List<IUndoableAction> _redoStack = [];
    private IUndoableAction? _savedAction;
    private bool _hasSavedState;
    private bool _savedStatePruned;
    private int _suppressDepth;

    public int MaxUndoSteps { get; set; } = 100;
    public TimeSpan CoalesceWindow { get; set; } = TimeSpan.FromMilliseconds(800);

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string UndoActionName => CanUndo ? _undoStack[^1].Description : string.Empty;
    public string RedoActionName => CanRedo ? _redoStack[^1].Description : string.Empty;

    public bool IsPerformingUndoRedo { get; private set; }
    public bool IsRecordingSuppressed => _suppressDepth > 0;

    public bool IsModified
    {
        get
        {
            if (!_hasSavedState)
            {
                return _undoStack.Count > 0;
            }

            if (_savedStatePruned)
            {
                return true;
            }

            IUndoableAction? currentTop = _undoStack.Count > 0 ? _undoStack[^1] : null;
            return currentTop != _savedAction;
        }
    }

    public event Action? StateChanged;

    public bool CanCoalesce(string? coalesceKey)
    {
        return !string.IsNullOrEmpty(coalesceKey) &&
               _undoStack.Count > 0 &&
               _undoStack[^1] is ModifyShapeAction topModify &&
               topModify.CoalesceKey == coalesceKey &&
               (DateTime.UtcNow - topModify.Timestamp) <= CoalesceWindow;
    }

    public void RecordAction(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsPerformingUndoRedo || IsRecordingSuppressed)
        {
            return;
        }

        // Check if we can coalesce with the top action on the undo stack
        if (action is ModifyShapeAction newModify && CanCoalesce(newModify.CoalesceKey))
        {
            var topModify = (ModifyShapeAction)_undoStack[^1];
            topModify.UpdateAfterSnapshot(newModify.AfterSnapshot);
            _redoStack.Clear();
            NotifyStateChanged();
            return;
        }

        _undoStack.Add(action);
        _redoStack.Clear();

        while (_undoStack.Count > MaxUndoSteps)
        {
            IUndoableAction pruned = _undoStack[0];
            _undoStack.RemoveAt(0);

            if (_hasSavedState && pruned == _savedAction)
            {
                _savedStatePruned = true;
                _savedAction = null;
            }
        }

        NotifyStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        FlushCoalescing();

        IUndoableAction action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        IsPerformingUndoRedo = true;
        try
        {
            action.Undo();
        }
        finally
        {
            IsPerformingUndoRedo = false;
        }

        _redoStack.Add(action);
        NotifyStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        FlushCoalescing();

        IUndoableAction action = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        IsPerformingUndoRedo = true;
        try
        {
            action.Redo();
        }
        finally
        {
            IsPerformingUndoRedo = false;
        }

        _undoStack.Add(action);
        NotifyStateChanged();
    }

    public void MarkSaved()
    {
        FlushCoalescing();
        _hasSavedState = true;
        _savedStatePruned = false;
        _savedAction = _undoStack.Count > 0 ? _undoStack[^1] : null;
        NotifyStateChanged();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _savedAction = null;
        _hasSavedState = false;
        _savedStatePruned = false;
        NotifyStateChanged();
    }

    public void FlushCoalescing()
    {
        if (_undoStack.Count > 0 && _undoStack[^1] is ModifyShapeAction modify)
        {
            modify.ClearCoalesceKey();
        }
    }

    public IDisposable SuppressRecording()
    {
        _suppressDepth++;
        return new SuppressionScope(this);
    }

    private void EndSuppression()
    {
        if (_suppressDepth > 0)
        {
            _suppressDepth--;
        }
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private sealed class SuppressionScope(UndoRedoManager manager) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                manager.EndSuppression();
                _disposed = true;
            }
        }
    }
}
