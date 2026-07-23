# Decision Log

Last reviewed: 2026-07-20

This log records decisions that materially affect product scope, architecture, security, distribution, or implementation order. IDs remain stable even when a decision is superseded.

## Summary

| ID | Decision | Status |
|---|---|---|
| DEC-001 | Build a Windows-native client with C#/.NET/WPF | Accepted |
| DEC-002 | Target Windows 11 x64 first | Accepted |
| DEC-003 | Use a physical-pixel Per-Monitor V2 coordinate model | Accepted |
| DEC-004 | Start with GDI capture behind an abstraction | Accepted |
| DEC-005 | Use a local-first privacy model | Accepted |
| DEC-006 | Separate pixelation from secure opaque redaction | Accepted |
| DEC-007 | Distribute through one per-user Inno Setup EXE | Accepted |
| DEC-008 | Package self-contained runtime files; do not require one installed physical file | Accepted |
| DEC-009 | Keep upload behind a provider interface | Accepted |
| DEC-010 | Choose product/publisher name | Open |
| DEC-011 | Choose source license | Open |
| DEC-012 | Choose version 1.0 upload profile | Open |
| DEC-013 | Choose public code-signing route | Open |
| DEC-014 | Lock selection bounds after first annotation | Accepted |
| DEC-015 | Use app-owned HKCU Run startup setting | Accepted |

## Accepted decisions

### DEC-001 - C# 14, .NET 10 LTS, and WPF

Status: Accepted
Date: 2026-07-20

Decision: Build the Windows client using C# 14 on .NET 10 LTS with WPF.

Rationale:

- Native Windows desktop behavior and Win32 interop are central requirements.
- WPF supports custom transparent/borderless overlays and vector drawing.
- .NET 10 is the current LTS release and avoids beginning on a runtime near end of support.
- A web runtime would add footprint and complexity without benefiting the capture path.

Consequences:

- The initial client is Windows-only.
- Mixed-DPI behavior still requires deliberate Win32/WPF boundary handling.
- UI tests require an interactive Windows session.

### DEC-002 - Windows 11 x64 first

Status: Accepted
Date: 2026-07-20

Decision: Officially support documented Windows 11 x64 releases for version 1.0. Add ARM64 after x64 stabilization.

Rationale: Consumer Windows 10 is no longer a clean 2026 support baseline for a new .NET 10 application. Narrowing the initial matrix improves capture and installer quality.

Consequences: The installer rejects unsupported systems with a readable explanation. Compatibility outside the supported matrix is not promised.

### DEC-003 - Physical-pixel Per-Monitor V2 model

Status: Accepted
Date: 2026-07-20

Decision: Store display, selection, and annotation geometry as signed physical virtual-desktop pixels. Convert WPF units only at the UI edge.

Rationale: Mixed scaling and monitors left/above the primary display are common sources of screenshot offset defects.

Consequences: Display topology and conversion adapters must be built before editor tools. A topology change cancels the current capture session.

### DEC-004 - GDI backend first

Status: Accepted
Date: 2026-07-20

Decision: Implement GDI BitBlt with `SRCCOPY | CAPTUREBLT` first, behind `IScreenCaptureBackend`.

Rationale: It provides an immediate virtual-desktop snapshot without the Windows capture picker and is sufficient for the basic desktop workflow.

Consequences: HDR, protected, and exclusive-fullscreen limitations must be tested and documented. DXGI is added only if measured beta failures justify the complexity.

### DEC-005 - Local-first privacy

Status: Accepted
Date: 2026-07-20

Decision: Capture, annotation, copy, and local save make no network requests and write no temporary screenshot file. Upload is always explicit.

Rationale: Screen pixels are potentially sensitive and public URL sharing is easily misunderstood.

Consequences: No required account, no default telemetry, packet-level release testing, and a separate upload trust boundary.

### DEC-006 - Pixelation is not redaction

Status: Accepted
Date: 2026-07-20

Decision: Provide separate pixelation and opaque-redaction tools. Only the opaque replacement tool is described as redaction.

Rationale: Pixelation can leave text or values inferable and must not create a false security guarantee.

Consequences: Export tests decode the file and verify exact replacement pixels in redacted bounds.

### DEC-007 - Per-user Inno Setup installer

Status: Accepted
Date: 2026-07-20

Decision: Create one downloadable Inno Setup EXE that installs per user under LocalAppData without requiring administrator rights.

Rationale: It matches the requested installer experience and is simpler than MSIX/MSI for an initial direct GitHub release.

Consequences:

- Support install, prior-version upgrade, same-version reinstall, interruption recovery, silent switches, and uninstall.
- Do not promise MSI-style transactional repair or rollback.
- Store/enterprise packaging may be added later.

### DEC-008 - Self-contained folder payload

Status: Accepted
Date: 2026-07-20

Decision: Publish a self-contained folder and package it into the installer. One downloadable installer EXE is required; one physical file inside the installed directory is not.

Rationale: This avoids requiring a separate .NET runtime while minimizing single-file extraction/native-dependency risk.

Consequences: Evaluate `PublishSingleFile` only if measured startup and compatibility tests demonstrate a benefit.

### DEC-009 - Upload-provider abstraction

Status: Accepted
Date: 2026-07-20

Decision: Upload implementations conform to `IUploadProvider` and do not enter capture/editor projects.

Rationale: Local output must remain independent of a host, account system, or third-party API.

Consequences: A local-only release can omit providers without disabling capture/edit/save.

### DEC-015 - App-owned startup setting

Status: Accepted
Date: 2026-07-20

Decision: The app owns one quoted current-user Run-key value. The installer does not create a second startup mechanism and removes the known value during uninstall.

Rationale: One owner avoids inconsistent toggle, upgrade, and uninstall behavior.

### DEC-014 - Lock selection after first annotation

Status: Accepted
Date: 2026-07-20

Decision: Allow selection movement and resizing until the first annotation is committed, then lock the source bounds. Undoing the final annotation unlocks the selection.

Rationale: Resizing after annotations creates unclear crop and scale semantics. Locking keeps annotation geometry and exported pixels predictable.

Consequences: Users choose the final crop before annotating. The editor and WPF surface enforce the same invariant.

## Proposed decisions

No decisions are currently proposed.

## Open decisions

### DEC-010 - Product and publisher name

Status: Accepted
Date: 2026-07-22

Decision: Use **SnipArc** as the public product name and **77degrees** as the alpha publisher identity. Preserve early-alpha technical identifiers until an explicit compatibility migration is implemented.

Rationale: The name is short, pronounceable, relevant to the capture workflow, and supports a distinct crop-frame and gesture-arc visual identity.

Consequences: Complete formal trademark review before a public paid release. Keep the installer, documentation, shortcut, application metadata, and repository branding consistent.

### DEC-011 - Source license

Status: Accepted
Date: 2026-07-22

Decision: License SnipArc under the MIT License with copyright held by `77degrees`.

Rationale: MIT is OSI-approved, concise, permissive, familiar to contributors, and compatible with the intended free open-source signing application.

Consequences: Commercial use, redistribution, modification, and forks are permitted when the copyright and license notice are preserved. Third-party components retain their own licenses.

### DEC-012 - Version 1.0 upload profile

Status: Open
Required before: Step 6 and final UI scope

Options:

1. **Local-only 1.0 (recommended fastest/safest)** - copy and save only; provider interface remains available for later work.
2. **Generic user-owned HTTPS endpoint** - user configures an endpoint and credentials.
3. **First-party hosting service** - the most integrated option, but it adds operations, abuse, deletion, retention, cost, and legal/privacy work.

Recommendation: Ship a reliable local-only beta first. Add generic or first-party upload only after local capture/editor quality gates pass.

### DEC-013 - Code signing

Status: Accepted
Date: 2026-07-22

Decision: Apply to the SignPath Foundation open-source program for free trusted Authenticode signing. Until approval, publish only clearly labeled unsigned alpha artifacts with SHA-256 checksums.

Rationale: SnipArc will be public and MIT-licensed, making an open-source signing program preferable to a recurring Azure Artifact Signing subscription. SignPath's GitHub origin verification also ties signed binaries to public build provenance.

Consequences: The Windows signature will identify SignPath Foundation as publisher. Approval is external and not guaranteed. The release workflow must use GitHub-hosted runners, origin verification, and a SignPath-issued project/policy configuration.

## Decision template

```markdown
### DEC-XXX - Title

Status: Proposed | Accepted | Open | Deferred | Superseded
Date: YYYY-MM-DD
Supersedes: DEC-XXX (when applicable)

Decision:

Rationale:

Alternatives:

Consequences:

Validation or review trigger:
```
