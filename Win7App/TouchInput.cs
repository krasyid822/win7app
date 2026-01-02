using System;
using System.Runtime.InteropServices;

namespace Win7App
{
    /// <summary>
    /// Simulates mouse input on Windows using Win32 API.
    /// Compatible with Windows 7.
    /// </summary>
    public static class TouchInput
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private const int INPUT_MOUSE = 0;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// Moves the mouse cursor to the specified screen position.
        /// </summary>
        /// <param name="screenX">Absolute X position (0 to screenWidth)</param>
        /// <param name="screenY">Absolute Y position (0 to screenHeight)</param>
        /// <param name="screenWidth">The width of the captured screen area</param>
        /// <param name="screenHeight">The height of the captured screen area</param>
        /// <param name="offsetX">X offset of the captured screen (for multi-monitor)</param>
        /// <param name="offsetY">Y offset of the captured screen (for multi-monitor)</param>
        public static void MoveMouse(int screenX, int screenY, int screenWidth, int screenHeight, int offsetX, int offsetY)
        {
            // Convert relative position to actual screen position
            int actualX = offsetX + screenX;
            int actualY = offsetY + screenY;

            // Get virtual screen size for absolute coordinates
            int virtualWidth = GetSystemMetrics(SM_CXSCREEN);
            int virtualHeight = GetSystemMetrics(SM_CYSCREEN);

            // Normalize to 0-65535 range (required for MOUSEEVENTF_ABSOLUTE)
            int normalizedX = (int)((actualX * 65535.0) / virtualWidth);
            int normalizedY = (int)((actualY * 65535.0) / virtualHeight);

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = normalizedX;
            inputs[0].mi.dy = normalizedY;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Simulates a left mouse button down event.
        /// </summary>
        public static void MouseDown()
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Simulates a left mouse button up event.
        /// </summary>
        public static void MouseUp()
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Simulates a complete mouse click at the specified position.
        /// </summary>
        public static void Click(int screenX, int screenY, int screenWidth, int screenHeight, int offsetX, int offsetY)
        {
            MoveMouse(screenX, screenY, screenWidth, screenHeight, offsetX, offsetY);
            MouseDown();
            MouseUp();
        }

        /// <summary>
        /// Simulates a right-click at the specified position.
        /// </summary>
        public static void RightClick(int screenX, int screenY, int screenWidth, int screenHeight, int offsetX, int offsetY)
        {
            MoveMouse(screenX, screenY, screenWidth, screenHeight, offsetX, offsetY);

            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = MOUSEEVENTF_RIGHTUP;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
