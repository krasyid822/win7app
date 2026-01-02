Packaging instructions

This folder helps you create installers for Win7 Virtual Monitor.

Files:
- `build.ps1` - main build script. Usage:

  PowerShell (run as Administrator recommended):

  ```powershell
  # Try to build (will fallback to portable ZIP if no tools present)
  .\build.ps1

  # Attempt to auto-install tools via Chocolatey (if available)
  .\build.ps1 -AutoInstall
  ```

- `setup_tools.ps1` - helper to detect and prepare WiX Toolset and Inno Setup. If Chocolatey is installed it can attempt automatic installation.

Manual install links:
- WiX Toolset: https://wixtoolset.org/releases/
- Inno Setup: https://jrsoftware.org/isinfo.php
- Chocolatey: https://chocolatey.org

Notes:
- Running installers or Chocolatey requires Administrator privileges.
- On Windows 7 TLS/crypto availability may block Chocolatey; if so, download installers manually and run them.
- After installing tools, re-run `build.ps1` to create MSI/EXE installers.