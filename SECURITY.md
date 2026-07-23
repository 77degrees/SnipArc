# Security policy

## Supported versions

SnipArc is currently an alpha. Security fixes are applied to the latest code on
`main` and the newest published alpha release.

| Version | Supported |
|---|---|
| 0.2.x alpha | Yes |
| 0.1.x alpha | No |

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability.

Use GitHub's **Report a vulnerability** option on the repository Security page.
Include:

- the affected version or commit;
- reproduction steps or a proof of concept;
- the expected security impact;
- whether screenshot pixels, clipboard data, credentials, or filesystem data
  may be exposed.

Please do not include real confidential screenshots or personal information.
Use generated test images instead.

The maintainer will acknowledge a complete report as soon as practical,
coordinate remediation privately, and publish an advisory when users need to
take action. No response-time guarantee is offered for this volunteer project.

## Release integrity

Until trusted code signing is active, releases are explicitly labeled
unsigned and include SHA-256 checksums. Never treat an unsigned development
artifact as a trusted production release.
