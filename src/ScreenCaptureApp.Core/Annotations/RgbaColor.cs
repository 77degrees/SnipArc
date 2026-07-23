namespace ScreenCaptureApp.Core.Annotations;

/// <summary>A platform-neutral, non-premultiplied RGBA color.</summary>
public readonly record struct RgbaColor(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue)
{
    public static readonly RgbaColor RedColor = new(byte.MaxValue, 0, 0);
    public static readonly RgbaColor Yellow = new(byte.MaxValue, byte.MaxValue, 0);
    public static readonly RgbaColor Black = new(0, 0, 0);
    public static readonly RgbaColor White = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public bool IsOpaque => Alpha == byte.MaxValue;
}
