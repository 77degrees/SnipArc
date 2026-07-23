using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScreenCaptureApp.Windows.Policies;
using ComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace ScreenCaptureApp.App;

public partial class SettingsWindow : Window
{
    private readonly AppLocalSettings _original;

    internal SettingsWindow(AppLocalSettings settings, AppPolicy? policy = null)
    {
        InitializeComponent();
        _original = settings;
        FolderBox.Text = settings.CaptureFolder;
        SelectByTag(FormatBox, settings.QuickSaveFormat.ToString());
        SelectByTag(HotkeyBox, settings.Hotkey);
        QualitySlider.Value = settings.JpegQuality;
        StartupBox.IsChecked = settings.StartWithWindows;
        CursorBox.IsChecked = settings.IncludeCursor;
        NotificationsBox.IsChecked = settings.ShowNotifications;
        OverrideSnippingBox.IsChecked = settings.OverrideWindowsSnippingShortcut;
        TranslationEndpointBox.Text = settings.TranslationEndpoint ?? string.Empty;
        SelectByTag(TranslationLanguageBox, settings.TranslationTargetLanguage);

        if (policy?.ManagedCaptureFolder is not null)
        {
            FolderBox.IsReadOnly = true;
            FolderBox.ToolTip = "Managed by your organization";
            BrowseButton.IsEnabled = false;
        }
        if (policy?.ForceStartWithWindows is not null)
        {
            StartupBox.IsEnabled = false;
            StartupBox.ToolTip = "Managed by your organization";
        }
        if (policy?.DisableTranslation == true)
        {
            TranslationEndpointBox.IsEnabled = false;
            TranslationLanguageBox.IsEnabled = false;
            TranslationEndpointBox.ToolTip = "Translation is disabled by your organization";
        }
    }

    internal AppLocalSettings Settings { get; private set; } = new();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose where quick saves are stored",
            SelectedPath = Directory.Exists(FolderBox.Text) ? FolderBox.Text : _original.CaptureFolder,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK) FolderBox.Text = dialog.SelectedPath;
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityLabel is not null) QualityLabel.Text = $"{Math.Round(e.NewValue)}%";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var folder = Environment.ExpandEnvironmentVariables(FolderBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(folder) || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            MessageBox.Show(this, "Choose a valid capture folder.", "Invalid folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            FolderBox.Focus();
            return;
        }

        string? translationEndpoint = string.IsNullOrWhiteSpace(TranslationEndpointBox.Text)
            ? null
            : TranslationEndpointBox.Text.Trim();
        if (translationEndpoint is not null &&
            (!Uri.TryCreate(translationEndpoint, UriKind.Absolute, out Uri? endpoint) ||
             (endpoint.Scheme != Uri.UriSchemeHttps &&
              !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback))))
        {
            MessageBox.Show(this,
                "Use an HTTPS translation endpoint. HTTP is accepted only for a service running on this computer.",
                "Invalid translation endpoint", MessageBoxButton.OK, MessageBoxImage.Warning);
            TranslationEndpointBox.Focus();
            return;
        }

        Settings = _original with
        {
            CaptureFolder = Path.GetFullPath(folder),
            QuickSaveFormat = Enum.Parse<ImageFileFormat>(SelectedTag(FormatBox, "Png")),
            JpegQuality = (int)Math.Round(QualitySlider.Value),
            Hotkey = SelectedTag(HotkeyBox, "PrintScreen"),
            StartWithWindows = StartupBox.IsChecked == true,
            IncludeCursor = CursorBox.IsChecked == true,
            ShowNotifications = NotificationsBox.IsChecked == true,
            OverrideWindowsSnippingShortcut = OverrideSnippingBox.IsChecked == true,
            TranslationEndpoint = translationEndpoint,
            TranslationTargetLanguage = SelectedTag(TranslationLanguageBox, "en")
        };
        DialogResult = true;
    }

    private static string SelectedTag(ComboBox box, string fallback) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    private static void SelectByTag(ComboBox box, string tag)
    {
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase)) ?? box.Items[0];
    }
}
