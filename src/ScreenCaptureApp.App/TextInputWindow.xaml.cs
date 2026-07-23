using System.Windows;

namespace ScreenCaptureApp.App;

public partial class TextInputWindow : Window
{
    public TextInputWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Input.Focus();
    }

    public string EnteredText => Input.Text;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = !string.IsNullOrWhiteSpace(Input.Text);
    }
}
