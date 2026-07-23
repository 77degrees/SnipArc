# Enterprise deployment

SnipArc includes a per-machine x64 MSI definition and Administrative Template policy files.

## Build

Publish the application, then build the WiX project:

```powershell
.\eng\build-release.ps1 -BuildEnterpriseMsi
```

The MSI installs under `%ProgramFiles%\SnipArc` and requires administrative rights. Deploy it with Intune, Configuration Manager, or Group Policy Software Installation using standard MSI options:

```powershell
msiexec /i SnipArc-Enterprise-x64.msi /qn /norestart
msiexec /x SnipArc-Enterprise-x64.msi /qn /norestart
```

Copy `policies\SnipArc.admx` and `policies\en-US\SnipArc.adml` into the matching `PolicyDefinitions` locations in the domain Central Store or on an administrative workstation. Machine policy takes precedence over user policy, and policy takes precedence over the user's JSON settings.

The MSI is not a public release until both the application files and MSI are Authenticode-signed by the same trusted publisher.
