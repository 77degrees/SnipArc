using ScreenCaptureApp.Core.Annotations;

namespace ScreenCaptureApp.Core.Editor;

public enum AnnotationTool
{
    Pen,
    Line,
    Arrow,
    Rectangle,
    Highlight,
    Text,
    Pixelate,
    OpaqueRedaction,
}

/// <summary>Defaults applied only to newly-created annotations.</summary>
public sealed record EditorSettings
{
    public EditorSettings(AnnotationTool selectedTool, RgbaColor selectedColor, int strokeWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        SelectedTool = selectedTool;
        SelectedColor = selectedColor;
        StrokeWidth = strokeWidth;
    }

    public AnnotationTool SelectedTool { get; }

    public RgbaColor SelectedColor { get; }

    public int StrokeWidth { get; }

    public static EditorSettings Default { get; } = new(AnnotationTool.Pen, RgbaColor.RedColor, 3);
}
