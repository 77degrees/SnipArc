# Testing Strategy

## Latest local alpha verification

Verified on 2026-07-22:

- Release build: zero warnings and zero errors.
- Automated tests: 84 passed (46 core, 34 Windows infrastructure, 4 app/export behavior).
- Self-contained `win-x64` publish and Inno Setup compilation succeeded.
- Silent per-user install, tray-process startup, second-instance handoff, capture overlay, and uninstall were exercised locally.
- The compact overlay and directional resize cursors were visually confirmed in an interactive Windows session.

This evidence validates the local development machine only. The full display, DPI, graphics, accessibility, upgrade, and clean-VM matrix below remains required for a public stable release.

Status: Required verification model for version 1.0
Last reviewed: 2026-07-22

## 1. Test objectives

Testing must prove more than “a screenshot appeared.” Version 1.0 requires evidence that:

- Selection maps to exact physical pixels across supported display layouts.
- Preview and output use consistent annotation geometry.
- Secure redaction cannot be undone from the exported file.
- Local workflows do not use the network or persist hidden image data.
- Native resources do not leak across repeated captures.
- Clipboard, tray, overlays, hotkeys, and installer behavior work in an interactive Windows session.
- The exact signed artifact being published passed the release suite.

## 2. Test layers

| Layer | Environment | Coverage |
|---|---|---|
| Core unit | GitHub-hosted Windows runner | Geometry, commands, settings validation, filename rules, upload policy logic |
| Windows integration | Dedicated interactive Windows VM | Hotkeys, clipboard, tray, overlays, registry, capture, DPI, installer UI |
| Golden image | CI plus interactive VM | Annotation/compositor pixels, encoders, redaction, color tolerances |
| HTTP integration | Local fake server | Upload success/failure/cancel, redirect, credential and timeout behavior |
| Installer | Fresh VM/Sandbox snapshots | Clean install, prior-version upgrade, reinstall, interruption recovery, silent switches, uninstall |
| Manual hardware | Physical/VM display matrix | Mixed DPI, HDR, multiple monitors, portrait, RDP, common paste targets |
| Security/privacy | Isolated VM with traffic/filesystem inspection | No implicit network, no pixel residue, no secret logging, signature validation |
| Soak/performance | Reference physical machine | Latency budgets, memory, CPU, GDI/User handles over repeated sessions |

Ordinary GitHub-hosted CI is not considered evidence for interactive desktop behavior. Windows services/session 0 cannot reliably validate clipboard, hotkeys, topmost WPF overlays, tray icons, or monitor DPI interaction.

## 3. Test projects

```text
tests/
  AppName.Core.Tests/
    Geometry/
    Annotations/
    Commands/
    Filenames/
    Settings/
  AppName.Windows.Tests/
    DisplayTopology/
    Capture/
    Clipboard/
    Hotkeys/
    Registry/
  AppName.IntegrationTests/
    Overlay/
    Upload/
    Installer/
    Privacy/
  GoldenImages/
    Inputs/
    Expected/
    Masks/
```

Keep production service calls out of every automated test. Upload tests use a local server under test control.

## 4. Core unit tests

### Geometry

Cover:

- Drag in all four directions.
- Zero-size selection rejection.
- Window-candidate filtering for visibility, minimization, DWM cloaking, tool windows, shell windows, process identity, titles, and empty bounds.
- Topmost-first hover selection and half-open window hit-testing.
- Normalization, clamp, move, resize, and handle hit-testing.
- Negative virtual-desktop origins.
- Monitor gaps and non-rectangular topologies.
- 100%, 125%, 150%, 175%, and 200% conversions.
- Cross-monitor selection clipping.
- Fullscreen selection on the active monitor.
- One-pixel keyboard nudge and modified increments.

Property tests should generate valid display topologies and assert:

- Normalized width/height are nonnegative.
- A clamped selection does not leave capture bounds.
- UI-to-physical-to-UI conversions stay within documented rounding tolerance.
- Output dimensions exactly equal the physical selection dimensions.

### Editor commands

For every annotation type:

- Create, commit, cancel, undo, and redo.
- Stable ordering.
- Color and stroke width behavior.
- Serialization only if document persistence is later introduced.
- Selection-lock behavior after the first annotation.

Undo/redo tests compare the complete domain model, not only rendered pixels.

### Settings and filenames

- Default creation and round trip.
- Each schema migration.
- Unknown future field handling.
- Malformed/truncated JSON recovery.
- Atomic-write interruption.
- Invalid hotkey and path rejection.
- Windows + Shift + S override chord matching, key-repeat suppression, key-up handling, and reset behavior.
- Reserved names, Unicode, long paths, invalid characters, collisions, and clock-equal filenames.
- Startup registry path quoting.

## 5. Golden-image tests

Use small deterministic source fixtures plus representative 1080p and 4K fixtures.

For each tool, test:

- Horizontal, vertical, diagonal, and clipped geometry where applicable.
- Minimum and maximum supported widths.
- Multiple colors and transparency.
- Drawing at selection boundaries.
- Combined layers and ordering.
- DPI-independent preview geometry versus physical-pixel compositor output.

### Comparison policy

- Geometry and output dimensions: exact.
- Opaque redaction region: exact replacement color for every decoded pixel.
- PNG/BMP lossless regions: exact unless a documented color-management conversion applies.
- WPF text/anti-aliasing and JPEG: use an explicit per-channel/structural tolerance stored with the test, never an unreviewed blanket threshold.
- A changed golden image requires reviewer approval and a reason in the change description.

The redaction test decodes the exported file, crops the redacted bounds, and verifies that source pixels, transparency, metadata layers, and alternate frames cannot recover covered content.

## 6. Capture and display tests

### Required display matrix

| Configuration | Required checks |
|---|---|
| 1920x1080 at 100% | Complete happy path and baseline performance |
| 4K at 150% | Selection precision, annotation placement, toolbar layout |
| 4K at 200% | Text/accessibility scaling, edges, performance |
| Two displays at 100%/150% | Each display and selection crossing boundary |
| Secondary left of primary | Negative X coordinates and cursor hotspot |
| Secondary above primary | Negative Y coordinates and toolbar placement |
| Landscape plus portrait | Bounds, clipping, annotations |
| HDR on/off where available | Color comparison and documented capture limits |
| RDP session | Capture behavior and reconnect stability |

Each recorded result includes:

- Exact Windows build and edition.
- GPU model and display-driver version.
- Monitor resolution, orientation, scale, and layout.
- Application version/commit.
- Installer SHA-256.
- Capture backend.
- Pass/fail, measured latency, and evidence artifact references.

### Capture assertions

- Source snapshot completes before overlay visibility.
- No overlay pixels appear in output.
- `SRCCOPY | CAPTUREBLT` behavior is documented for layered windows.
- Cursor include/exclude, hotspot, and negative coordinates are correct.
- A display change during capture cancels safely.
- Repeated hotkey presses create only one session.
- Secure/DRM/exclusive-fullscreen limitations fail safely and are documented.

## 7. Clipboard compatibility

Automated checks:

- Correct bitmap dimensions and pixel sample.
- Bounded retry when another process holds the clipboard.
- Final failure keeps capture state available.
- No temporary image file is created.

Manual paste targets:

- Microsoft Paint.
- Outlook.
- Teams.
- Word and PowerPoint.
- Current Edge and Chrome.
- Slack and Discord desktop clients when available.

Record target application versions with release evidence.

## 8. File-output tests

- PNG, JPEG, and BMP decode successfully.
- Dimensions match selection.
- JPEG quality setting changes expected size/quality.
- Save As and quick-save use the same compositor.
- Filename collision creates a suffix.
- Existing file is never silently overwritten.
- Destination errors preserve capture state.
- Unicode, long, OneDrive, unavailable network, read-only, and disk-full cases return actionable errors.
- Successful output leaves no screenshot in global temp or log locations.

## 9. Upload tests

Use a local fake server to simulate:

- 2xx success with view URL, deletion token, and expiration.
- Slow response and progress reporting.
- User cancellation.
- DNS/connect/TLS/timeout failures.
- 400, 401/403, 404, 409, 413, 429, and 5xx classes.
- Malformed JSON and invalid URLs.
- Same-origin HTTPS redirect.
- HTTPS-to-HTTP downgrade rejection.
- Cross-origin redirect without authorization forwarding.
- Redirect loop and excessive redirect count.

Assertions:

- No request occurs before explicit upload.
- Credentials and deletion tokens never enter settings, history, logs, exceptions, or clipboard.
- Failure leaves copy/save/retry available.
- URL is copied only after provider success.
- Certificate-validation bypass is impossible in production configuration.

Production endpoints are never called from CI.

## 10. Installer tests

Run each scenario from a clean snapshot with a standard user account:

1. Interactive clean install.
2. Silent clean install.
3. Launch and first-run settings creation.
4. Upgrade from the prior supported version.
5. Same-version reinstall.
6. Installation interrupted at controlled points, followed by recovery/reinstall.
7. Uninstall while the tray app is running.
8. Silent uninstall.
9. Optional “remove settings” path.

Post-install assertions:

- Correct Programs & Features entry.
- Start Menu and optional desktop shortcut target quoted valid paths.
- One installed application process and one tray icon.
- No separate .NET installation is requested.
- Application and installer signatures validate when signing is enabled.

Post-uninstall assertions:

- No running process.
- No application binaries or shortcuts.
- No Programs & Features entry.
- No startup registry value.
- Settings remain only under the documented preserve behavior.

Inno Setup is not MSI and the plan does not promise MSI-style transactional repair or rollback.

## 11. Privacy and security tests

### Network silence

Capture traffic from process start through capture, annotate, copy, and save. Pass condition: zero application-originated outbound connection attempts. Run update checks and upload as separate explicit cases.

### Residue inspection

After capture/cancel, copy, failed save, failed upload, crash simulation, and uninstall, inspect:

- `%TEMP%`.
- Application LocalAppData.
- Install directory.
- Log directory.
- Recent/history data.
- Crash/support bundles.

Pass condition: no unexpected encoded or raw image content.

### Secret inspection

Search settings, history, logs, installer logs, exceptions, and support bundles for seeded fake API keys and deletion tokens. Pass condition: no plaintext occurrence outside the protected secret store/test fixture.

### IPC

- Different user/session cannot activate or block the instance.
- Malformed/oversized/unknown messages are rejected.
- Only the fixed activation command is accepted.

### Release integrity

- Authenticode publisher matches the pinned identity.
- Timestamp and chain validate.
- Published SHA-256 equals the tested installer.
- SBOM and provenance refer to the same source revision and unsigned inputs.

## 12. Performance methodology

Reference-machine specifications must be recorded with results.

### Activation latency

- Warm tray process.
- Measure from hotkey message receipt to first interactive overlay frame.
- Run at least 30 samples per display configuration.
- Report median, p95, maximum, and failures.
- Budgets: p95 <= 300 ms on one 4K display; <= 500 ms on three mixed-DPI displays.

### Idle use

- Let startup settle for one minute.
- Record CPU and working set for five minutes.
- CPU budget: <= 0.1% average.
- Working-set target: <= 100 MB.

### Soak

- Complete 100 capture/cancel and capture/copy cycles.
- Record managed memory, private bytes, GDI handles, User handles, and process handle count after each cycle.
- Force/allow normal collection and idle stabilization before final comparison.
- Pass: zero net GDI/User handle growth across the final 20 captures and no unbounded memory trend.

## 13. Release gates

### Pull request

- Restore/build succeeds with warnings treated as errors.
- Core unit and deterministic golden tests pass.
- Changed requirements retain stable IDs and objective criteria.
- No new production dependency lacks license/security review.

### Beta

- Complete interactive display and paste matrix.
- Complete 100-capture soak.
- No unresolved P0 defect.
- Privacy traffic and residue checks pass.
- Installer lifecycle passes on clean snapshots.

### Public 1.0

- All applicable P0 requirements map to passing evidence.
- Exact signed installer hash passed the complete release suite.
- Signatures, checksums, SBOM, and provenance are published.
- Known platform limitations are documented.
- No screenshot content or secret exposure remains.
- Upgrade from the prior beta/stable version is verified.

## 14. Requirement traceability

Maintain a release evidence table with:

```text
Requirement ID | Test ID | Environment | Artifact/Log | Result | Date
```

Every P0 `FR-*` and `NFR-*` entry in [Product requirements](requirements.md) must have at least one test or documented inspection. A requirement without evidence is not complete.
