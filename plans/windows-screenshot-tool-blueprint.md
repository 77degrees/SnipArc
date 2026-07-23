# Windows Screenshot Tool — Product Requirements and Build Blueprint

Status: Implementation-ready; adversarial review incorporated
Prepared: 2026-07-20
Product name: SnipArc

## Documentation map

This blueprint is the authoritative implementation sequence and remains self-contained for cold-start execution. Focused documentation provides stable product and technical references:

- [Project overview](../README.md)
- [Product requirements](../docs/requirements.md)
- [Architecture](../docs/architecture.md)
- [Security and privacy](../docs/security-and-privacy.md)
- [Testing strategy](../docs/testing.md)
- [Decision log](../docs/decisions.md)

When implementation changes behavior, update the relevant focused document and this blueprint in the same change. Requirement IDs are defined in `docs/requirements.md` and should remain stable after code or tests reference them.

## 1. Objective

Build a fast, privacy-conscious Windows screenshot utility around a direct capture workflow:

1. Press a global hotkey.
2. Drag to select a screen region.
3. Adjust or annotate the selection in place.
4. Copy, save, print, or explicitly upload it.

The deliverables are:

- `<AppName>.exe`: the installed desktop application (supporting runtime files may live beside it).
- `<AppName>-Setup-x64.exe`: a normal Windows installer with upgrade and uninstall support.
- Optional upload service: a separately deployed service that returns shareable URLs.

Use original product branding, original visual assets, and documented Windows platform interfaces.

## 2. Product Findings

### Workflow baseline

The product baseline is:

- The Print Screen key launches area capture.
- A region can be copied to the Windows clipboard.
- Local save formats are PNG, JPG, and BMP.
- A selected image can be uploaded and shared by URL.
- Optional upload can return an explicitly shared URL.
- Keyboard shortcuts cover copy, quick save, fullscreen selection, and cancellation.
- Any hosted sharing feature must state its visibility, retention, and deletion behavior clearly.
- The Windows application ships through a normal per-user installer with upgrade and uninstall support.

### Product inference

The essential appeal is not merely taking a screenshot; Windows already does that. The differentiator is a low-friction, in-place workflow with immediate annotation and output actions. Therefore, activation latency, precise selection, and a compact overlay matter more than a large editor or account system.

### Product safeguards

- Local-only by default; no network request during capture, editing, copy, or save.
- Upload is an explicit action with a first-use privacy warning.
- Pixelate and irreversible opaque redaction are separate initial tools. Pixelation is visual obfuscation, not secure redaction.
- Custom upload endpoints are supported through a provider interface rather than coupling the client to one host.
- Mixed-DPI multi-monitor behavior is tested as a release gate.
- No account is required for the desktop MVP.

## 3. Recommended Scope

### MVP / P0 requirements

| Area | Requirement | Acceptance criterion |
|---|---|---|
| Process | Single-instance tray application | Starting a second instance activates the first; no duplicate tray icon or hotkey registration |
| Activation | Configurable global capture hotkey, default Print Screen | Hotkey works while the app is in the tray; a registration conflict produces a useful message and fallback shortcut |
| Capture | Snapshot the desktop before displaying the overlay | The overlay and its toolbars never appear in the resulting image |
| Displays | Windows 11 x64; multiple monitors; negative desktop coordinates; portrait displays; mixed 100–200% scaling | Pixel-accurate selection and output across the documented hardware test matrix |
| Selection | Click-drag rectangle, live width × height, move, resize handles, arrow-key nudging, fullscreen select, cancel | All operations preserve exact physical-pixel bounds |
| Editing | Pen, line, arrow, rectangle, highlighter, text, pixelate, opaque redaction, color, stroke width, undo/redo | Annotations render identically in preview, clipboard output, and saved/uploaded image; decoded redacted output contains only replacement pixels in the covered area |
| Clipboard | Copy the composited selection | Ctrl+C and the Copy button put a bitmap on the clipboard that pastes correctly into Paint, Outlook, Teams, browsers, and Office |
| Save | Save As plus quick-save | PNG and JPG are supported; BMP is included for broad application compatibility; quick-save uses the configured folder and collision-safe filename |
| Upload | Optional provider returns a share URL | Upload occurs only after explicit action; success copies the URL; cancel/retry/error states work |
| Tray | Capture, Open recent folder, Settings, About, Exit | All commands work without opening a permanent main window |
| Settings | Hotkeys, output folder, filename pattern, image format/JPEG quality, start at login, cursor inclusion, upload provider | Settings persist per user and survive upgrades |
| Installer | Per-user `Setup.exe`, upgrade-in-place, Start Menu shortcut, and clean uninstall | Standard user can install without admin; Programs & Features entry and silent uninstall are present; startup remains owned by the app setting |
| Privacy | No telemetry and no implicit upload | Network test shows zero outbound traffic except explicit upload or manual update check |

### P1 after MVP

- Active-window and full-monitor capture commands.
- Delayed capture.
- Local metadata history with thumbnail opt-in and retention controls.
- Print command and “open in editor” command.
- Magnifier and pixel color readout while selecting.
- Configurable annotation defaults and numbered step markers.
- Update notification from signed GitHub releases; user chooses when to download/install.
- ARM64 build after the x64 release is stable.

### P2 / explicitly out of initial scope

- Scrolling capture.
- Video/GIF recording.
- OCR and translation.
- Browser extensions.
- macOS/Linux versions.
- Team accounts, cloud galleries, comments, or social login.
- Capturing the Windows secure desktop, UAC prompts, DRM-protected video, or guaranteed exclusive-fullscreen game capture.
- A silent background auto-updater that installs without user confirmation.

These exclusions protect the core “hotkey → mark up → copy/share” experience from becoming a ShareX-sized product before it is reliable.

## 4. Nonfunctional Requirements

### Performance budgets

- Tray idle CPU: ≤ 0.1% average over a five-minute sample on the reference machine; no polling loop.
- Tray idle working set: target ≤ 100 MB on the reference Windows 11 machine.
- Hotkey-to-interactive overlay: p95 ≤ 300 ms on one 4K display; p95 ≤ 500 ms on three mixed-DPI displays.
- Pointer-to-preview latency during drawing: one rendered frame at 60 Hz under normal load.
- Copy/save of a 4K region: target ≤ 500 ms excluding user dialog time.
- Application must remain usable offline.
- “Pixel-correct” means exact geometry and dimensions. Golden-image color comparisons allow a documented codec/color-management tolerance; secure-redaction areas allow no tolerance from the replacement color.

### Reliability and security

- Dispose capture bitmaps and device contexts deterministically.
- Keep an unmodified source snapshot plus a non-destructive annotation model until export.
- Never write a temporary screenshot to disk unless the user saves, enables local history, or initiates upload.
- Store ordinary settings in `%LocalAppData%\<Publisher>\<AppName>\settings.json` using atomic replacement.
- Store upload credentials and deletion tokens in Windows Credential Manager or protect them with DPAPI; never keep them in plaintext settings, history, or logs.
- Logs must exclude image bytes, clipboard contents, upload URLs containing credentials, and selected text.
- Before enabling update notifications, require signed metadata, HTTPS with no downgrade redirect, monotonic version/channel rules, and Authenticode signer-identity pinning for the downloaded installer.
- Use cryptographically random, non-enumerable object identifiers and deletion tokens in any first-party upload service.

### Accessibility and UX

- Full keyboard path for capture, select, edit, output, and cancel.
- Visible focus indicators, high-contrast-compatible tool icons, and non-color-only selected states.
- Tooltips and accessible names for every toolbar action.
- Settings UI works with Narrator and at 200% text scaling.
- Overlay toolbars reposition to remain inside the working area near display edges.

## 5. Architecture Decision

### Client stack

- Language/runtime: C# 14 on [.NET 10 LTS](https://dotnet.microsoft.com/en-us/platform/support/policy), supported through November 2028.
- UI: [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/), because it is Windows-native, supports custom transparent windows and vector drawing, and avoids a browser runtime.
- Target: Windows 11 x64 first. Publish self-contained so end users do not separately install .NET. Let Inno Setup package the publish folder into one downloadable installer EXE; evaluate .NET single-file publishing only after startup and native-dependency smoke tests prove it beneficial.
- Installer: [Inno Setup](https://jrsoftware.org/isinfo.php), producing one signed Setup EXE with per-user install, upgrade, shortcuts, startup option, silent switches, and uninstall.
- Tests: xUnit for domain/infrastructure tests plus a small Windows-only integration harness for capture, clipboard, hotkeys, and installer verification.

### Client components

```text
AppHost / Tray / Single Instance
        |
        +-- HotkeyService ------ Win32 RegisterHotKey
        +-- CaptureCoordinator
        |       +-- IScreenCaptureBackend
        |       +-- DisplayTopology / DPI mapping
        |       +-- Overlay windows
        |
        +-- Selection + Annotation domain model
        |       +-- Editor tools
        |       +-- Undo/redo command stack
        |       +-- Compositor / encoders
        |
        +-- Output services
        |       +-- Clipboard
        |       +-- Local file
        |       +-- IUploadProvider
        |
        +-- Settings / Credentials / Optional history
```

### Capture approach

Use an abstraction from day one:

```csharp
public interface IScreenCaptureBackend
{
    CaptureSession CaptureVirtualDesktop(DisplayTopology topology, bool includeCursor);
}
```

Start with a GDI/BitBlt backend because it can snapshot the virtual desktop immediately without showing the Windows system picker. The coordinator captures before any overlay is visible, then presents one borderless topmost overlay per monitor. All selection state is stored in physical virtual-desktop pixels, not WPF device-independent units.

Add a DXGI Desktop Duplication backend only if beta testing proves the GDI backend inadequate for HDR or borderless games. `Windows.Graphics.Capture` is useful for user-selected window/display capture, but its standard secure picker is not the desired one-hotkey region workflow.

Use Per-Monitor V2 DPI awareness. Microsoft specifically recommends PMv2 for modern desktop apps and notes that desktop frameworks require deliberate mixed-DPI handling:

- [High-DPI desktop development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Windows screen capture API](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)

### Annotation and export model

- `CaptureDocument`: immutable source bitmap, selection rectangle, ordered annotations.
- Annotation records: freehand path, line, arrow, rectangle, highlighter, text, pixelate, and opaque redaction.
- Editor actions use a command stack for undo/redo.
- The overlay renders a fast preview; a separate compositor renders final physical pixels for copy/save/upload.
- Encoders use Windows/WPF codecs for PNG, JPEG, and BMP to minimize third-party dependencies.
- Pixelation must be flattened into final pixels. Opaque redaction is a separate tool that replaces every covered output pixel with an opaque chosen color; it must never be implemented as blur, transparency, or a removable metadata layer.

### Upload contract

Keep upload out of the capture core:

```csharp
public interface IUploadProvider
{
    Task<UploadResult> UploadAsync(Stream image, UploadMetadata metadata,
                                   IProgress<double> progress,
                                   CancellationToken cancellationToken);
}

public sealed record UploadResult(Uri ViewUrl, string? DeleteToken, DateTimeOffset? ExpiresAt);
```

Ship the client first with either:

1. A documented generic HTTP provider for a user-owned endpoint; or
2. A small first-party service deployed separately.

Use only documented upload-provider APIs. Require HTTPS for every non-loopback provider, reject HTTPS-to-HTTP redirects, and never forward authorization to a different origin. Use normal Windows certificate validation without a bypass. If a first-party service is built, also require size/type validation, rate limiting, randomized object IDs, deletion support, configurable expiration, and an explicit statement that anyone with the URL can view the image.

## 6. Repository Layout

```text
/
  README.md
  LICENSE
  Directory.Build.props
  <AppName>.slnx
  src/
    AppName.App/                  # WPF startup, tray, settings windows
    AppName.Core/                 # selection, annotations, commands, interfaces
    AppName.Windows/              # capture, DPI, hotkeys, clipboard, credentials
    AppName.Upload/               # optional provider implementations
  tests/
    AppName.Core.Tests/
    AppName.Windows.Tests/
    AppName.IntegrationTests/
  installer/
    AppName.iss                   # Inno Setup source
  tools/
    SmokeTest/                    # manual/integration test harness
  docs/
    architecture.md
    privacy.md
    test-matrix.md
    release.md
  plans/
    windows-screenshot-tool-blueprint.md
  .github/workflows/
    ci.yml
    release.yml
```

## 7. Construction Plan

Each numbered step is intended to fit one focused PR after the repository is initialized. Do not begin a dependent step until the prior step's exit criteria pass.

### Step 1 — Product shell and engineering baseline

Dependencies: none
Estimated effort: 1–2 days
Rollback: delete the generated solution; no user data exists.

Context: The current directory is empty and is not a Git repository. Establish project identity and guardrails before capture code.

Tasks:

- Select original app and publisher names; run a trademark/name search before public distribution.
- Initialize Git, create the solution/projects above, choose a license, and add formatting/analyzer rules.
- Target `net10.0-windows`, x64, nullable references, warnings-as-errors in CI, and Per-Monitor V2 application manifest.
- Add unit-test projects and Windows GitHub Actions CI for restore/build/test. Reserve clipboard, tray, hotkey, overlay, DPI, and installer UI tests for a dedicated interactive Windows VM runner; do not claim those pass in service/session-0 CI.
- Add architecture, privacy, threat-boundary, and release documentation stubs.
- Record supported OS as Windows 11 x64; reject unsupported OS with a readable installer message.

Verification:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

Exit criteria: clean release build and tests on a fresh Windows runner; version metadata appears in the built EXE.

### Step 2 — Tray host, single instance, settings, and hotkeys

Dependencies: Step 1
Estimated effort: 2–3 days
Rollback: disable hotkey/settings services while preserving the project shell.

Context: A screenshot utility spends most of its life in the tray. This step establishes lifecycle behavior without capture complexity.

Tasks:

- Implement single-instance coordination with a named mutex and local activation signal scoped to the current user/session. Include the user SID in object names, use restrictive ACLs, validate activation messages, and bound message sizes.
- Add tray icon/menu, clean shutdown, Settings/About windows, and no permanent taskbar window.
- Wrap `RegisterHotKey`/`UnregisterHotKey`; default to Print Screen and support a configurable modifier/key combination.
- Detect registration failure and recommend a fallback without modifying Windows Snipping Tool settings automatically.
- Implement schema-versioned JSON settings with validation, defaults, migration, and atomic writes.
- Implement opt-in start-at-login using one quoted current-user Run-key value owned by the app; remove stale paths on startup and remove the value on user toggle/uninstall. The installer must not create a second startup mechanism.

Tests: settings round trip/migration/corruption recovery; mutex behavior; hotkey register/unregister/conflict; startup entry creation/removal.

Exit criteria: app can idle in tray, survive restart, enforce one instance, and reliably invoke a placeholder capture command from the global hotkey.

### Step 3 — Display topology, snapshot, and selection overlay

Dependencies: Step 2
Estimated effort: 4–6 days
Rollback: retain the tray shell and replace capture command with a diagnostic notice.

Context: This is the highest-risk step. Coordinate spaces must be correct before annotation is added.

Tasks:

- Enumerate monitors, physical bounds, work areas, orientation, DPI, and virtual-desktop origin.
- Implement deterministic Win32 handle wrappers and GDI BitBlt capture using `SRCCOPY | CAPTUREBLT` behind `IScreenCaptureBackend`; test layered-window behavior.
- Capture all monitors before creating overlays; optionally composite the visible cursor using `GetCursorInfo`/`GetIconInfo`, respecting its hotspot and negative-origin monitor coordinates.
- Create one topmost borderless overlay per monitor while maintaining one physical-pixel selection model.
- Implement dimming, crosshair, drag selection, live dimensions, edge/corner resize, move, arrow-key nudge, Ctrl+A fullscreen, and Esc cancel.
- Keep toolbars inside visible work areas and recover cleanly from display changes mid-capture.
- Ensure a capture session cannot be opened twice concurrently.

Automated tests: coordinate conversion, negative origins, mixed DPI, selection normalization/clamping, monitor hot-plug cancellation, bitmap crop boundaries.

Manual gate: the full display matrix in Section 8 must pass before Step 4.

Exit criteria: copied diagnostic crops exactly match the selected physical pixels on all test configurations; no overlay pixels leak into output.

### Step 4 — In-place editor and non-destructive compositor

Dependencies: Step 3
Estimated effort: 5–7 days
Rollback: disable failed tools individually; region capture/copy remains usable.

Context: Keep editor state independent of WPF controls so final exports and tests use the same model.

Tasks:

- Implement annotation records and editor commands for pen, line, arrow, rectangle, highlighter, text, pixelate, opaque redaction, color, and width.
- Add selection hit-testing, pointer capture, keyboard focus, undo/redo, tooltips, accessible names, and toolbar repositioning.
- Render preview efficiently, then render final output through the independent compositor.
- Decide and document annotation scaling semantics when the selection is resized after drawing; recommended behavior is to lock source bounds when the first annotation is created.
- Add golden-image tests with tolerances for every tool and combined layers.

Exit criteria: preview and exported pixels meet the documented golden-image tolerance; undo/redo is deterministic; decoded opaque-redaction regions contain only replacement pixels and cannot be recovered by hiding a layer or reading metadata.

### Step 5 — Clipboard, local save, and output workflow

Dependencies: Step 4
Estimated effort: 2–4 days
Rollback: fall back to PNG-only Save As while preserving capture/edit.

Context: Copy is the primary happy path; save is the durable local path. Neither may depend on the network.

Tasks:

- Implement clipboard output with retry/backoff for temporarily locked clipboard access.
- Implement PNG, JPEG, and BMP encoders; expose JPEG quality.
- Implement Save As and atomic quick-save with filename templates such as `Screenshot_yyyy-MM-dd_HH-mm-ss` and collision suffixes.
- Remember only the last successful folder; handle missing/network/OneDrive paths gracefully.
- Add success feedback that does not steal focus and a command to open the containing folder.
- Define whether completing Copy/Save closes the overlay; default to close, with a “keep open after action” setting later if demanded.

Tests: clipboard contention, Unicode/long paths, collisions, disk full/access denied, encoder dimensions and metadata, no unintended temp files.

Exit criteria: paste tests pass in common Windows apps; repeated quick-saves never overwrite an existing file unless explicitly confirmed.

### Step 6 — Explicit upload and privacy controls

Dependencies: Steps 4–5
Estimated effort: 3–5 client days; add 4–8 days if building a hosted service
Rollback: ship local-only and hide upload controls without affecting capture/edit/save.

Context: Upload is a separate trust boundary and must never be required for local use.

Tasks:

- Implement `IUploadProvider`, progress, cancellation, timeout, retry classification, and URL clipboard copy.
- Show a one-time warning that share URLs are accessible to anyone who has them; show provider and retention policy.
- Enforce HTTPS for non-loopback endpoints, reject downgrade redirects, strip authorization on cross-origin redirects, and expose no certificate-validation override.
- Store provider credentials and deletion tokens securely; redact them from history, logs, and exceptions.
- Add metadata-only local recent-upload history with delete action if supported.
- If building the service, implement authenticated upload, MIME/content validation, maximum size, cryptographic IDs, rate limits, expiration, deletion token, abuse handling, and HTTPS-only responses.
- Create integration tests against a local fake HTTP server; never call production from CI.

Exit criteria: packet-level check confirms no network call before explicit upload; cancel and all server error classes leave the image available for local copy/save.

### Step 7 — Installer, upgrade, uninstall, and release pipeline

Dependencies: Steps 1–6; can begin after Step 2 using placeholder payload
Estimated effort: 3–5 days
Rollback: continue distributing internal ZIP builds while fixing installer issues.

Context: The user-facing artifact is an installer EXE, not a raw development build.

Tasks:

- Publish `win-x64` self-contained and package the complete publish folder. Optionally evaluate Microsoft's [single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) only if cold-start, extraction, and native-asset tests pass.
- Author Inno Setup for non-admin per-user install under `%LocalAppData%\Programs\<AppName>`.
- Create Start Menu shortcut, optional desktop shortcut, Programs & Features metadata, icon, version, publisher/support URLs, and clean uninstall. Startup is controlled only by the application setting.
- Ensure setup asks the running tray app to exit before replacing files, removes the known startup value during uninstall, and preserves user settings on normal upgrade/uninstall unless “remove settings” is explicitly selected.
- Add `/SILENT` and `/VERYSILENT` install support and documented uninstall command for managed deployment.
- Build reproducible unsigned inputs plus provenance in CI. On signed version tags, test those inputs, sign the app EXE, build and sign the installer, timestamp signatures, generate SHA-256 checksums, and publish the exact tested signed artifact hash. Do not promise bit-for-bit reproduction of timestamped signed binaries.
- Use one consistent RSA-based signing identity. For public non-Store distribution, evaluate Azure Artifact Signing; Microsoft currently lists it around $9.99/month for eligible US individuals and warns that initial SmartScreen reputation still takes time.
- Consider a WinGet manifest only after stable HTTPS-hosted signed releases exist.

Release artifacts:

```text
<AppName>-Setup-1.0.0-x64.exe
<AppName>-1.0.0-x64.zip        # optional portable/internal build
SHA256SUMS.txt
SBOM.spdx.json
```

Exit criteria: clean install, upgrade from the prior version, same-version reinstall, interrupted-install recovery, silent install, and uninstall are tested in fresh Windows Sandbox/VM snapshots; signed files validate with `Get-AuthenticodeSignature`. No MSI-style transactional repair or rollback is promised.

### Step 8 — Hardening, beta, and 1.0 release gate

Dependencies: Steps 1–7
Estimated effort: 5–10 days plus beta observation
Rollback: remain in prerelease and ship no automatic update prompt.

Tasks:

- Run the hardware/OS matrix and 100-capture soak tests on an interactive VM/physical session; inspect GDI/user handle counts and memory after each session. Net handle growth after garbage collection and idle stabilization must be zero across the final 20 captures.
- Test RDP reconnect, sleep/resume, Explorer restart, monitor dock/undock, clipboard contention, network loss during upload, and app crash recovery.
- Run dependency vulnerability scanning, static analysis, Defender scan, installer reputation checks, and privacy traffic capture.
- Have beta users validate Outlook/Teams/Slack/Discord/Office paste workflows and hotkey conflicts with Snipping Tool, OneDrive, Logitech utilities, ShareX, and Greenshot.
- Tune performance against Section 4 budgets and document any known capture limitations.
- Complete user documentation: installation, hotkey conflicts, privacy, uploads, support bundle, uninstall, and update process.

Exit criteria: no open P0 defects; no screenshot data in logs/temp storage; all release tests pass from the exact signed installer hash that will be published.

## 8. Required Test Matrix

| Configuration | Minimum coverage |
|---|---|
| One 1920×1080 monitor at 100% | all automated/manual happy paths |
| One 4K monitor at 150% and 200% | selection precision, tool layout, performance |
| Two monitors at 100%/150% | each monitor and a selection crossing the boundary |
| Secondary monitor left/above primary | negative virtual coordinates |
| Landscape + portrait | bounds, toolbar placement, annotation |
| HDR on/off where available | color/output comparison and documented limits |
| Windows 11 supported production release | installer, app, hotkeys, capture, uninstall; record exact OS build, GPU/driver, DPI topology, and installer SHA-256 with results |
| Standard user account | per-user install and all app functions without elevation |
| RDP session | capture or clear documented limitation; reconnect stability |
| Windows Sandbox/fresh VM | clean installer, upgrade, uninstall, no prerequisites |

Every release smoke test must verify:

1. Hotkey registration and conflict message.
2. Region capture and pixel dimensions.
3. Every annotation tool and undo/redo.
4. Clipboard paste and all save formats.
5. Explicit upload success/cancel/failure if upload is enabled.
6. No overlay in output and no screenshot residue in temp/log folders.
7. Installer signature, prior-version upgrade, interrupted-install recovery, and uninstall.
8. After uninstall: no process, binary, shortcut, Programs & Features entry, or startup value remains; user settings remain only when the documented preserve option was selected.

## 9. Dependency and Parallelism Map

```text
Step 1
  |
Step 2 ----------- Step 7 installer skeleton
  |
Step 3
  |
Step 4
  +------ Step 5 local outputs
  +------ Step 6 upload boundary
             |
          Step 7 final packaging
             |
          Step 8 hardening/release
```

After Step 4, local output work and upload work can proceed in parallel because they share only the compositor contract. Installer work can start early but cannot finish until the final payload and configuration behavior are stable.

## 10. Key Decisions Needed Before Coding

Recommended defaults are in bold.

1. Distribution audience: **personal/open-source public release**, internal enterprise, or commercial.
2. Upload for 1.0: **generic user-owned endpoint or local-only**, versus operating a public hosting service.
3. Capture scope: **Windows 11 x64**, with ARM64 later; do not promise consumer Windows 10 support on .NET 10 in 2026.
4. Install scope: **per-user/no admin**; add all-users MSI only if enterprise deployment requires it.
5. Screenshot history: **metadata only and opt-in thumbnails**, never silently retain all captured pixels.
6. Branding/license: choose an original name, icon, and license before the first public installer.
7. Code signing: **unsigned internal alpha; RSA trusted signing before public stable release**.

## 11. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Mixed-DPI selection is offset or blurry | Physical-pixel domain model, PMv2 manifest, one overlay per monitor, mandatory matrix gate before editor work |
| Print Screen belongs to Snipping Tool/another app | Detect `RegisterHotKey` failure, offer configurable fallback, document Windows setting; never rewrite OS settings silently |
| WPF overlay appears in capture | Complete desktop snapshot before any overlay HWND is shown |
| HDR/exclusive games produce incorrect/black images | Document P0 limit; preserve capture-backend abstraction; prototype DXGI only after measured failures |
| Clipboard is locked | Bounded retry with useful error; keep capture session open so user can retry/save |
| Upload leaks sensitive information | Explicit action/warning, local-first behavior, deletion/expiration support, cryptographic IDs, no URL logging |
| Unsigned installer is blocked | Sign app and installer consistently, timestamp, publish hashes; consider Store MSIX later for friction-free reputation |
| Scope expands into a full media suite | Hold OCR, scrolling, recording, accounts, and browser extensions until core P0 quality gates pass |

## 12. Definition of Done for Version 1.0

Version 1.0 is complete only when:

- A new user can install from one signed `Setup.exe` without separately installing a runtime.
- The app launches to the tray, starts capture from a configurable hotkey, and handles hotkey conflicts clearly.
- Region selection and annotations are pixel-correct across the required monitor/DPI matrix.
- Copy and local save work fully offline; upload is optional and explicit.
- Upgrade preserves settings and uninstall removes binaries, shortcuts, startup registration, and uninstall metadata.
- No P0 bug, secret exposure, unintended screenshot persistence, or undisclosed network traffic remains.
- CI can reproduce unsigned inputs with provenance, then test and publish the exact signed installer hash, checksums, and SBOM.

## 13. Plan Mutation Protocol

- A step may be split when it exceeds one reviewable PR; preserve the same acceptance criteria.
- New features enter P1/P2 unless required to satisfy an existing P0 criterion.
- A capture backend may be replaced only behind `IScreenCaptureBackend` with the full display matrix rerun.
- An upload provider may be added without changing capture/editor projects.
- Record material scope/architecture changes in this file with date, reason, dependency impact, and altered release gate.
