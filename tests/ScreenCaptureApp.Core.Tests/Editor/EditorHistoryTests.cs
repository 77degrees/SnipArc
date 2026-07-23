using ScreenCaptureApp.Core.Annotations;
using ScreenCaptureApp.Core.Editor;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Tests.Editor;

public sealed class EditorHistoryTests
{
    public static TheoryData<Annotation> EveryAnnotationType => new()
    {
        new FreehandPathAnnotation(Guid.NewGuid(), [new(0, 0), new(5, 5)], RgbaColor.RedColor, 2),
        new LineAnnotation(Guid.NewGuid(), new(0, 0), new(5, 5), RgbaColor.RedColor, 2),
        new ArrowAnnotation(Guid.NewGuid(), new(0, 0), new(5, 5), RgbaColor.RedColor, 2),
        new RectangleAnnotation(Guid.NewGuid(), new(0, 0, 5, 5), RgbaColor.RedColor, 2),
        new HighlightAnnotation(Guid.NewGuid(), [new(0, 0), new(5, 5)], new(255, 255, 0, 100), 8),
        new TextAnnotation(Guid.NewGuid(), new(0, 0), "Text", "Segoe UI", 12, RgbaColor.Black),
        new PixelateAnnotation(Guid.NewGuid(), new(0, 0, 5, 5), 4),
        new OpaqueRedactionAnnotation(Guid.NewGuid(), new(0, 0, 5, 5), RgbaColor.Black),
    };

    [Theory]
    [MemberData(nameof(EveryAnnotationType))]
    public void AddUndoRedo_FullyRestoresEveryAnnotationType(Annotation annotation)
    {
        var initial = CreateDocument();
        var history = new EditorHistory(initial);

        Assert.True(history.Execute(new AddAnnotationCommand(annotation)));
        Assert.True(history.Current.IsSelectionLocked);
        Assert.Same(annotation, Assert.Single(history.Current.Annotations));

        Assert.True(history.Undo());
        Assert.Same(initial, history.Current);
        Assert.False(history.Current.IsSelectionLocked);

        Assert.True(history.Redo());
        Assert.Same(annotation, Assert.Single(history.Current.Annotations));
    }

    [Fact]
    public void FirstAnnotation_LocksSelectionUntilItIsUndone()
    {
        var history = new EditorHistory(CreateDocument());
        history.Execute(new AddAnnotationCommand(
            new RectangleAnnotation(Guid.NewGuid(), new PhysicalRect(10, 10, 20, 20), RgbaColor.RedColor, 2)));

        Assert.Throws<InvalidOperationException>(() => history.Execute(
            new SetSelectionCommand(new SelectionState(new PhysicalRect(0, 0, 50, 50)))));

        history.Undo();

        Assert.True(history.Execute(
            new SetSelectionCommand(new SelectionState(new PhysicalRect(0, 0, 50, 50)))));
    }

    [Fact]
    public void NewCommandAfterUndo_ClearsRedoBranch()
    {
        var history = new EditorHistory(CreateDocument());
        history.Execute(new SetEditorSettingsCommand(new EditorSettings(AnnotationTool.Line, RgbaColor.Black, 4)));
        history.Undo();

        history.Execute(new SetEditorSettingsCommand(new EditorSettings(AnnotationTool.Text, RgbaColor.White, 6)));

        Assert.False(history.CanRedo);
        Assert.False(history.Redo());
        Assert.Equal(AnnotationTool.Text, history.Current.Settings.SelectedTool);
    }

    [Fact]
    public void ReplaceAnnotation_PreservesStableOrderingAndUndoState()
    {
        var firstId = Guid.NewGuid();
        var first = new LineAnnotation(firstId, new(0, 0), new(5, 5), RgbaColor.Black, 1);
        var second = new TextAnnotation(Guid.NewGuid(), new(10, 10), "Second", "Segoe UI", 12, RgbaColor.Black);
        var document = new CaptureDocument(CreateDocument().Selection, annotations: [first, second]);
        var history = new EditorHistory(document);
        var replacement = new LineAnnotation(firstId, new(1, 1), new(8, 8), RgbaColor.RedColor, 3);

        history.Execute(new ReplaceAnnotationCommand(replacement));

        Assert.Equal(2, history.Current.Annotations.Length);
        Assert.Equal(replacement, history.Current.Annotations[0]);
        Assert.Equal(second, history.Current.Annotations[1]);
        history.Undo();
        Assert.Equal(2, history.Current.Annotations.Length);
        Assert.Equal(first, history.Current.Annotations[0]);
        Assert.Equal(second, history.Current.Annotations[1]);
    }

    [Fact]
    public void NoOpCommand_IsNotAddedToHistory()
    {
        var document = CreateDocument();
        var history = new EditorHistory(document);

        var changed = history.Execute(new SetSelectionCommand(document.Selection));

        Assert.False(changed);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void ChangingDefaults_DoesNotMutateExistingAnnotationStyle()
    {
        var annotation = new RectangleAnnotation(Guid.NewGuid(), new(0, 0, 10, 10), RgbaColor.RedColor, 2);
        var history = new EditorHistory(CreateDocument());
        history.Execute(new AddAnnotationCommand(annotation));

        history.Execute(new SetEditorSettingsCommand(new EditorSettings(AnnotationTool.Rectangle, RgbaColor.Black, 10)));

        var existing = Assert.IsType<RectangleAnnotation>(history.Current.Annotations[0]);
        Assert.Equal(RgbaColor.RedColor, existing.Color);
        Assert.Equal(2, existing.StrokeWidth);
    }

    private static CaptureDocument CreateDocument() =>
        new(new SelectionState(new PhysicalRect(-100, -50, 300, 200)));
}
