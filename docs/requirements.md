# Product Requirements

Status: Proposed for version 1.0
Last reviewed: 2026-07-22
Product name: SnipArc

## 1. Purpose

SnipArc is a lightweight Windows utility for capturing a screen region, annotating it in place, and immediately copying, saving, or explicitly uploading it. It prioritizes a fast workflow, privacy, mixed-DPI reliability, predictable output, and secure redaction.

## 2. Product principles

1. **Fast path first** - capture to clipboard should require no unnecessary dialogs.
2. **Local by default** - capture, edit, copy, and save do not require an account or network.
3. **Explicit sharing** - no screenshot leaves the computer without a direct upload action.
4. **Predictable output** - preview, clipboard, saved file, and uploaded image use the same compositor.
5. **Windows-native behavior** - tray lifecycle, hotkeys, clipboard, DPI, installer, and uninstall follow Windows conventions.
6. **Capability isolation** - recording, recognition, browser, enterprise, and future cloud features must not weaken the fast local capture path.

## 3. Target users

### Primary user

A Windows user who frequently needs to show a small region of their screen in email, chat, documentation, support tickets, or issue reports. They value speed and basic markup more than a full image editor.

### Secondary user

A technical or business user who may capture sensitive information and needs a reliable local workflow, explicit uploads, opaque redaction, and predictable storage behavior.

### Not a primary target for 1.0

- Video creators needing long-form, high-frame-rate, or audio recording.
- Designers needing layered image editing.
- Cross-platform users.

## 4. Primary workflows

### Flow A - Capture and copy

1. User presses the configured global hotkey.
2. The desktop snapshot is frozen and dimmed.
3. User drags a rectangular region and optionally refines it.
4. User optionally annotates the selection.
5. User presses Ctrl+C or selects Copy.
6. The composited bitmap is placed on the clipboard and the overlay closes.

Success means the image pastes correctly into common Windows applications without creating a file or making a network request.

### Flow B - Capture and save

1. User completes a selection and optional annotations.
2. User chooses Save As or presses the quick-save shortcut.
3. The image is encoded in the selected format.
4. On success, the overlay closes and unobtrusive confirmation is shown.

Success means a collision-safe file is created at the requested location and no prior file is silently overwritten.

### Flow C - Capture and upload

1. User completes a selection and optional annotations.
2. User explicitly chooses Upload.
3. The app displays provider/progress information and supports cancel.
4. Provider returns a view URL and optional deletion token/expiration.
5. The view URL is copied to the clipboard.

Success means the user knows that the image left the computer, can identify the provider, and can delete it when the provider supports deletion.

### Flow D - Hotkey conflict

1. The app cannot register the requested global hotkey.
2. The app remains running and explains that another app or Windows owns the shortcut.
3. The user chooses a fallback shortcut in Settings.

The app must not silently alter Windows Snipping Tool, OneDrive, keyboard utility, or accessibility settings.

## 5. Functional requirements

Priority definitions:

- P0 - required for version 1.0.
- P1 - next release after the core is stable.
- P2 - deferred with no version commitment.

### Application lifecycle

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-001 | P0 | Run as a single-instance tray application | A second launch activates the existing process; only one tray icon and hotkey registration exist |
| FR-002 | P0 | Expose Capture, Recent Folder, Settings, About, and Exit from the tray menu | Each command works without a permanent main/taskbar window |
| FR-003 | P0 | Shut down cleanly | Exit unregisters hotkeys, closes overlays, releases native handles, and removes the tray icon |
| FR-004 | P0 | Optionally start at user sign-in | One quoted HKCU Run value is created/removed by the app; stale paths are repaired |

### Hotkeys and capture activation

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-010 | P0 | Provide a configurable global region-capture hotkey | The shortcut works while the app is unfocused and in the tray |
| FR-011 | P0 | Default to Print Screen when Windows allows registration | Successful registration opens exactly one capture session |
| FR-012 | P0 | Detect registration conflicts | Failure produces an actionable notice and Settings link; the app remains usable from the tray |
| FR-012A | P0 | Optionally override the Windows + Shift + S Snipping Tool shortcut | Enabling the clearly labeled setting routes only that shortcut to this app while it is running; disabling or exiting immediately restores normal Windows behavior |
| FR-013 | P1 | Add active-window and full-monitor capture shortcuts | Each mode produces exact target bounds without first opening region selection |
| FR-014 | P1 | Add delayed capture | User can select a delay and cancel before the snapshot is taken |

### Display capture and selection

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-020 | P0 | Snapshot the desktop before showing overlays | No overlay, cursor crosshair, or toolbar pixel appears in output |
| FR-021 | P0 | Support multiple displays and negative virtual coordinates | Required display matrix passes with exact output dimensions |
| FR-022 | P0 | Support mixed 100%-200% scaling using physical-pixel bounds | Selection edges map to the intended source pixels on every tested monitor |
| FR-023 | P0 | Create a click-drag rectangular selection | Dragging in any direction normalizes to a valid rectangle |
| FR-023A | P0 | Detect eligible application windows under the pointer | The topmost visible, titled, non-minimized, non-cloaked application window is highlighted; one click selects its exact frame bounds while dragging still creates a region |
| FR-024 | P0 | Show live width and height | Values equal final physical-pixel output dimensions |
| FR-025 | P0 | Move and resize the selection | Edge/corner handles and drag movement remain within the virtual desktop |
| FR-026 | P0 | Support keyboard refinement | Arrow keys nudge by one pixel; documented modifiers adjust the increment |
| FR-027 | P0 | Support fullscreen select and cancel | Ctrl+A selects the active monitor; Esc cancels without changing clipboard/files |
| FR-028 | P0 | Optionally include the visible cursor | Cursor position, hotspot, and monitor origin are correct |
| FR-029 | P0 | Prevent concurrent capture sessions | Repeated hotkey presses do not create overlapping overlays or corrupt state |

### Annotation

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-030 | P0 | Draw freehand pen strokes | Preview and exported path meet documented golden-image tolerance |
| FR-031 | P0 | Draw lines, arrows, and rectangles | Geometry and selected color/width are preserved in export |
| FR-032 | P0 | Highlight with a translucent marker | Composited output matches preview within the documented color tolerance |
| FR-033 | P0 | Add text | Text can be entered, committed, moved before commit, and exported at the correct scale |
| FR-034 | P0 | Pixelate an area | Output contains flattened pixel blocks; UI labels it as obfuscation, not secure redaction |
| FR-035 | P0 | Opaque-redact an area | Every decoded output pixel in the covered region is replaced with the opaque chosen color |
| FR-036 | P0 | Select annotation color and width | New annotations use the selected values; behavior for existing annotations is documented |
| FR-037 | P0 | Undo and redo editor actions | Command sequence is deterministic and fully restores the prior model state |
| FR-038 | P0 | Keep toolbars visible | Toolbars reposition within the monitor work area near desktop edges |
| FR-039 | P1 | Add numbered step markers and saved tool defaults | Defaults survive restart and exported numbering remains ordered |

### Clipboard and local files

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-040 | P0 | Copy the composited selection to the Windows clipboard | Bitmap pastes into Paint, Outlook, Teams, Office, browsers, Slack, and Discord test targets |
| FR-041 | P0 | Retry temporary clipboard contention | Bounded retry succeeds when the lock clears; failure keeps the capture open |
| FR-042 | P0 | Save PNG, JPG, and BMP | Decoded dimensions and expected format match the selected output |
| FR-043 | P0 | Support Save As and collision-safe quick-save | Existing files are never overwritten without explicit confirmation |
| FR-044 | P0 | Support configurable folder, filename pattern, format, and JPEG quality | Valid settings persist; invalid paths/patterns produce useful validation |
| FR-045 | P0 | Open the last successful output folder | Missing or unavailable folders fail gracefully |
| FR-046 | P1 | Print or open in an external editor | Command receives the same composited pixels as local save |

### Uploading

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-050 | P0* | Upload through an `IUploadProvider` implementation | Explicit action reports progress, supports cancel, and returns a view URL |
| FR-051 | P0* | Copy the successful view URL | Clipboard contains the URL only after provider success |
| FR-052 | P0* | Explain upload visibility and retention on first use | User sees provider, audience model, and expiration policy before first upload |
| FR-053 | P0* | Preserve local recovery on upload failure | User can retry, copy, or save after timeout, cancel, or server failure |
| FR-054 | P1 | Keep metadata-only recent upload history | History contains no image pixels and stores deletion tokens securely |

`P0*` means required only when upload is included in the selected version 1.0 distribution profile. A local-only 1.0 may hide upload controls without failing release scope.

### Settings and installation

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-060 | P0 | Persist schema-versioned per-user settings | Values survive restart and upgrade; corrupt data recovers to safe defaults |
| FR-061 | P0 | Install from one downloadable Setup EXE | Standard user installs without separately installing .NET or approving elevation |
| FR-062 | P0 | Upgrade in place | Prior supported version upgrades without duplicate entries and preserves settings |
| FR-063 | P0 | Uninstall cleanly | No process, binary, shortcut, Programs & Features entry, or startup value remains |
| FR-064 | P0 | Support silent install/uninstall switches | Documented commands work in a clean Windows VM |

### Extended capture and recognition

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-070 | P1 | Build a scrolling capture from repeated views of one selected viewport | User captures pages manually; matching vertical overlap is removed; output is a decodable PNG |
| FR-071 | P1 | Record a selected area as an animated GIF | Output contains multiple timed frames, loops, excludes the recording controls, and stops at the documented duration and dimension limits |
| FR-072 | P1 | Extract English text locally | OCR runs with no network access and makes editable/copyable text available without retaining image pixels |
| FR-073 | P1 | Recognize common 1D/2D barcodes locally | QR, Data Matrix, Aztec, PDF417, UPC/EAN, Code 39/93/128, ITF, Codabar, MSI, and RSS formats are attempted locally |
| FR-074 | P1 | Translate extracted text through an optional provider | No request occurs until Translate is pressed; endpoint validation requires HTTPS except loopback; image pixels are never submitted |

### Browser and managed distribution

| ID | Priority | Requirement | Acceptance criterion |
|---|---:|---|---|
| FR-080 | P1 | Provide a Chromium browser extension | Edge/Chrome extension captures the visible tab, crops, copies, and downloads locally with no server dependency |
| FR-081 | P1 | Provide a per-machine enterprise MSI | WiX build produces an x64 MSI supporting quiet install/uninstall and major upgrades |
| FR-082 | P1 | Provide Group Policy settings | ADMX/ADML controls translation, capture folder, and startup; machine policy overrides user policy and JSON preferences |

## 6. Nonfunctional requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-001 | Performance | Hotkey-to-overlay p95 <= 300 ms on one 4K display and <= 500 ms on three mixed-DPI displays |
| NFR-002 | Performance | Idle CPU <= 0.1% average over five minutes on the reference machine |
| NFR-003 | Performance | Idle working set target <= 100 MB on the reference machine |
| NFR-004 | Performance | Normal annotation preview responds within one 60 Hz frame under the reference workload |
| NFR-005 | Reliability | Net GDI/User handle growth is zero across the final 20 captures of a 100-capture soak test |
| NFR-006 | Privacy | No outbound request occurs during capture, edit, recognition, copy, save, scrolling capture, or recording; translation and future upload/update actions are separately explicit |
| NFR-007 | Privacy | No screenshot pixels are written to disk unless the user saves, enables thumbnail history, or uploads |
| NFR-008 | Security | Credentials and deletion tokens are protected by Credential Manager or DPAPI and excluded from logs |
| NFR-009 | Accessibility | Settings supports keyboard navigation, visible focus, Narrator naming, high contrast, and 200% text scaling |
| NFR-010 | Compatibility | Version 1.0 supports documented Windows 11 x64 releases; unsupported systems receive a readable installer message |
| NFR-011 | Offline | Capture, annotation, clipboard, settings, and local saving remain usable with no network |
| NFR-012 | Maintainability | Capture, compositor, and upload providers are behind interfaces testable without production services |

## 7. Expansion status

| Capability | Status in 0.2 alpha | Remaining release dependency |
|---|---|---|
| Scrolling capture | Implemented as a user-stepped, overlap-stitched PNG workflow | Hardware/application matrix and improved handling of dynamic or repeating content |
| Animated recording | Implemented for looping GIF, 8 FPS, 15-second maximum, no audio | Performance and visual validation across the supported display matrix |
| OCR, translation, and barcode recognition | Implemented; OCR/barcodes are local and translation is provider-configured | Additional OCR language packs and provider compatibility testing |
| Browser extension | Functional Manifest V3 Edge/Chrome source package | Store accounts, review, final extension IDs, and signed store publication |
| Enterprise MSI/GPO | Buildable WiX 5 MSI and functional registry-backed ADMX policies | Authenticode certificate and clean-VM/managed-deployment validation |
| Cloud accounts, galleries, comments, and social login | Not implemented | Hosting/operator, retention policy, abuse moderation, identity providers, legal documents, and service budget |
| Native macOS and Linux clients | Not implemented | Separate UI/capture backends, signing/notarization, packaging, and test hardware |
| Silent background installation of updates | Not enabled | Public immutable release feed plus signed metadata, signed installers, pinned signer identity, and rollback testing |

## 8. Version 1.0 definition of done

Version 1.0 is complete when all applicable P0 requirements have evidence in the [testing strategy](testing.md), no P0 defects remain open, the exact signed installer hash passes release testing, and privacy inspection finds no unintended image persistence or network traffic.
