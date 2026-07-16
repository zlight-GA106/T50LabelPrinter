using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace T50LabelPrinter
{
    public static class LabelRenderer
    {
        public const float PrinterDotsPerMm = 8f;

        public static Bitmap RenderForPrinter(LabelDocument document, DateTime timestamp)
        {
            return Render(document, timestamp, PrinterDotsPerMm, document.PrintGuide);
        }

        public static Bitmap Render(LabelDocument document, DateTime timestamp, float dotsPerMm, bool includeGuide)
        {
            return Render(document, timestamp, dotsPerMm, includeGuide, null);
        }

        public static Bitmap Render(LabelDocument document, DateTime timestamp, float dotsPerMm, bool includeGuide, LabelElement excludedElement)
        {
            int width = Math.Max(1, (int)Math.Round((double)(document.WidthMm * (decimal)dotsPerMm)));
            int height = Math.Max(1, (int)Math.Round((double)(document.HeightMm * (decimal)dotsPerMm)));
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                foreach (LabelElement element in document.Elements)
                {
                    if (ReferenceEquals(element, excludedElement) || (element.IsBarcode && !document.PrintBarcodes))
                    {
                        continue;
                    }
                    RectangleF rectangle = ToPixels(element, dotsPerMm);
                    if (element.Kind == LabelElementKind.Text)
                    {
                        DrawText(graphics, element, rectangle, dotsPerMm);
                    }
                    else
                    {
                        DrawBarcode(graphics, element, timestamp, rectangle, dotsPerMm);
                    }
                }

                if (includeGuide && document.GuideMode != CenterGuideMode.None)
                {
                    DrawCenterGuide(graphics, document, dotsPerMm, Pens.Black);
                }
            }
            return bitmap;
        }

        public static Bitmap RenderElement(LabelElement element, DateTime timestamp, float dotsPerMm)
        {
            int width = Math.Max(1, (int)Math.Round((double)(element.Width * (decimal)dotsPerMm)));
            int height = Math.Max(1, (int)Math.Round((double)(element.Height * (decimal)dotsPerMm)));
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                RectangleF rectangle = new RectangleF(0f, 0f, width, height);
                if (element.Kind == LabelElementKind.Text)
                {
                    DrawText(graphics, element, rectangle, dotsPerMm);
                }
                else
                {
                    DrawBarcode(graphics, element, timestamp, rectangle, dotsPerMm);
                }
            }
            return bitmap;
        }

        public static void DrawCenterGuide(Graphics graphics, LabelDocument document, float dotsPerMm, Pen template)
        {
            float width = (float)document.WidthMm * dotsPerMm;
            float height = (float)document.HeightMm * dotsPerMm;
            float thickness = Math.Max(1f, (float)document.GuideThicknessMm * dotsPerMm);
            using (Pen pen = new Pen(template.Color, thickness))
            {
                pen.DashStyle = template.DashStyle;
                if (document.GuideMode == CenterGuideMode.Vertical || document.GuideMode == CenterGuideMode.Cross)
                {
                    graphics.DrawLine(pen, width / 2f, 0f, width / 2f, height);
                }
                if (document.GuideMode == CenterGuideMode.Horizontal || document.GuideMode == CenterGuideMode.Cross)
                {
                    graphics.DrawLine(pen, 0f, height / 2f, width, height / 2f);
                }
            }
        }

        private static RectangleF ToPixels(LabelElement element, float dotsPerMm)
        {
            return new RectangleF(
                (float)element.X * dotsPerMm,
                (float)element.Y * dotsPerMm,
                Math.Max(1f, (float)element.Width * dotsPerMm),
                Math.Max(1f, (float)element.Height * dotsPerMm));
        }

        private static void DrawText(Graphics graphics, LabelElement element, RectangleF rectangle, float dotsPerMm)
        {
            string familyName = FontCatalog.ResolveFamily(element.FontFamily);
            FontStyle requestedStyle = element.Bold ? FontStyle.Bold : FontStyle.Regular;
            float fontSize = Math.Max(3f, (float)element.FontSizeMm * dotsPerMm);
            Font font = null;
            try
            {
                try
                {
                    font = new Font(familyName, fontSize, requestedStyle, GraphicsUnit.Pixel);
                }
                catch (ArgumentException)
                {
                    font = new Font(familyName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                }

                using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
                {
                    format.LineAlignment = StringAlignment.Center;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags |= StringFormatFlags.LineLimit;
                    switch (element.Align)
                    {
                        case 1:
                            format.Alignment = StringAlignment.Center;
                            break;
                        case 2:
                            format.Alignment = StringAlignment.Far;
                            break;
                        default:
                            format.Alignment = StringAlignment.Near;
                            break;
                    }
                    graphics.DrawString(element.Text ?? string.Empty, font, Brushes.Black, rectangle, format);
                }
            }
            finally
            {
                if (font != null)
                {
                    font.Dispose();
                }
            }
        }

        private static void DrawBarcode(Graphics graphics, LabelElement element, DateTime timestamp, RectangleF rectangle, float dotsPerMm)
        {
            string content = element.GetBarcodeContent(timestamp);
            float digitsHeight = element.PrintDigits
                ? Math.Min(rectangle.Height * 0.25f, Math.Max(10f, dotsPerMm * 2.5f))
                : 0f;
            RectangleF barcodeRectangle = new RectangleF(
                rectangle.X,
                rectangle.Y,
                rectangle.Width,
                Math.Max(1f, rectangle.Height - digitsHeight));

            if (element.Kind == LabelElementKind.DataMatrix)
            {
                float size = Math.Min(barcodeRectangle.Width, barcodeRectangle.Height);
                barcodeRectangle = new RectangleF(
                    barcodeRectangle.X + (barcodeRectangle.Width - size) / 2f,
                    barcodeRectangle.Y,
                    size,
                    size);
            }

            int width = Math.Max(element.Kind == LabelElementKind.DataMatrix ? 12 : 20, (int)Math.Round(barcodeRectangle.Width));
            int height = Math.Max(element.Kind == LabelElementKind.DataMatrix ? 12 : 10, (int)Math.Round(barcodeRectangle.Height));
            using (Bitmap barcode = element.Kind == LabelElementKind.DataMatrix
                ? DataMatrixRenderer.Create(content, width, height)
                : Pdf417Renderer.Create(content, width, height))
            {
                InterpolationMode oldMode = graphics.InterpolationMode;
                PixelOffsetMode oldPixelMode = graphics.PixelOffsetMode;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(barcode, barcodeRectangle);
                graphics.InterpolationMode = oldMode;
                graphics.PixelOffsetMode = oldPixelMode;
            }

            if (element.PrintDigits && digitsHeight > 0f)
            {
                string digits = element.GetDigitsContent(timestamp);
                RectangleF digitsRectangle = new RectangleF(
                    rectangle.X,
                    rectangle.Bottom - digitsHeight,
                    rectangle.Width,
                    digitsHeight);
                float fontSize = Math.Max(5f, Math.Min(digitsHeight * 0.72f, dotsPerMm * 2f));
                using (Font font = new Font(FontCatalog.ResolveFamily(FontCatalog.DefaultSansFamily), fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    graphics.DrawString(digits, font, Brushes.Black, digitsRectangle, format);
                }
            }
        }
    }
}
