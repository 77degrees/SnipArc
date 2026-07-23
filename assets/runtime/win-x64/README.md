# App-local Visual C++ runtime

This directory contains the x64 Microsoft Visual C++ runtime files required by
the native Tesseract OCR binaries:

- `msvcp140.dll`
- `vcruntime140.dll`
- `vcruntime140_1.dll`

The pinned files have file version `14.51.36247.0` and come from the Visual
Studio 18 toolset `14.51.36231` `Microsoft.VC145.CRT` redistributable
directory. Their SHA-256 hashes are:

| File | SHA-256 |
|---|---|
| `msvcp140.dll` | `7C26614E1D733892C2DEAC7E245CE115504B1D80592DD0A01B08E3E5A55F89CA` |
| `vcruntime140.dll` | `D1F4225DF2CD877DBF130D5668A021DCE3F94118455FF5EC952061C30AFC9CE7` |
| `vcruntime140_1.dll` | `A7146C08F89FE5B04541AB507CDB59FF7B44534D4BA3C668A426C6450A03434E` |

They are copied beside
`ScreenCaptureApp.exe` during build and publish so a clean Windows 11 machine
does not need a separately installed Visual C++ Redistributable.

These files are Microsoft Visual Studio distributable code. When updating them,
use the matching x64 CRT directory from a licensed Visual Studio installation,
retain the original filenames, and rerun the clean-machine packaging tests.
