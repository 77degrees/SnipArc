# Changelog

## Unreleased

### Added

- README screenshots of the selection, annotation, pixelation and opaque redaction, the extract
  window, the tray menu, and settings, plus an animated GIF of a whole capture from shortcut to
  clipboard. All are recorded against a synthetic backdrop, so no real content appears in them.
  See `assets/screenshots/`.
- User-stepped scrolling capture with tested vertical-overlap detection and PNG composition.
- Selected-area animated GIF recording at 8 FPS for up to 15 seconds.
- Offline English OCR plus local QR and multi-format barcode recognition.
- Opt-in translation through a user-configured HTTPS LibreTranslate-compatible endpoint.
- Optional translation API key, sent as `api_key` only when one is configured, for hosted
  LibreTranslate services that reject unauthenticated requests.
- `deploy/libretranslate/docker-compose.yml` for running a self-hosted translation backend.

### Changed

- Translation failures now explain the cause. A 401 or 403 states that the service needs an API
  key, a 404 points at the missing `/translate` path, and the service's own error text is shown.
- The settings window states the endpoint and API key rules inline instead of hiding them in a
  tooltip.

### Fixed

- `SingleInstanceService` no longer writes its lock file to a hard-coded `%LOCALAPPDATA%` path.
  The root is injectable, so tests stop leaving a directory behind on every run.
- Manifest V3 Edge/Chrome extension for private visible-tab capture, crop, copy, and download.
- WiX 5 per-machine x64 MSI and registry-backed Group Policy Administrative Templates.
- Automated integration tests for OCR/barcode recognition, GIF frame timing, scrolling composition, and output encoding.
- Repeatable release-script packaging for the Chromium extension, enterprise MSI, and per-user installer.
- MIT licensing, contribution guidance, private security-reporting instructions, issue forms, and automated dependency update configuration for the public repository.

### Fixed

- Settings now survive application restarts and Windows sign-in. Earlier builds wrote a PascalCase JSON envelope while the loader accepted only camelCase, causing valid saved settings to be replaced in memory by defaults on each fresh process.

### Changed

- Advanced the working version to `0.2.0-alpha`.
- Renamed the product to **SnipSnap**. This replaces the earlier SnipArc name and, unlike that
  change, goes all the way down: the `ScreenCaptureApp.*` projects, namespaces, assemblies and
  executable are now `SnipSnap.*`, the per-user install folder is `%LocalAppData%\Programs\SnipSnap`,
  the settings folder is `%LocalAppData%\SnipSnap`, and the Group Policy key is
  `Software\Policies\77degrees\SnipSnap`.
- **Breaking for existing alpha installs.** Because the install and settings folders moved, an
  existing SnipArc install is not upgraded in place. Uninstall it first, and copy
  `%LocalAppData%\ScreenCaptureApp\settings.json` to `%LocalAppData%\SnipSnap\` to keep your
  settings. Any Group Policy targeting the old key must be repointed.
- Replaced the generic crop-frame icon with an original crop-corners, cyan gesture-arc, and violet-spark mark.
- Renamed the generated installer to `SnipSnap-Setup-x64.exe`.
- Selected the SignPath Foundation open-source program as the no-cost trusted-signing path; releases remain explicitly unsigned until the application is approved.
- Replaced the custom-license GIF dependency with a first-party streaming
  encoder built on the MIT-licensed WPF imaging stack, leaving only
  OSI-approved non-system runtime dependencies.

## 0.1.0-alpha — 2026-07-20

First working Windows alpha.

### Added

- Notification-area app with global capture shortcut and single-instance activation.
- Optional setting that routes Windows + Shift + S to SnipArc instead of the built-in Snipping Tool while SnipArc is running.
- Active-monitor screen capture, selection movement/resizing, live dimensions, and keyboard nudging.
- Automatic visible-window detection with hover highlighting and click-to-select whole-window capture.
- Pen, line, arrow, rectangle, highlighter, text, pixelation, and opaque-redaction tools.
- Compact dark overlay controls with vector icons and directional resize cursors.
- Original application icon embedded in the EXE, installer, shortcuts, and notification area.
- Undo/redo, clipboard copy, quick save, and PNG/JPG/BMP export.
- Settings for shortcut, output folder, file format, pointer inclusion, notifications, and Windows startup.
- Self-contained per-user Inno Setup installer.
- Automated geometry, editor, Windows-service, and redaction export tests.

### Security and privacy

- No upload implementation, account requirement, telemetry, or screenshot history.
- Opaque redactions replace covered output pixels exactly and are applied after every other annotation.
- Captures containing opaque redactions can only be saved in lossless PNG or BMP format.

### Known limitations

- One monitor per capture session; cross-monitor selection remains planned.
- Unsigned development build.
- Full mixed-DPI, HDR, protected-content, and exclusive-fullscreen validation remains pending.
