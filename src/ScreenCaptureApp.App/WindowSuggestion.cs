using System.Windows;
using Point = System.Windows.Point;

namespace ScreenCaptureApp.App;

internal sealed record WindowSuggestion(string Title, Rect Bounds);

internal static class WindowSuggestionSelector
{
    internal static WindowSuggestion? FindAt(IReadOnlyList<WindowSuggestion> suggestions, Point point)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        return suggestions.FirstOrDefault(suggestion =>
            point.X >= suggestion.Bounds.Left &&
            point.X < suggestion.Bounds.Right &&
            point.Y >= suggestion.Bounds.Top &&
            point.Y < suggestion.Bounds.Bottom);
    }
}
