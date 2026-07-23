using System.Collections.Immutable;
using ScreenCaptureApp.Core.Annotations;

namespace ScreenCaptureApp.Core.Editor;

/// <summary>
/// Immutable editor state. Captured pixels remain owned by the capture session;
/// every coordinate in this document uses signed physical desktop pixels.
/// </summary>
public sealed record CaptureDocument
{
    public CaptureDocument(
        SelectionState selection,
        EditorSettings? settings = null,
        IEnumerable<Annotation>? annotations = null)
    {
        ArgumentNullException.ThrowIfNull(selection);

        Selection = selection;
        Settings = settings ?? EditorSettings.Default;
        Annotations = annotations is null
            ? ImmutableArray<Annotation>.Empty
            : ImmutableArray.CreateRange(annotations);

        if (Annotations.Any(static annotation => annotation is null))
        {
            throw new ArgumentException("Annotations cannot contain null values.", nameof(annotations));
        }

        if (Annotations.Select(static annotation => annotation.Id).Distinct().Count() != Annotations.Length)
        {
            throw new ArgumentException("Annotation identifiers must be unique.", nameof(annotations));
        }
    }

    public SelectionState Selection { get; }

    public EditorSettings Settings { get; }

    public ImmutableArray<Annotation> Annotations { get; }

    public bool IsSelectionLocked => !Annotations.IsEmpty;

    public CaptureDocument WithSelection(SelectionState selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (IsSelectionLocked && selection != Selection)
        {
            throw new InvalidOperationException("Selection bounds are locked after the first annotation is committed.");
        }

        return selection == Selection ? this : new CaptureDocument(selection, Settings, Annotations);
    }

    public CaptureDocument WithSettings(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings == Settings ? this : new CaptureDocument(Selection, settings, Annotations);
    }

    public CaptureDocument AddAnnotation(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (Annotations.Any(existing => existing.Id == annotation.Id))
        {
            throw new ArgumentException($"Annotation '{annotation.Id}' already exists.", nameof(annotation));
        }

        return new CaptureDocument(Selection, Settings, Annotations.Add(annotation));
    }

    public CaptureDocument RemoveAnnotation(Guid annotationId)
    {
        var index = FindAnnotationIndex(annotationId);
        return index < 0 ? this : new CaptureDocument(Selection, Settings, Annotations.RemoveAt(index));
    }

    public CaptureDocument ReplaceAnnotation(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var index = FindAnnotationIndex(annotation.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Annotation '{annotation.Id}' does not exist.");
        }

        return Equals(Annotations[index], annotation)
            ? this
            : new CaptureDocument(Selection, Settings, Annotations.SetItem(index, annotation));
    }

    private int FindAnnotationIndex(Guid annotationId)
    {
        for (var index = 0; index < Annotations.Length; index++)
        {
            if (Annotations[index].Id == annotationId)
            {
                return index;
            }
        }

        return -1;
    }
}
