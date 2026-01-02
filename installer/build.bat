@echo off
echo ==========================================
echo  Win7 Virtual Monitor - Build Installer
echo ==========================================
echo.

:: Check for Inno Setup
set ISCC=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files\Inno Setup 6\ISCC.exe
if exist "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 5\ISCC.exe

:: Check for WiX
set CANDLE=
set LIGHT=
if exist "C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe" (
    set CANDLE=C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe
    set LIGHT=C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe
)

:: Create output directory
if not exist "..\output" mkdir "..\output"

:: Try Inno Setup first
if defined ISCC (
    echo Found Inno Setup at: %ISCC%
    echo Building EXE installer...
    "%ISCC%" Win7App.iss
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo SUCCESS! Installer created in: output\Win7VirtualMonitor_Setup_v1.0.0.exe
    ) else (
        echo ERROR: Inno Setup compilation failed.
    )
    goto :done
)

:: Try WiX
if defined CANDLE (
    echo Found WiX Toolset
    echo Building MSI installer...
    "%CANDLE%" Win7App.wxs -o Win7App.wixobj
    if %ERRORLEVEL% NEQ 0 (
        echo ERROR: WiX candle failed.
        goto :done
    )
    "%LIGHT%" Win7App.wixobj -o ..\output\Win7VirtualMonitor.msi
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo SUCCESS! Installer created in: output\Win7VirtualMonitor.msi
        del Win7App.wixobj 2>nul
    ) else (
        echo ERROR: WiX light failed.
    )
    goto :done
)

:: No installer tool found
echo.
echo ERROR: No installer tool found!
echo.
echo Please install one of the following:
echo   1. Inno Setup: https://jrsoftware.org/isdl.php
echo   2. WiX Toolset: https://wixtoolset.org/releases/
echo.
echo Or manually ZIP the following file:
echo   Win7App\bin\Release\Win7App.exe
echo.

:done
echo.
pause
