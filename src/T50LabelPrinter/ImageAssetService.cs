using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace T50LabelPrinter
{
    public sealed class ImageImportData
    {
        public string FileName { get; set; }
        public string PngBase64 { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
    }

    public static class ImageAssetService
    {
        private const long MaximumFileBytes = 25L * 1024L * 1024L;
        private const long MaximumPixels = 40L * 1000L * 1000L;

        public static ImageImportData Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("没有选择图片文件。", "filePath");
            }

            FileInfo file = new FileInfo(filePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("图片文件不存在。", filePath);
            }
            if (file.Length > MaximumFileBytes)
            {
                throw new InvalidDataException("图片文件不能超过 25 MB。");
            }

            using (FileStream stream = File.OpenRead(filePath))
            using (Image source = Image.FromStream(stream, true, true))
            {
                if ((long)source.Width * source.Height > MaximumPixels)
                {
                    throw new InvalidDataException("图片分辨率过大，最多支持 4000 万像素。");
                }

                using (Bitmap flattened = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
                using (MemoryStream output = new MemoryStream())
                {
                    using (Graphics graphics = Graphics.FromImage(flattened))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(source, new Rectangle(0, 0, flattened.Width, flattened.Height));
                    }
                    flattened.Save(output, ImageFormat.Png);
                    return new ImageImportData
                    {
                        FileName = Path.GetFileName(filePath),
                        PngBase64 = Convert.ToBase64String(output.ToArray()),
                        PixelWidth = flattened.Width,
                        PixelHeight = flattened.Height
                    };
                }
            }
        }

        public static bool IsValidImageData(string imageData)
        {
            Bitmap bitmap;
            if (!TryDecode(imageData, out bitmap))
            {
                return false;
            }
            bitmap.Dispose();
            return true;
        }

        public static bool TryDecode(string imageData, out Bitmap bitmap)
        {
            bitmap = null;
            if (string.IsNullOrWhiteSpace(imageData))
            {
                return false;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(imageData);
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (Image source = Image.FromStream(stream, true, true))
                {
                    bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    }
                }
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
        }

        public static Bitmap CreateMonochromeBitmap(LabelElement element, int targetWidth, int targetHeight)
        {
            Bitmap source;
            if (element == null || !element.IsImage || !TryDecode(element.ImageData, out source))
            {
                return null;
            }

            targetWidth = Math.Max(1, targetWidth);
            targetHeight = Math.Max(1, targetHeight);
            Bitmap scaled = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(scaled))
                {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    Rectangle destination = GetDestinationRectangle(
                        source.Width,
                        source.Height,
                        targetWidth,
                        targetHeight,
                        element.ImageKeepAspect);
                    graphics.DrawImage(source, destination);
                }
                ConvertToMonochrome(scaled, element.ImageThreshold, element.ImageDither);
                return scaled;
            }
            catch
            {
                scaled.Dispose();
                throw;
            }
            finally
            {
                source.Dispose();
            }
        }

        private static Rectangle GetDestinationRectangle(
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight,
            bool keepAspect)
        {
            if (!keepAspect || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return new Rectangle(0, 0, targetWidth, targetHeight);
            }

            double scale = Math.Min((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return new Rectangle((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
        }

        private static void ConvertToMonochrome(Bitmap bitmap, int threshold, bool dither)
        {
            threshold = Math.Max(0, Math.Min(255, threshold));
            Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                double[] luminance = new double[bitmap.Width * bitmap.Height];

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int offset = row + x * 3;
                        luminance[y * bitmap.Width + x] =
                            pixels[offset + 2] * 0.299 + pixels[offset + 1] * 0.587 + pixels[offset] * 0.114;
                    }
                }

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int index = y * bitmap.Width + x;
                        double oldValue = Math.Max(0d, Math.Min(255d, luminance[index]));
                        byte newValue = oldValue < threshold ? (byte)0 : (byte)255;
                        int offset = row + x * 3;
                        pixels[offset] = newValue;
                        pixels[offset + 1] = newValue;
                        pixels[offset + 2] = newValue;

                        if (dither)
                        {
                            double error = oldValue - newValue;
                            AddError(luminance, bitmap.Width, bitmap.Height, x + 1, y, error * 7d / 16d);
                            AddError(luminance, bitmap.Width, bitmap.Height, x - 1, y + 1, error * 3d / 16d);
                            AddError(luminance, bitmap.Width, bitmap.Height, x, y + 1, error * 5d / 16d);
                            AddError(luminance, bitmap.Width, bitmap.Height, x + 1, y + 1, error / 16d);
                        }
                    }
                }

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void AddError(double[] values, int width, int height, int x, int y, double error)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                values[y * width + x] += error;
            }
        }
    }
}
