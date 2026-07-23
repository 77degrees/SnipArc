# Changelog

## Unreleased

### Added

- User-stepped scrolling capture with tested vertical-overlap detection and PNG composition.
- Selected-area animated GIF recording at 8 FPS for up to 15 seconds.
- Offline English OCR plus local QR and multi-format barcode recognition.
- Opt-in translation through a user-configured HTTPS LibreTranslate-compatible endpoint.
- Manifest V3 Edge/Chrome extension for private visible-tab capture, crop, copy, and download.
- WiX 5 per-machine x64 MSI and registry-backed Group Policy Administrative Templates.
- Automated integration tests for OCR/barcode recognition, GIF frame timing, scrolling composition, and output encoding.
- Repeatable release-script packaging for the Chromium extension, enterprise MSI, and per-user installer.
- MIT licensing, contribution guidance, private security-reporting instructions, issue forms, and automated dependency update configuration for the public repository.

### Fixed

- Settings now survive application restarts and Windows sign-in. Earlier builds wrote a PascalCase JSON envelope while the loader accepted only camelCase, causing valid saved settings to be replaced in memory by defaults on each fresh process.

### Changed

- Advanced the working version to `0.2.0-alpha`.
- Adopted **SnipArc** as the working public product name while retaining legacy internal identifiers for upgrade compatibility.
- Replaced the generic crop-frame icon with an original crop-corners, cyan gesture-arc, and violet-spark mark.
- Renamed the generated installer to `SnipArc-Setup-x64.exe`.
- Selected the SignPath Foundation open-source program as the no-cost trusted-signing path; releases remain explicitly unsigned until the application is approved.

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
