# Documentation Index

This directory separates product, technical, security, and verification concerns. The implementation blueprint remains self-contained so an implementation session can execute a step without reconstructing prior context.

Implementation status: `0.2.0-alpha` is working with local screenshot capture, scrolling composition, GIF recording, recognition, a source-loadable browser extension, and buildable enterprise packaging. Documents describe the longer-term 1.0 target unless a section is explicitly labeled as current-alpha behavior.

## Document map

| Document | Primary audience | Owns |
|---|---|---|
| [Product requirements](requirements.md) | Product owner, developer, tester | User workflows, feature scope, numbered requirements, acceptance criteria |
| [Architecture](architecture.md) | Developer, reviewer | Components, data flow, coordinate invariants, interfaces, persistence, failure handling |
| [Security and privacy](security-and-privacy.md) | Developer, security reviewer | Data classification, trust boundaries, threats, required controls, logging restrictions |
| [Testing strategy](testing.md) | Developer, tester, release owner | Test layers, hardware matrix, performance measurement, release evidence |
| [Decision log](decisions.md) | Product owner, developer | Accepted decisions, recommended defaults, unresolved choices |
| [Brand and naming](branding.md) | Product owner, designer, release owner | Public name shortlist, collision research, icon rationale, migration rules |
| [Implementation blueprint](../plans/windows-screenshot-tool-blueprint.md) | Implementer | Ordered work, dependencies, verification commands, exit criteria |
| [Browser extension](../extensions/chromium/README.md) | Developer, tester | Edge/Chrome development installation and browser limitations |
| [Enterprise deployment](../packaging/enterprise/README.md) | IT administrator, release owner | MSI build, silent deployment, and Group Policy installation |
| [Contributing](../CONTRIBUTING.md) | Contributor | Development setup, change expectations, and pull request validation |
| [Security policy](../SECURITY.md) | Reporter, maintainer | Private vulnerability reporting and supported versions |
| [MIT License](../LICENSE) | User, contributor | Rights and conditions for SnipArc source and distributions |
| [Code signing](code-signing.md) | User, release owner | Current signature state and the SignPath Foundation integration plan |

## Authority and change rules

- Product behavior is authoritative in `requirements.md`.
- Technical structure and invariants are authoritative in `architecture.md`.
- Security and privacy controls are authoritative in `security-and-privacy.md`.
- Verification requirements are authoritative in `testing.md`.
- Sequencing and delivery scope are authoritative in the implementation blueprint.
- A conflict between documents is a defect. Update all affected documents in the same change.
- Material decisions must be added to `decisions.md` before implementation relies on them.
- Public naming and icon changes must be reflected in `branding.md` and preserve documented upgrade identifiers.
- New features default to post-1.0 scope unless they are required to satisfy an existing P0 acceptance criterion.

## Status labels

- `Accepted` - implementation should follow the decision.
- `Proposed` - recommended but still reversible before implementation.
- `Open` - product-owner input is required.
- `Deferred` - intentionally excluded from the current release.
- `Superseded` - retained for history and linked to its replacement.

## Documentation quality checklist

Before merging a documentation change:

- Check internal links and heading anchors.
- Keep requirement IDs stable once implementation references them.
- Give every P0 requirement an objective acceptance criterion.
- Label facts, inferences, recommendations, and open decisions clearly.
- Do not describe unsupported Windows behavior as guaranteed.
- Do not place secrets, private upload URLs, or captured image content in examples.
- Update the “last reviewed” date in any materially changed document.
