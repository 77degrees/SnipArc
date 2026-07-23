using System.Windows;
using ScreenCaptureApp.App;

namespace ScreenCaptureApp.App.Tests;

public sealed class WindowSuggestionSelectorTests
{
    [Fact]
    public void FindAtReturnsFirstContainingWindowInZOrder()
    {
        WindowSuggestion[] suggestions =
        [
            new("Front", new Rect(50, 50, 200, 200)),
            new("Back", new Rect(0, 0, 400, 400))
        ];

        var result = WindowSuggestionSelector.FindAt(suggestions, new Point(100, 100));

        Assert.Equal("Front", result?.Title);
    }

    [Fact]
    public void FindAtUsesHalfOpenWindowBounds()
    {
        WindowSuggestion[] suggestions = [new("Window", new Rect(10, 20, 100, 80))];

        Assert.NotNull(WindowSuggestionSelector.FindAt(suggestions, new Point(10, 20)));
        Assert.Null(WindowSuggestionSelector.FindAt(suggestions, new Point(110, 100)));
    }
}
