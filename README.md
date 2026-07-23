# SnipArc

A fast, local-only Windows screenshot and annotation tool built for low-friction capture, precise selection, immediate markup, and private output.

![SnipArc icon](assets/app-icon.png)

> Status: working `0.1.0-alpha` for Windows 11 x64. The current installer is unsigned and intended for local testing.

## Install the alpha

Run [SnipArc-Setup-x64.exe](artifacts/installer/SnipArc-Setup-x64.exe). It installs per user under `%LocalAppData%\Programs\ScreenCaptureApp` and does not require administrator rights. The internal folder and executable names intentionally remain `ScreenCaptureApp` during the alpha so existing installations and settings upgrade in place.

Because this alpha is not code-signed, Windows SmartScreen may show an unknown-publisher warning. Do not distribute it as a trusted public release until a signing identity is configured.

## Use it

1. Start **SnipArc** from the Start menu. It stays in the notification area.
2. Press **Print Screen**. If Windows or another app owns that shortcut, choose `Ctrl+Shift+4` or `Ctrl+Shift+S` in Settings. To use the familiar Snipping Tool shortcut, enable **Use Windows + Shift + S for this app instead of Snipping Tool**.
3. Hover over a window and click to select the whole window, or drag to select a custom area.
4. Resize from the eight handles, move the selection, or annotate it.
5. Copy, quick-save, save as PNG/BMP, or press **Esc** to cancel.

The selection supports directional resize cursors, one-pixel arrow-key nudging, ten-pixel `Shift` nudging, live pixel dimensions, undo/redo, and keyboard tool shortcuts shown in each tooltip.

## Implemented features

- Single-instance notification-area application with global hotkey handling and an optional, reversible Windows + Shift + S override.
- Physical-pixel GDI capture with optional mouse pointer composition.
- Active-monitor selection overlay with mixed-DPI-aware positioning.
- Smart whole-window detection with hover preview and click-to-select.
- Pen, line, arrow, rectangle, highlighter, text, pixelation, and opaque redaction.
- Clipboard copy and local PNG, JPG, or BMP saving.
- Exact-pixel opaque redaction applied last during export.
- Lossless-only export enforcement when a capture contains opaque redaction.
- Configurable shortcuts, capture folder, quick-save format, pointer inclusion, notifications, and startup preference.
- Per-user, self-contained Inno Setup installer and clean uninstall.
- No accounts, uploads, analytics, telemetry, or hidden screenshot history.

## Build from source

Requirements:

- Windows 11 x64
- .NET SDK `10.0.302` or the compatible SDK selected by `global.json`
- Inno Setup 6 only when building the installer

Build and test:

```powershell
dotnet restore ScreenCaptureApp.slnx
dotnet build ScreenCaptureApp.slnx -c Release --no-restore
dotnet test ScreenCaptureApp.slnx -c Release --no-build --no-restore
```

Publish the self-contained app and installer:

```powershell
.\eng\build-release.ps1 -BuildInstaller
```

Outputs:

- `artifacts/app/win-x64/ScreenCaptureApp.exe` — unpackaged self-contained app.
- `artifacts/installer/SnipArc-Setup-x64.exe` — per-user installer.

## Project layout

| Path | Responsibility |
|---|---|
| `src/ScreenCaptureApp.Core` | Geometry, selection, annotations, editor commands, and history |
| `src/ScreenCaptureApp.Windows` | Capture, displays, hotkeys, clipboard, settings, startup, and single-instance IPC |
| `src/ScreenCaptureApp.App` | WPF tray application, overlay, toolbars, export workflow, and settings UI |
| `tests/` | Core, Windows-infrastructure, and export-safety tests |
| `installer/` | Inno Setup definition and installer notes |
| `eng/` | Repeatable release build script |
| `docs/` | Requirements, architecture, privacy, testing, and decisions |
| `plans/` | Original implementation blueprint |

## Current alpha limitations

- Capture and selection are limited to one monitor at a time; cross-monitor selection is planned.
- Windows spanning more than one monitor are not offered for whole-window selection in this alpha.
- HDR, protected content, and exclusive-fullscreen applications may not capture as expected with the initial GDI backend.
- The installer and binaries are not code-signed.
- Upload and link sharing are intentionally absent. The alpha never sends captured pixels over the network.
- Visual and interaction testing across the full mixed-DPI hardware matrix remains a release gate.

## Documentation

- [Documentation index](docs/README.md)
- [Product requirements](docs/requirements.md)
- [Architecture](docs/architecture.md)
- [Security and privacy](docs/security-and-privacy.md)
- [Testing strategy](docs/testing.md)
- [Decision log](docs/decisions.md)
- [Brand and naming](docs/branding.md)
- [Implementation blueprint](plans/windows-screenshot-tool-blueprint.md)
- [Changelog](CHANGELOG.md)

## Platform references

Platform and packaging decisions use Microsoft documentation for [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy), [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/), [high-DPI desktop applications](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows), [Windows screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture), and [Windows code signing](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).
