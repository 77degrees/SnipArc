# Code signing

## Current status

SnipArc's `0.2.0-alpha` artifacts are unsigned. They are published with
SHA-256 checksums for testing, but they must not be represented as trusted
production releases.

## Accepted signing path

The project will apply to the SignPath Foundation open-source program. SignPath
Foundation provides qualifying open-source projects with trusted Authenticode
signatures at no charge and keeps the private signing key in managed signing
infrastructure.

The resulting Windows publisher identity will be **SignPath Foundation**, not
`77degrees`. The repository remains the authoritative link between the
project, reproducible GitHub Actions build, and signed artifact.

Acceptance is controlled by SignPath Foundation and is not guaranteed. Until
the application is approved, releases remain explicitly unsigned.

## Eligibility controls

- The repository is public.
- SnipArc source is licensed under the OSI-approved MIT License.
- Third-party components and their licenses are listed in
  `THIRD_PARTY_NOTICES.md` and `licenses/`.
- Release artifacts are produced on GitHub-hosted Windows runners.
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

## Official references

- [SignPath Foundation](https://signpath.org/)
- [Open-source program conditions](https://signpath.org/terms.html)
- [SignPath GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
- [SignPath artifact configuration](https://docs.signpath.io/artifact-configuration/)
