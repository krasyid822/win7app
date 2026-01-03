using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Win7App
{
    public class ScreenCapture
    {
        // Win32 API untuk mendapatkan cursor
        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr CopyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        private const int CURSOR_SHOWING = 0x00000001;

        public static byte[] CaptureScreenToJpeg(Screen screen, long quality)
        {
            try
            {
                Rectangle bounds = screen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                        
                        // Gambar cursor di atas screenshot
                        DrawCursor(g, bounds);
                    }

                    return EncodeToJpeg(bitmap, quality);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void DrawCursor(Graphics g, Rectangle screenBounds)
        {
            try
            {
                CURSORINFO cursorInfo;
                cursorInfo.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                if (GetCursorInfo(out cursorInfo))
                {
                    // Cek apakah cursor visible
                    if (cursorInfo.flags == CURSOR_SHOWING && cursorInfo.hCursor != IntPtr.Zero)
                    {
                        // Cek apakah cursor ada di screen ini
                        int cursorX = cursorInfo.ptScreenPos.x;
                        int cursorY = cursorInfo.ptScreenPos.y;

                        if (cursorX >= screenBounds.Left && cursorX < screenBounds.Right &&
                            cursorY >= screenBounds.Top && cursorY < screenBounds.Bottom)
                        {
                            // Copy cursor icon
                            IntPtr hIcon = CopyIcon(cursorInfo.hCursor);
                            if (hIcon != IntPtr.Zero)
                            {
                                try
                                {
                                    // Dapatkan hotspot cursor
                                    ICONINFO iconInfo;
                                    if (GetIconInfo(hIcon, out iconInfo))
                                    {
                                        // Hitung posisi relatif ke screen
                                        int drawX = cursorX - screenBounds.Left - iconInfo.xHotspot;
                                        int drawY = cursorY - screenBounds.Top - iconInfo.yHotspot;

                                        // Gambar cursor
                                        IntPtr hdc = g.GetHdc();
                                        try
                                        {
                                            DrawIcon(hdc, drawX + iconInfo.xHotspot, drawY + iconInfo.yHotspot, hIcon);
                                        }
                                        finally
                                        {
                                            g.ReleaseHdc(hdc);
                                        }

                                        // Cleanup bitmap handles
                                        if (iconInfo.hbmMask != IntPtr.Zero)
                                            DeleteObject(iconInfo.hbmMask);
                                        if (iconInfo.hbmColor != IntPtr.Zero)
                                            DeleteObject(iconInfo.hbmColor);
                                    }
                                }
                                finally
                                {
                                    DestroyIcon(hIcon);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Jika gagal, tetap lanjut tanpa cursor
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // Cache JPEG encoder for performance
        private static ImageCodecInfo _jpgEncoder = null;
        private static EncoderParameters _encoderParams = null;
        private static long _lastQuality = -1;
        
        private static byte[] EncodeToJpeg(Bitmap bmp, long quality)
        {
            using (MemoryStream ms = new MemoryStream(bmp.Width * bmp.Height / 4)) // Pre-allocate estimated size
            {
                // Cache encoder for better performance
                if (_jpgEncoder == null)
                {
                    _jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                }
                
                // Only recreate encoder params if quality changed
                if (_encoderParams == null || _lastQuality != quality)
                {
                    _encoderParams = new EncoderParameters(1);
                    _encoderParams.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, quality);
                    _lastQuality = quality;
                }

                bmp.Save(ms, _jpgEncoder, _encoderParams);
                return ms.ToArray();
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
