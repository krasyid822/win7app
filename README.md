Windows 7 (x86) sample app and MSI installer scaffold

Prerequisites:
- Visual Studio 2017/2019 with .NET Framework 4.6.1 support (or MSBuild)
- WiX Toolset 3.11 (for building the MSI)

Build steps (Developer Command Prompt):

1) Build the WinForms project (x86, Release):

```bat
msbuild Win7App\Win7App.csproj /p:Configuration=Release /p:Platform=x86
```

2) Build the MSI with WiX (from `installer` folder):

```bat
cd installer
"C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe" Product.wxs
"C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe" -out Win7AppInstaller.msi Product.wixobj -ext WixUIExtension
```

Notes:
- Ensure `Win7App.exe` exists at `Win7App\bin\Release\Win7App.exe` before running WiX tools.
- Replace GUIDs in `installer/Product.wxs` with stable GUIDs for production.
- If you prefer an MSI project in Visual Studio, create a WiX Setup Project and add the `Product.wxs` fragments.
