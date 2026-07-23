using ScreenCaptureApp.Core.Annotations;

namespace ScreenCaptureApp.Core.Editor;

public interface IEditorCommand
{
    public string Description { get; }

    public CaptureDocument Execute(CaptureDocument document);
}

public sealed record SetSelectionCommand(SelectionState Selection) : IEditorCommand
{
    public string Description => "Change selection";

    public CaptureDocument Execute(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithSelection(Selection);
    }
}

public sealed record AddAnnotationCommand(Annotation Annotation) : IEditorCommand
{
    public string Description => "Add annotation";

    public CaptureDocument Execute(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.AddAnnotation(Annotation);
    }
}

public sealed record RemoveAnnotationCommand(Guid AnnotationId) : IEditorCommand
{
    public string Description => "Remove annotation";

    public CaptureDocument Execute(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.RemoveAnnotation(AnnotationId);
    }
}

public sealed record ReplaceAnnotationCommand(Annotation Annotation) : IEditorCommand
{
    public string Description => "Update annotation";

    public CaptureDocument Execute(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.ReplaceAnnotation(Annotation);
    }
}

public sealed record SetEditorSettingsCommand(EditorSettings Settings) : IEditorCommand
{
    public string Description => "Change editor settings";

    public CaptureDocument Execute(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithSettings(Settings);
    }
}
