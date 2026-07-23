# Contributing to SnipArc

Thank you for helping improve SnipArc. Bug reports, accessibility feedback,
documentation fixes, tests, and focused feature contributions are welcome.

## Before opening a change

- Search existing issues and pull requests for related work.
- Open an issue before starting a large feature or architectural change.
- Keep screenshots free of personal, confidential, or legally protected data.
- Report security problems privately according to [SECURITY.md](SECURITY.md).

## Development setup

SnipArc currently requires Windows 11 x64 and the .NET SDK selected by
`global.json`.

```powershell
dotnet restore ScreenCaptureApp.slnx
dotnet build ScreenCaptureApp.slnx -c Release --no-restore
dotnet test ScreenCaptureApp.slnx -c Release --no-build --no-restore
```

Inno Setup 6 is required only for the per-user Setup EXE. The enterprise MSI
uses the WiX SDK restored by .NET.

## Pull requests

1. Create a focused branch from `main`.
2. Add or update tests for observable behavior.
3. Update requirements, architecture, security, and testing documentation when
   the corresponding contract changes.
4. Run the complete build and test commands above.
5. Explain the user impact, implementation, and validation in the pull request.

Contributions are accepted under the repository's [MIT License](LICENSE).
Do not submit code you do not have the right to license.
