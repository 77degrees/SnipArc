using Microsoft.Win32;

namespace ScreenCaptureApp.Windows.Policies;

public sealed record AppPolicy(
    bool DisableTranslation,
    string? ManagedCaptureFolder,
    bool? ForceStartWithWindows);

public static class AppPolicyProvider
{
    private const string PolicyPath = @"Software\Policies\77degrees\SnipArc";

    public static AppPolicy Load()
    {
        using RegistryKey? machine = Registry.LocalMachine.OpenSubKey(PolicyPath, writable: false);
        using RegistryKey? user = Registry.CurrentUser.OpenSubKey(PolicyPath, writable: false);
        return new AppPolicy(
            ReadDword(machine, user, "DisableTranslation") == 1,
            ReadString(machine, user, "CaptureFolder"),
            ReadNullableBoolean(machine, user, "StartWithWindows"));
    }

    private static int? ReadDword(RegistryKey? machine, RegistryKey? user, string name) =>
        ReadValue(machine, user, name) switch
        {
            int value => value,
            long value => checked((int)value),
            _ => null
        };

    private static bool? ReadNullableBoolean(RegistryKey? machine, RegistryKey? user, string name) =>
        ReadDword(machine, user, name) switch
        {
            0 => false,
            1 => true,
            _ => null
        };

    private static string? ReadString(RegistryKey? machine, RegistryKey? user, string name) =>
        ReadValue(machine, user, name) is string value && !string.IsNullOrWhiteSpace(value)
            ? Environment.ExpandEnvironmentVariables(value.Trim())
            : null;

    private static object? ReadValue(RegistryKey? machine, RegistryKey? user, string name) =>
        machine?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) ??
        user?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
}
