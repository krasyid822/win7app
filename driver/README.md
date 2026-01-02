# Virtual Display Driver (WDDM) for Windows 7

This folder contains a **skeleton** for a WDDM Miniport Driver.

## ⚠️ Complexity Warning
Writing a fully functional WDDM driver from scratch is an advanced task requiring:
1.  **Windows Driver Kit (WDK) 7.1.0** (compatible with Win7).
2.  **C/C++ Kernel Programming** knowledge.
3.  **Driver Signing**: Windows 7 x86 requires drivers to be signed (at least test-signed) to load.

## How to Build
1.  Install **WDK 7.1.0**.
2.  Open the **x86 Checked Build Environment** (or Free Build).
3.  Navigate to this directory.
4.  Run `build -cZ`.

## How to Install
1.  Enable Test Signing: `bcdedit /set testsigning on` (and reboot).
2.  Use `devcon.exe` (from WDK) to install:
    ```bat
    devcon install VirtualDisplay.inf Root\VirtualDisplay
    ```
    Or use "Add Legacy Hardware" in Device Manager.

## Architecture
- **VirtualDisplay.sys**: The kernel driver that reports a new monitor to Windows.
- **User Mode App**: Will communicate with this driver via `DxgkDdiDispatchIoRequest` (IOCTL) to get the framebuffer content.

## Next Steps
You need to implement the full `DxgkDdi*` interface, specifically:
- `DxgkDdiPresent`: To capture the screen updates.
- `DxgkDdiCommitVidPn`: To handle resolution changes.
