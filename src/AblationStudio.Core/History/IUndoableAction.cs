namespace AblationStudio.Core.History;

public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}
