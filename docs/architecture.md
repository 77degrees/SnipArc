# Architecture

Status: Proposed implementation architecture
Last reviewed: 2026-07-22

## 1. Architecture goals

- Keep the hotkey-to-overlay path short and deterministic.
- Keep capture, editing, and local outputs independent of network availability.
- Represent geometry in physical desktop pixels to avoid mixed-DPI drift.
- Keep source pixels immutable and annotations non-destructive until export.
- Isolate Win32 resources behind narrow, testable interfaces.
- Allow the capture backend and upload providers to change without rewriting the editor.
- Package every required runtime dependency inside one downloadable installer.

## 2. Technology baseline

| Concern | Selection | Reason |
|---|---|---|
| Language/runtime | C# 14 and .NET 10 LTS | Current supported Windows desktop runtime through November 2028 |
| UI | WPF | Mature Windows desktop UI, custom borderless overlays, vector rendering, native message integration |
| Process model | One per-user tray process | Global hotkey ownership and fast capture activation |
| Capture v1 | GDI BitBlt with `SRCCOPY | CAPTUREBLT` | Immediate virtual-desktop snapshot without the Windows system picker |
| DPI model | Per-Monitor V2 | Physical-pixel accuracy on mixed-scale displays |
| Encoding | WPF/Windows bitmap codecs | PNG/JPEG/BMP support without an image-library dependency |
| Installer | Inno Setup | Single Setup EXE, per-user install, upgrade/uninstall, silent switches |
| Configuration | Versioned JSON under LocalAppData | Human-inspectable, easy migration, no database dependency |
| Secret storage | Credential Manager or DPAPI | Avoid plaintext upload credentials and deletion tokens |

## 3. Solution boundaries

```text
src/
  AppName.App/          WPF startup, tray, overlay/settings windows
  AppName.Core/         Selection, annotations, commands, interfaces
  AppName.Windows/      Capture, DPI, hotkeys, clipboard, credentials
  AppName.Upload/       Optional upload-provider implementations

tests/
  AppName.Core.Tests/
  AppName.Windows.Tests/
  AppName.IntegrationTests/
```

Dependency direction:

```text
AppName.App ---------> AppName.Core
     |                      ^
     +--> AppName.Windows --+
     |
     +--> AppName.Upload ---+

AppName.Core has no dependency on WPF windows, HTTP clients, registry,
clipboard, installer, or a concrete screen-capture API.
```

## 4. Runtime components

```text
AppHost
  |
  +-- SingleInstanceService
  +-- TrayService
  +-- HotkeyService
  +-- SettingsService
  +-- CaptureCoordinator
          |
          +-- DisplayTopologyService
          +-- IScreenCaptureBackend
          +-- OverlayManager
          +-- CaptureDocument
          |      +-- Selection
          |      +-- Annotation collection
          |      +-- Undo/redo command stack
          +-- ImageCompositor
          +-- OutputCoordinator
                 +-- ClipboardService
                 +-- FileOutputService
                 +-- IUploadProvider
```

### AppHost

Owns process startup and shutdown. It is responsible for establishing the single-instance boundary before initializing hotkeys or the tray icon.

### CaptureCoordinator

Owns one capture session at a time. It freezes display topology, captures source pixels, creates overlays, owns session cancellation, and transfers the final document to output services.

### CaptureDocument

Contains all state required to reproduce output:

- Immutable captured source bitmap(s).
- Physical-pixel selection rectangle.
- Ordered immutable annotation records.
- Current editor/tool settings.
- Undo/redo command history.

The document does not contain a provider credential, output path, WPF control, HWND, HDC, or HTTP client.

### ImageCompositor

Produces the authoritative output bitmap. Clipboard, save, and upload must all use this component. The overlay preview may use optimized WPF rendering, but it cannot be the source of saved pixels.

## 5. Capture sequence

```text
User       Hotkey      Coordinator      Capture backend      Overlay
 |            |             |                  |                 |
 |--press---->|             |                  |                 |
 |            |--activate-->|                  |                 |
 |            |             |--read topology-->|                 |
 |            |             |--snapshot------->|                 |
 |            |             |<--source pixels--|                 |
 |            |             |--create overlays----------------->|
 |<-----------------------------interactive selection/editor----|
 |--copy/save/upload-------------------------------------------->|
 |            |             |<--final document------------------|
 |            |             |--composite/export                  |
```

Required ordering invariant: no overlay HWND may become visible until the desktop snapshot is complete.

## 6. Coordinate system

Coordinate mistakes are the largest technical risk. The domain model uses one coordinate space:

> Signed physical pixels relative to the virtual-desktop origin.

This permits monitors to exist left of or above the primary display. WPF device-independent units are used only at the UI boundary.

### Required invariants

- Monitor bounds, source bitmaps, selection bounds, and annotation geometry are normalized to physical pixels.
- Conversion between WPF units and physical pixels occurs in one adapter, not throughout editor code.
- Every overlay knows its monitor's physical origin, DPI, and clipping bounds.
- A cross-monitor selection remains one document rectangle; each overlay displays only its clipped portion.
- Output dimensions equal the normalized selection width and height exactly.
- Display topology is immutable during one capture session. A topology change cancels the session safely.

## 7. Capture backend

```csharp
public interface IScreenCaptureBackend
{
    CaptureSession CaptureVirtualDesktop(
        DisplayTopology topology,
        bool includeCursor);
}
```

### GDI implementation

- Read virtual-screen bounds through Win32 display APIs.
- Copy with BitBlt using `SRCCOPY | CAPTUREBLT`.
- Wrap HDC, HBITMAP, HICON, and related resources in deterministic safe handles.
- Use `GetCursorInfo` and `GetIconInfo` when cursor capture is enabled.
- Apply the cursor hotspot and signed virtual-screen coordinates correctly.
- Snapshot before creating overlays.

### Known limitations

GDI may not correctly capture every HDR, protected, or exclusive-fullscreen surface. Do not add ad hoc hooks to work around individual applications. If beta evidence justifies another backend, implement DXGI Desktop Duplication behind the existing interface and rerun the complete display matrix.

`Windows.Graphics.Capture` remains an option for future user-selected window/display modes, but its standard secure picker does not match the one-hotkey region workflow.

## 8. Overlay and editor model

Use one topmost borderless overlay per monitor with one shared `CaptureDocument`.

Overlay responsibilities:

- Draw frozen source pixels and dimming outside the selection.
- Preview the topmost eligible window from the pre-overlay z-order snapshot and convert a click into its physical-pixel bounds.
- Convert pointer input at the UI boundary.
- Display selection handles, dimensions, tools, and keyboard focus.
- Clip shared selection/annotation preview to the overlay monitor.
- Reposition toolbars within the monitor work area.

The window detector enumerates visible top-level windows before the overlay exists. It preserves `EnumWindows` z-order, uses DWM extended-frame bounds when available, and excludes the shell, minimized, cloaked, tool, untitled, zero-sized, and current-process windows. The active-monitor alpha only offers candidates fully contained by that monitor; dragging beyond the click threshold always switches to manual region selection.

Core responsibilities:

- Normalize, move, resize, and clamp selection geometry.
- Store immutable annotation records.
- Execute editor commands and undo/redo.
- Define annotation scaling semantics.

Recommended rule: once the first annotation is committed, lock source selection bounds. This avoids ambiguous scaling and cropping of existing annotations.

## 9. Annotation records

Recommended discriminated record types:

- `FreehandPathAnnotation`
- `LineAnnotation`
- `ArrowAnnotation`
- `RectangleAnnotation`
- `HighlightAnnotation`
- `TextAnnotation`
- `PixelateAnnotation`
- `OpaqueRedactionAnnotation`

Pixelation and redaction are not synonyms:

- Pixelation is an irreversible visual transform but can leave content inferable.
- Opaque redaction replaces all covered final pixels with a fully opaque color.

The compositor must flatten both into exported pixels and omit editor-layer metadata from saved images.

## 10. Output architecture

```text
CaptureDocument
      |
ImageCompositor
      |
      +-- Bitmap -> ClipboardService
      +-- Bitmap -> Encoder -> Atomic file write
      +-- Bitmap -> Encoder -> IUploadProvider
```

### Clipboard

- Use the STA UI thread or a dedicated STA service.
- Retry temporary clipboard contention with a short bounded backoff.
- Keep the capture session open if copy fails.

### Files

- Encode to a temporary file in the destination directory and atomically replace/move when complete.
- Never silently overwrite an existing file.
- Treat unavailable OneDrive, network, or removable destinations as recoverable errors.
- Do not use a global temp directory for screenshot pixels.

### Upload contract

```csharp
public interface IUploadProvider
{
    Task<UploadResult> UploadAsync(
        Stream image,
        UploadMetadata metadata,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}

public sealed record UploadResult(
    Uri ViewUrl,
    string? DeleteToken,
    DateTimeOffset? ExpiresAt);
```

Providers cannot depend on overlay types or mutate the capture document. Security rules are defined in [Security and privacy](security-and-privacy.md).

## 11. Process lifecycle and IPC

- Scope the mutex and activation IPC to the current user and interactive session.
- Include the current user SID in object names.
- Apply restrictive ACLs.
- Validate activation message type and size.
- A second process sends only an activation command, then exits.
- No image pixels, file paths, URLs, credentials, or arbitrary command lines cross the activation channel.

## 12. Settings and migrations

Settings path:

```text
%LocalAppData%\<Publisher>\<AppName>\settings.json
```

Settings document requirements:

- Explicit schema version.
- Safe defaults for every field.
- Validation before applying hotkeys, paths, or upload settings.
- Atomic write through a sibling temporary file.
- Backup/recovery from malformed JSON.
- Forward migration one supported version at a time.
- No credentials or deletion tokens.

The canonical envelope uses camelCase (`schemaVersion` and `settings`). The loader compares envelope names case-insensitively so PascalCase files written by early alpha builds remain readable. This backward-compatibility rule is regression-tested because rejecting the older casing makes every fresh process appear to reset user preferences.

SnipArc is the public display name. The following internal identifiers remain stable through the alpha upgrade path: `%LocalAppData%\ScreenCaptureApp`, `ScreenCaptureApp.exe`, the `ScreenCaptureApp` HKCU Run value, the single-instance identifier, and the Inno Setup AppId. Changing these identifiers requires an explicit settings/install migration rather than a search-and-replace rename.

Startup preference uses one quoted HKCU Run value owned by the app. The installer removes that known value on uninstall but does not create a competing startup mechanism.

## 13. Packaging

Publish `win-x64` as a self-contained folder. Inno Setup packages the complete folder into one downloadable `Setup.exe` and installs it per user under:

```text
%LocalAppData%\Programs\<AppName>
```

Single-file .NET publishing is optional. It should be enabled only if native dependency, extraction, antivirus, and cold-start tests show no regression. The user-facing requirement is one installer EXE, not one physical file inside the installation directory.

## 14. Failure-handling rules

| Failure | Required behavior |
|---|---|
| Hotkey conflict | Explain and allow fallback; do not silently change Windows settings |
| Optional Windows + Shift + S override | A low-level keyboard hook suppresses only the S key event for that exact chord, dispatches capture asynchronously, and is removed immediately when disabled or on shutdown |
| Capture resource failure | Release acquired handles, close overlays, show actionable error |
| Display topology changes | Cancel current session and invite a retry |
| Clipboard locked | Bounded retry; keep document open on final failure |
| Save path unavailable | Preserve document and allow another destination |
| Upload canceled/timed out | Preserve document and allow retry/copy/save |
| Settings corrupt | Quarantine invalid file, use safe defaults, notify once |
| Second instance | Activate first process and exit |

## 15. Architecture fitness checks

- `AppName.Core` builds without WPF/Win32/HTTP references.
- Core geometry and editor commands are deterministic unit tests.
- Clipboard, capture, DPI, and upload implementations can be substituted with fakes.
- No output path bypasses `ImageCompositor`.
- No network dependency appears on the capture-to-local-save path.
- GDI/User handle soak tests show zero net growth.
- The exact install payload runs in a fresh Windows VM with no separate .NET installation.

## References

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [WPF documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [High-DPI desktop development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Windows screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)
- [Inno Setup](https://jrsoftware.org/isinfo.php)
