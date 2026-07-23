# Installer

`ScreenCaptureApp.iss` builds a per-user, x64-compatible Setup EXE with Inno Setup 6.

Run from the repository root:

```powershell
.\eng\build-release.ps1 -BuildInstaller
```

The script regenerates the multi-resolution Windows icon, publishes the self-contained application to `artifacts/app/win-x64`, compiles `installer/ScreenCaptureApp.iss` with Inno Setup, and writes `artifacts/installer/SnipArc-Setup-x64.exe`.

The icon source and generated assets are under `assets/`. Run `eng/generate-icon.ps1` directly when iterating on the icon without producing a complete release.

Inno Setup supplies `/SILENT`, `/VERYSILENT`, and `/SUPPRESSMSGBOXES` install switches. The generated uninstaller accepts the same silent switches. Settings under `%LocalAppData%\ScreenCaptureApp` are intentionally preserved on uninstall; application files, shortcuts, the tray process, and the optional startup registry value are removed. The legacy internal directory, executable, registry value, and AppId remain stable while the public display name is SnipArc, allowing existing alpha installations to upgrade without losing preferences.
