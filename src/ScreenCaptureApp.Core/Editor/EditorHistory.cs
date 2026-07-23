namespace ScreenCaptureApp.Core.Editor;

/// <summary>A deterministic snapshot-based command history for immutable editor documents.</summary>
public sealed class EditorHistory
{
    private readonly Stack<CaptureDocument> _undo = new();
    private readonly Stack<CaptureDocument> _redo = new();

    public EditorHistory(CaptureDocument initialDocument)
    {
        ArgumentNullException.ThrowIfNull(initialDocument);
        Current = initialDocument;
    }

    public CaptureDocument Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var next = command.Execute(Current);
        ArgumentNullException.ThrowIfNull(next);

        if (ReferenceEquals(next, Current))
        {
            return false;
        }

        _undo.Push(Current);
        Current = next;
        _redo.Clear();
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        _redo.Push(Current);
        Current = _undo.Pop();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        _undo.Push(Current);
        Current = _redo.Pop();
        return true;
    }

    public void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
