# Brand and Naming

Status: Working public brand selected for alpha
Last reviewed: 2026-07-22

## Recommendation

**SnipArc** is the recommended public name.

- **Snip** immediately communicates screenshot selection.
- **Arc** suggests the fast pointer gesture used to select, annotate, and finish a capture.
- Seven letters, two clear syllables, easy to spell, and not tied to one annotation feature.
- The name supports a distinctive visual mark: crop-frame corners surrounding a gesture arc and completion spark.

A July 2026 exact-name web and GitHub scan found no obvious screenshot product or repository using SnipArc. This is preliminary product research, not trademark clearance. Before a public paid release, search USPTO and relevant international trademark classes, Windows/Microsoft Store listings, package registries, social handles, and desired domains through authoritative services.

## Shortlist

| Name | Positioning | Visual direction | Current recommendation |
|---|---|---|---|
| **SnipArc** | Fast capture-to-finish workflow | Crop corners + gesture arc + spark | Best overall |
| **FrameLark** | Friendly, lightweight screen capture | Framed bird-wing/check gesture | Strong friendly alternative |
| **PixelLatch** | Precision and dependable capture | Pixel grid closing into a latch | Best technical alternative |
| **CropHalo** | Polished visual capture | Crop brackets around an open halo | Best creator-oriented alternative |

Names intentionally rejected after current collision research:

- **SnapLoom** already identifies active image editing and conversion products ([SnapLoom](https://snaploom.com/image/image-compressor)).
- **MarkFrame** is an active photo and watermark application ([Apple App Store](https://apps.apple.com/us/app/markframe/id6748356183)).
- **CropSpark** is used by active agriculture businesses and its `.com` domain is listed for resale ([domain listing](https://www.atom.com/name/CropSpark)).
- **ScreenQuill** has prior domain history, making ownership and discoverability less clean.

## Icon system

The SnipArc mark uses four elements:

1. A midnight-blue rounded tile for a recognizable Windows shortcut silhouette.
2. Four white crop corners for the core capture action.
3. A cyan arc for speed and pointer movement.
4. A violet four-point spark for the completed, polished result.

The production icon is generated deterministically at 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels. The editable vector source is [`assets/app-icon.svg`](../assets/app-icon.svg), the deterministic renderer is [`eng/generate-icon.ps1`](../eng/generate-icon.ps1), and the original AI-assisted concept is [`assets/branding/sniparc-icon-concept.png`](../assets/branding/sniparc-icon-concept.png).

The concept was generated as original logo exploration, then redrawn as simple native vector geometry. The generated bitmap is reference material; shipped ICO/PNG assets come from the deterministic renderer so edges remain stable across builds.

## Upgrade identity

The public UI, installer name, shortcut, product metadata, and documentation use **SnipArc**. Early-alpha technical identifiers intentionally remain unchanged:

- Executable: `ScreenCaptureApp.exe`
- Install directory: `%LocalAppData%\Programs\ScreenCaptureApp`
- Settings directory: `%LocalAppData%\ScreenCaptureApp`
- Startup registry value: `ScreenCaptureApp`
- Inno Setup AppId: unchanged

Keeping those identifiers avoids creating a second installation or losing existing settings. They can be migrated later only with explicit compatibility code and upgrade tests.

## Public-release checklist

- Complete formal trademark review; this document is not legal clearance.
- Reserve the selected GitHub repository name, package identifiers, social handles, and practical domain variants.
- Keep the GitHub repository under its current `77degrees/SnipArc` name so the repository and public product identity remain consistent.
- Code-sign both the application and installer under the final publisher identity.
- Prepare Microsoft Store assets from the same icon geometry.
- Replace alpha version text and publish a privacy policy/support URL.
