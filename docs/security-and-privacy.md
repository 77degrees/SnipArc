# Security and Privacy

Status: Required controls for version 1.0
Last reviewed: 2026-07-20

## 1. Security objective

Screenshots may contain credentials, customer data, legal material, private conversations, internal systems, or other sensitive information. The application must minimize collection, storage, transmission, and logging of captured content.

The safest default is local-only operation. Uploading is optional and is a distinct trust boundary.

## 2. Data classification

| Data | Classification | Default persistence |
|---|---|---|
| Captured source pixels | Sensitive user content | Memory only |
| Composited output pixels | Sensitive user content | Memory only until explicit save/upload |
| Clipboard bitmap | Sensitive user content | Windows clipboard, explicit copy only |
| Saved image | User-controlled content | User-selected path |
| Upload payload | Sensitive user content | Remote provider after explicit upload |
| View URL | Potentially sensitive locator | Clipboard; optional metadata history |
| Deletion token | Secret | Credential Manager/DPAPI only |
| Provider credential/API key | Secret | Credential Manager/DPAPI only |
| Settings | Internal | LocalAppData JSON; no secrets |
| Diagnostic logs | Internal | LocalAppData; never image content or secrets |

## 3. Privacy promises

Version 1.0 must be able to state truthfully:

- Capture, editing, clipboard copy, and local saving do not require a network connection.
- The application does not upload automatically.
- The application does not collect telemetry by default.
- Screenshot pixels are not written to temporary files during ordinary capture/edit/copy.
- Upload occurs only after the user selects Upload.
- The upload provider and visibility/retention model are shown before first use.
- Logs do not contain screenshot pixels, recognized text, clipboard content, credentials, or deletion tokens.

Any future telemetry feature requires a new decision record, documented event schema, opt-in/opt-out behavior, retention policy, and privacy review.

## 4. Trust boundaries

```text
[Desktop pixels]
      |
      v
[Local process memory] ----explicit save----> [User-selected filesystem]
      |
      +----explicit copy---------------------> [Windows clipboard]
      |
      +----explicit upload-------------------> [HTTPS provider]
                                                   |
                                                   v
                                           [Anyone with view URL]
```

Additional boundaries:

- Installer download to local machine.
- Update metadata and installer retrieval.
- Single-instance IPC between processes in the same user session.
- Windows Credential Manager/DPAPI for secrets.

## 5. Threats and required controls

### T-01: Accidental upload

Risk: A sensitive screenshot leaves the computer because upload is automatic or visually confused with Copy/Save.

Controls:

- Upload requires an explicit button or dedicated shortcut.
- No upload on selection completion.
- First-use warning identifies provider, visibility, and retention.
- Upload control uses a distinct accessible name and confirmation state.
- Network tests verify zero requests during capture/edit/copy/save.

### T-02: Guessable public URLs

Risk: A first-party host exposes screenshots through enumerable identifiers.

Controls:

- Generate at least 128 bits of cryptographic randomness for object identifiers.
- Do not use counters, timestamps, short sequential IDs, or reversible user IDs.
- Apply HTTPS, rate limits, deletion, and optional expiration.
- Do not claim URLs are private; describe them as accessible to anyone who has the link.

### T-03: Credential or deletion-token disclosure

Risk: Secrets appear in settings, history, logs, exceptions, clipboard, or crash dumps.

Controls:

- Store secrets only in Credential Manager or DPAPI-protected records.
- Store a stable secret reference in settings, not the secret value.
- Redact headers, query parameters, tokens, and provider response bodies from logs.
- Never copy a deletion token to the clipboard automatically.
- Clear secret buffers when practical and never include them in exception messages.

### T-04: HTTPS downgrade or credential forwarding

Risk: A redirect sends screenshot bytes or authorization to HTTP or another origin.

Controls:

- Require HTTPS for all non-loopback upload endpoints.
- Reject HTTPS-to-HTTP redirects.
- Never forward authorization headers to a different origin.
- Use default Windows certificate validation with no user bypass.
- Bound redirects and request timeout.

### T-05: Incomplete redaction

Risk: Pixelation, blur, transparency, layers, metadata, or undo state lets a recipient recover covered content.

Controls:

- Label pixelation as visual obfuscation, not secure redaction.
- Provide a separate fully opaque redaction tool.
- Replace every covered output pixel in the final compositor.
- Do not export editor layers or source-image metadata.
- Decode the final file in tests and assert uniform replacement pixels in redacted bounds.

### T-06: Screenshot residue

Risk: Sensitive pixels remain in temp files, crash artifacts, thumbnails, history, or logs.

Controls:

- Keep source and composited images in memory.
- Save atomically in the selected destination directory, not a global temp folder.
- Keep local history metadata-only by default.
- Require explicit opt-in and retention controls before storing thumbnails.
- Inspect LocalAppData, Temp, recent files, logs, and install directories in release tests.

### T-07: Malicious local IPC

Risk: Another process blocks activation, injects a command, or sends oversized data.

Controls:

- Scope mutex/IPC names with current user SID and session.
- Apply restrictive ACLs.
- Accept only fixed activation message types and bounded payloads.
- Never accept paths, URLs, image bytes, provider settings, or shell commands through activation IPC.

### T-08: Malicious or replaced update

Risk: Update notification leads the user to an attacker-controlled installer.

Controls before updates are enabled:

- Retrieve signed metadata over HTTPS.
- Pin the expected Authenticode publisher identity.
- Enforce channel and monotonic-version rules.
- Reject downgrade redirects and unexpected origins.
- Verify the complete installer before prompting execution.
- Publish and display SHA-256 checksums.

### T-09: Path and filename abuse

Risk: Invalid filename templates or unsafe paths overwrite files or escape the intended destination.

Controls:

- Validate templates and reserved Windows names.
- Resolve and display the final destination before writing when confirmation is required.
- Use collision suffixes by default.
- Never silently overwrite.
- Quote executable paths written to the startup registry value.

### T-10: Recognition or translation disclosure

Risk: Image pixels, extracted text, or confidential barcodes leave the device without clear intent.

Controls:

- OCR and barcode recognition use bundled local engines and make no HTTP request.
- Do not log image pixels, OCR text, barcode values, or translation bodies.
- Translation is disabled until the user configures an endpoint and presses Translate.
- Accept HTTPS translation endpoints; allow HTTP only for loopback development.
- Send extracted text only, never the source image.
- Managed environments can disable translation through policy.

### T-11: Browser-extension capture exposure

Risk: A browser capture is retained, uploaded, or made available to unrelated pages or extensions.

Controls:

- Request `activeTab` rather than broad host permissions.
- Capture only after the user clicks the extension action.
- Keep the pending PNG in extension session storage.
- Perform crop, copy, and download in the extension origin without remote scripts or network APIs.
- Browser-protected pages remain unavailable by platform design.

### T-12: Recording and scrolling-capture residue

Risk: A canceled capture leaves partial files or unbounded image data.

Controls:

- Bound animated GIF duration, frame interval, and maximum dimension.
- Exclude capture-control windows and omit the mouse pointer from repeated frames.
- Use sibling partial files and atomic rename for completed outputs.
- Delete partial files on encoder failure and delete completed GIF output when the user cancels.
- Keep scrolling pages only in memory until Finish; Cancel writes nothing.

## 6. Upload-service requirements

If a first-party service is built, it is a separate project and deployment boundary. At minimum it must provide:

- HTTPS only.
- Maximum request and decoded image dimensions.
- MIME type and actual image decoding validation.
- Re-encoding or metadata stripping where appropriate.
- Cryptographically random object IDs.
- Rate limiting and abuse controls.
- Configurable expiration.
- Unauthenticated or authenticated deletion through a secret deletion token.
- Clear visibility and retention terms.
- No directory listing, sequential browsing, or search endpoint.
- Operational logs that exclude image bytes and secrets.
- Backup/retention behavior consistent with advertised deletion.

Use only documented upload-provider APIs. Do not integrate undocumented or private third-party protocols.

## 7. Logging policy

Allowed fields:

- Application version and build ID.
- Windows version and architecture.
- Operation name and duration.
- Display count, dimensions, DPI values, and coordinate topology.
- Error category and sanitized stack trace.
- Provider identifier and HTTP status class, without URL path/query/body.
- GDI/User handle counts for diagnostics.

Forbidden fields:

- Image pixels, encoded images, thumbnails, or hashes intended to fingerprint content.
- OCR/extracted text or clipboard contents.
- Local filenames unless the user explicitly creates a support bundle that previews them.
- Full upload URLs.
- Authorization headers, cookies, API keys, deletion tokens, or signed query parameters.
- Usernames, email addresses, or unrelated device identifiers.

Translation request bodies, barcode values, and recognition confidence details are also forbidden.

Logs should use a bounded rolling policy with a documented retention period and a user-facing delete action.

## 8. Installer and signing controls

- Build unsigned inputs in CI with recorded source revision and dependency lock state.
- Scan dependencies and release payloads.
- Sign the application EXE before packaging.
- Sign and timestamp the final installer.
- Use one consistent RSA-based publisher identity.
- Test and publish the exact signed installer hash.
- Do not claim timestamped signed binaries are bit-for-bit reproducible.
- Preserve provenance linking source revision, unsigned input hash, signed payload hash, and release tag.

Unsigned internal alpha builds must be labeled clearly and must not be presented as trusted public releases.

## 9. Security verification gate

A public 1.0 release requires evidence that:

- No network connection occurs for local workflows.
- No screenshot residue appears outside the selected destination/clipboard/provider.
- Redacted output contains only opaque replacement pixels in covered regions.
- Secrets are absent from settings, history, logs, exceptions, and support bundles.
- Redirect and certificate rules are enforced.
- IPC rejects another user's/session's access and malformed messages.
- Installer and application signatures validate and match the expected publisher.
- Dependency and Defender scans have no unresolved release-blocking result.

Detailed procedures are in [Testing strategy](testing.md).
