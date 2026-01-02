# Win7 Virtual Monitor - Build Installer

Untuk membuat installer MSI/EXE, Anda perlu menginstall salah satu tool berikut:

## Opsi 1: Inno Setup (Recommended - Gratis)

1. Download Inno Setup dari: https://jrsoftware.org/isdl.php
2. Install Inno Setup
3. Buka file `installer/Win7App.iss` dengan Inno Setup
4. Klik Build > Compile (atau tekan Ctrl+F9)
5. File installer akan dibuat di folder `output/`

## Opsi 2: WiX Toolset (Untuk MSI)

1. Download WiX Toolset dari: https://wixtoolset.org/releases/
2. Install WiX Toolset v3.11
3. Jalankan perintah berikut di PowerShell:
   ```
   cd installer
   & "C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe" Win7App.wxs
   & "C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe" Win7App.wixobj -o ..\output\Win7VirtualMonitor.msi
   ```

## Opsi 3: Manual ZIP

Jika tidak ingin menginstall tool tambahan:

1. Buat folder `Win7VirtualMonitor`
2. Copy file-file berikut ke folder tersebut:
   - `Win7App\bin\Release\Win7App.exe`
3. ZIP folder tersebut
4. Distribusikan file ZIP

## File yang Diperlukan

Installer hanya membutuhkan satu file:
- `Win7App.exe` - Aplikasi utama (standalone, tidak perlu dependency tambahan)

## Setelah Install

Aplikasi akan:
- Menambah shortcut di Start Menu
- Menambah firewall rules untuk port 8080 (HTTP) dan 8081 (HTTPS)
- Opsional: shortcut Desktop dan auto-start dengan Windows
