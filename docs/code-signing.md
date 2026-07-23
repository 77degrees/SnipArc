# Code signing policy

## Current status

SnipArc's `0.2.0-alpha` artifacts are unsigned. They are published with
SHA-256 checksums for testing, but they must not be represented as trusted
production releases.

## Accepted signing path

The project will apply to the SignPath Foundation open-source program. SignPath
Foundation provides qualifying open-source projects with trusted Authenticode
signatures at no charge and keeps the private signing key in managed signing
infrastructure.

**Free code signing provided by
[SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).**

The resulting Windows publisher identity will be **SignPath Foundation**, not
`77degrees`. The repository remains the authoritative link between the
project, reproducible GitHub Actions build, and signed artifact.

Acceptance is controlled by SignPath Foundation and is not guaranteed. Until
the application is approved, releases remain explicitly unsigned.

## Team roles

- Committer and reviewer: [@77degrees](https://github.com/77degrees)
- Signing approver: [@77degrees](https://github.com/77degrees)

As the sole maintainer, `77degrees` currently holds all three responsibilities.
Outside contributions must be reviewed before merge, and every signing request
requires manual approval.

## Privacy policy

This program will not transfer any information to other networked systems
unless specifically requested by the user or the person installing or
operating it.

Capture, annotation, OCR, barcode recognition, copy, save, scrolling capture,
and GIF recording are local. The optional translation command sends only the
recognized text to the user-configured HTTPS translation endpoint after the
user explicitly requests translation. Screenshot pixels are not sent. See
[Security and Privacy](security-and-privacy.md) for the full policy.

## Eligibility controls

- The repository is public.
- SnipArc source is licensed under the OSI-approved MIT License.
- Third-party components and their licenses are listed in
  `THIRD_PARTY_NOTICES.md` and `licenses/`.
- All non-system runtime components use OSI-approved licenses. Animated GIF
  encoding uses the MIT-licensed WPF imaging stack rather than a custom-license
  package.
- The current unsigned alpha was published before signing integration. The
  repeatable `Unsigned release build` workflow now builds and tests the EXE,
  MSI, and extension on a GitHub-hosted Windows runner and uploads one immutable
  signing-input artifact.
- Release source, workflow, commit, tests, and checksums remain publicly
  inspectable.
- No proprietary SnipArc source or separate commercial edition exists.

## Planned SignPath integration

After approval, the maintainer will:

1. Install the SignPath GitHub App for this repository.
2. Configure the SignPath organization, project, signing policy, and artifact
   configuration issued during onboarding.
3. Store only the SignPath submission token as a GitHub Actions secret.
4. Upload the unsigned artifact through GitHub Actions before submitting its
   GitHub artifact ID to SignPath.
5. Use origin verification and GitHub-hosted runners.
6. Deep-sign the first-party application binaries and the outer MSI/Setup EXE
   while excluding third-party binaries.
7. Verify Authenticode status and publish SHA-256 checksums before release.

The exact workflow identifiers cannot be committed before SignPath creates the
project and signing policy.

The unsigned half of this process is already implemented in
`.github/workflows/release-build.yml`. Its raw ZIP artifact preserves the EXE,
MSI, extension, and checksums as one immutable GitHub artifact whose
`artifact-id` can be submitted to SignPath after onboarding.

## Official references

- [SignPath Foundation](https://signpath.org/)
- [Open-source program conditions](https://signpath.org/terms.html)
- [SignPath GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
- [SignPath artifact configuration](https://docs.signpath.io/artifact-configuration/)
