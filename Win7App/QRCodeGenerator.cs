using System;
using System.Drawing;
using System.Text;
using QRCoder;

namespace Win7App
{
    /// <summary>
    /// Simple QR Code generator - no external dependencies.
    /// Generates basic QR codes for URLs.
    /// </summary>
    public static class QRCodeGenerator
    {
        // QR Code encoding tables for alphanumeric mode
        private static readonly int[] ALPHANUMERIC_TABLE = new int[] {
            36, 37, 38, 39, 40, 41, 42, 43, 44, 45, // SP, $, %, *, +, -, ., /, :
        };

        /// <summary>
        /// Generate a QR code bitmap for the given text (URL).
        /// Uses a simplified approach - generates via external service or manual encoding.
        /// </summary>
        public static Bitmap GenerateQRCode(string text, int size = 150)
        {
            try
            {
                using (var qrGenerator = new QRCoder.QRCodeGenerator())
                {
                    var qrData = qrGenerator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
                    using (var qrCode = new QRCoder.QRCode(qrData))
                    {
                        int moduleCount = qrData.ModuleMatrix.Count;
                        int pixelsPerModule = Math.Max(1, size / (moduleCount + 8));
                        Bitmap bmp = qrCode.GetGraphic(pixelsPerModule, Color.Black, Color.White, true);

                        if (bmp.Width != size || bmp.Height != size)
                        {
                            Bitmap resized = new Bitmap(size, size);
                            using (Graphics g = Graphics.FromImage(resized))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                                g.DrawImage(bmp, 0, 0, size, size);
                            }
                            bmp.Dispose();
                            return resized;
                        }

                        return bmp;
                    }
                }
            }
            catch
            {
                // Fallback to built-in/simple renderer below
            }

            // Fallback: simple renderer (kept for safety when QRCoder isn't available)
            int moduleCountFallback = 21; // Version 1 QR code
            bool[,] modules = EncodeToModules(text, moduleCountFallback);
            Bitmap bmpFallback = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmpFallback))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

                int totalModulesWithQuiet = moduleCountFallback + 8; // 4-module quiet zone on each side
                int moduleSizePx = Math.Max(1, size / totalModulesWithQuiet);
                int actualSizePx = moduleSizePx * totalModulesWithQuiet;
                int offset = (size - actualSizePx) / 2; // center if rounding occurred

                for (int y = 0; y < moduleCountFallback; y++)
                {
                    for (int x = 0; x < moduleCountFallback; x++)
                    {
                        if (modules[y, x])
                        {
                            int rx = offset + (4 + x) * moduleSizePx;
                            int ry = offset + (4 + y) * moduleSizePx;
                            g.FillRectangle(Brushes.Black, rx, ry, moduleSizePx, moduleSizePx);
                        }
                    }
                }
            }

            return bmpFallback;
        }

        private static bool[,] EncodeToModules(string text, int size)
        {
            bool[,] modules = new bool[size, size];

            // Add finder patterns (the three big squares in corners)
            AddFinderPattern(modules, 0, 0);
            AddFinderPattern(modules, size - 7, 0);
            AddFinderPattern(modules, 0, size - 7);

            // Add timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                modules[6, i] = (i % 2 == 0);
                modules[i, 6] = (i % 2 == 0);
            }

            // Add alignment pattern for version 1 (none needed)
            
            // Encode data in remaining space
            byte[] data = Encoding.UTF8.GetBytes(text);
            int dataIndex = 0;
            int bitIndex = 0;

            // Fill data area (simplified - real QR uses complex interleaving)
            for (int col = size - 1; col >= 0; col -= 2)
            {
                if (col == 6) col--; // Skip timing pattern column
                
                for (int row = 0; row < size; row++)
                {
                    int actualRow = ((col / 2) % 2 == 0) ? (size - 1 - row) : row;
                    
                    for (int c = 0; c < 2 && col - c >= 0; c++)
                    {
                        int x = col - c;
                        int y = actualRow;

                        // Skip if in reserved area
                        if (IsReserved(x, y, size)) continue;

                        // Get data bit
                        bool bit = false;
                        if (dataIndex < data.Length)
                        {
                            bit = ((data[dataIndex] >> (7 - bitIndex)) & 1) == 1;
                            bitIndex++;
                            if (bitIndex >= 8)
                            {
                                bitIndex = 0;
                                dataIndex++;
                            }
                        }
                        
                        // Apply mask (pattern 0: (row + col) % 2 == 0)
                        if ((y + x) % 2 == 0) bit = !bit;
                        
                        modules[y, x] = bit;
                    }
                }
            }

            // Add format info
            AddFormatInfo(modules, size);

            return modules;
        }

        private static void AddFinderPattern(bool[,] modules, int startX, int startY)
        {
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    bool black = (x == 0 || x == 6 || y == 0 || y == 6 || 
                                 (x >= 2 && x <= 4 && y >= 2 && y <= 4));
                    modules[startY + y, startX + x] = black;
                }
            }
            
            // Add separator
            for (int i = 0; i < 8; i++)
            {
                if (startX + 7 < modules.GetLength(1)) modules[startY + Math.Min(i, 6), startX + 7] = false;
                if (startY + 7 < modules.GetLength(0)) modules[startY + 7, startX + Math.Min(i, 6)] = false;
            }
        }

        private static bool IsReserved(int x, int y, int size)
        {
            // Finder patterns
            if (x < 9 && y < 9) return true;
            if (x < 9 && y >= size - 8) return true;
            if (x >= size - 8 && y < 9) return true;
            
            // Timing patterns
            if (x == 6 || y == 6) return true;
            
            return false;
        }

        private static void AddFormatInfo(bool[,] modules, int size)
        {
            // Simplified format info (error correction L, mask 0)
            int formatBits = 0x5412; // Pre-calculated for EC-L, mask 0
            
            for (int i = 0; i < 15; i++)
            {
                bool bit = ((formatBits >> (14 - i)) & 1) == 1;
                
                // Around top-left finder
                if (i < 6) modules[i, 8] = bit;
                else if (i < 8) modules[i + 1, 8] = bit;
                else if (i == 8) modules[8, 7] = bit;
                else modules[8, 14 - i] = bit;
                
                // Around bottom-left and top-right finders
                if (i < 8) modules[8, size - 1 - i] = bit;
                else modules[size - 15 + i, 8] = bit;
            }
            
            // Dark module
            modules[size - 8, 8] = true;
        }

        /// <summary>
        /// Generate a simple text-based QR representation (for debugging).
        /// </summary>
        public static string GenerateTextQR(string text)
        {
            int size = 21;
            bool[,] modules = EncodeToModules(text, size);
            
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    sb.Append(modules[y, x] ? "██" : "  ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
