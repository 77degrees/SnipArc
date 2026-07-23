using System.IO;
using Microsoft.Win32;

namespace ScreenCaptureApp.Windows.Startup;

public sealed class StartupRegistrationService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    public StartupRegistrationService(string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        _valueName = valueName;
    }

    public bool IsEnabled(string executablePath, string arguments = "--startup")
    {
        string expected = FormatCommand(executablePath, arguments);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(_valueName) as string, expected, StringComparison.Ordinal);
    }

    public void Enable(string executablePath, string arguments = "--startup")
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(_valueName, FormatCommand(executablePath, arguments), RegistryValueKind.String);
    }

    public void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    public static string FormatCommand(string executablePath, string arguments = "--startup")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("The startup executable path must be absolute.", nameof(executablePath));
        }

        if (executablePath.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("The startup executable path cannot contain a quote.", nameof(executablePath));
        }

        string trimmedArguments = arguments.Trim();
        return trimmedArguments.Length == 0
            ? $"\"{executablePath}\""
            : $"\"{executablePath}\" {trimmedArguments}";
    }
}
