using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing.Common;

namespace T50LabelPrinter
{
    internal static class MatrixBitmapRenderer
    {
        public static Bitmap Create(BitMatrix matrix, int targetWidth, int targetHeight)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException("matrix");
            }

            Bitmap result = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
            Rectangle bounds = new Rectangle(0, 0, targetWidth, targetHeight);
            BitmapData data = result.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] pixels = new byte[data.Stride * targetHeight];
                for (int y = 0; y < targetHeight; y++)
                {
                    int matrixY = Math.Min(matrix.Height - 1, y * matrix.Height / targetHeight);
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int matrixX = Math.Min(matrix.Width - 1, x * matrix.Width / targetWidth);
                        byte color = matrix[matrixX, matrixY] ? (byte)0 : (byte)255;
                        int offset = y * data.Stride + x * 3;
                        pixels[offset] = color;
                        pixels[offset + 1] = color;
                        pixels[offset + 2] = color;
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                result.UnlockBits(data);
            }
            return result;
        }
    }
}
