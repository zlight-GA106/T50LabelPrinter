using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;

namespace T50LabelPrinter
{
    public static class ThermalScheduleRenderer
    {
        public const float DotsPerMm = 8f;
        public const decimal MaximumReceiptHeightMm = 1000m;

        public static Bitmap Render(ThermalScheduleDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            ThermalScheduleDocument document = source.DeepClone();
            int width = MillimetersToPixels(ThermalScheduleDocument.PaperWidthMm);
            int height;
            using (Bitmap measurementBitmap = new Bitmap(1, 1, PixelFormat.Format24bppRgb))
            using (Graphics measurementGraphics = Graphics.FromImage(measurementBitmap))
            using (ScheduleFonts fonts = ScheduleFonts.Create(document))
            {
                ConfigureGraphics(measurementGraphics);
                height = CalculateHeight(document, measurementGraphics, fonts, width);
            }

            decimal heightMm = (decimal)height / (decimal)DotsPerMm;
            if (heightMm > MaximumReceiptHeightMm)
            {
                throw new InvalidOperationException("日程内容超过 1000 mm，请减少条目或拆分打印。");
            }

            Bitmap bitmap = new Bitmap(width, Math.Max(1, height), PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (ScheduleFonts fonts = ScheduleFonts.Create(document))
            {
                ConfigureGraphics(graphics);
                graphics.Clear(Color.White);
                DrawDocument(document, graphics, fonts, width);
            }
            return bitmap;
        }

        public static decimal GetHeightMm(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException("bitmap");
            }
            return (decimal)bitmap.Height / (decimal)DotsPerMm;
        }

        private static int CalculateHeight(
            ThermalScheduleDocument document,
            Graphics graphics,
            ScheduleFonts fonts,
            int paperWidth)
        {
            LayoutMetrics metrics = LayoutMetrics.Create(document, paperWidth);
            float y = metrics.Margin;
            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                y += fonts.Title.GetHeight(graphics) + MillimetersToPixels(1.2m);
            }
            if (document.ShowDate)
            {
                y += fonts.Date.GetHeight(graphics) + MillimetersToPixels(0.8m);
            }
            y += MillimetersToPixels(0.8m);

            IList<ThermalScheduleItem> items = GetVisibleItems(document);
            foreach (ThermalScheduleItem item in items)
            {
                y += MeasureRowHeight(item, graphics, fonts.Body, metrics);
            }
            y += metrics.Margin + MillimetersToPixels(2m);
            return Math.Max(MillimetersToPixels(40m), (int)Math.Ceiling(y));
        }

        private static void DrawDocument(
            ThermalScheduleDocument document,
            Graphics graphics,
            ScheduleFonts fonts,
            int paperWidth)
        {
            LayoutMetrics metrics = LayoutMetrics.Create(document, paperWidth);
            float y = metrics.Margin;
            using (StringFormat centered = CreateStringFormat(StringAlignment.Center, StringAlignment.Near))
            using (StringFormat left = CreateStringFormat(StringAlignment.Near, StringAlignment.Near))
            using (Pen separator = new Pen(Color.Black, Math.Max(1f, DotsPerMm * 0.12f)))
            {
                if (!string.IsNullOrWhiteSpace(document.Title))
                {
                    float titleHeight = fonts.Title.GetHeight(graphics);
                    graphics.DrawString(document.Title, fonts.Title, Brushes.Black,
                        new RectangleF(metrics.Margin, y, metrics.ContentWidth, titleHeight), centered);
                    y += titleHeight + MillimetersToPixels(1.2m);
                }

                if (document.ShowDate)
                {
                    string date = document.Date.ToString("yyyy-MM-dd  dddd", CultureInfo.CurrentCulture);
                    float dateHeight = fonts.Date.GetHeight(graphics);
                    graphics.DrawString(date, fonts.Date, Brushes.Black,
                        new RectangleF(metrics.Margin, y, metrics.ContentWidth, dateHeight), centered);
                    y += dateHeight + MillimetersToPixels(0.8m);
                }

                graphics.DrawLine(separator, metrics.Margin, y, paperWidth - metrics.Margin, y);
                y += MillimetersToPixels(0.8m);

                foreach (ThermalScheduleItem item in GetVisibleItems(document))
                {
                    float rowHeight = MeasureRowHeight(item, graphics, fonts.Body, metrics);
                    float contentY = y + metrics.RowPadding;
                    float x = metrics.Margin;

                    if (document.ShowCheckboxes)
                    {
                        float boxSize = metrics.CheckboxSize;
                        float boxY = contentY + Math.Max(0f, (fonts.Body.GetHeight(graphics) - boxSize) / 2f);
                        graphics.DrawRectangle(separator, x, boxY, boxSize, boxSize);
                        if (item.Completed)
                        {
                            graphics.DrawLine(separator, x + boxSize * 0.18f, boxY + boxSize * 0.55f,
                                x + boxSize * 0.43f, boxY + boxSize * 0.8f);
                            graphics.DrawLine(separator, x + boxSize * 0.43f, boxY + boxSize * 0.8f,
                                x + boxSize * 0.86f, boxY + boxSize * 0.2f);
                        }
                        x += boxSize + metrics.ColumnGap;
                    }

                    graphics.DrawString(item.Time ?? string.Empty, fonts.Body, Brushes.Black,
                        new RectangleF(x, contentY, metrics.TimeWidth, rowHeight - metrics.RowPadding * 2f), left);
                    x += metrics.TimeWidth + metrics.ColumnGap;

                    Font contentFont = item.Completed ? fonts.CompletedBody : fonts.Body;
                    graphics.DrawString(string.IsNullOrWhiteSpace(item.Content) ? "（空日程）" : item.Content,
                        contentFont, Brushes.Black,
                        new RectangleF(x, contentY, metrics.ContentTextWidth, rowHeight - metrics.RowPadding * 2f), left);

                    y += rowHeight;
                    graphics.DrawLine(separator, metrics.Margin, y, paperWidth - metrics.Margin, y);
                }
            }
        }

        private static IList<ThermalScheduleItem> GetVisibleItems(ThermalScheduleDocument document)
        {
            List<ThermalScheduleItem> items = document.Items
                .Where(item => item != null &&
                    (!string.IsNullOrWhiteSpace(item.Time) || !string.IsNullOrWhiteSpace(item.Content)))
                .ToList();
            if (items.Count == 0)
            {
                items.Add(new ThermalScheduleItem { Content = "暂无日程" });
            }
            return items;
        }

        private static float MeasureRowHeight(
            ThermalScheduleItem item,
            Graphics graphics,
            Font font,
            LayoutMetrics metrics)
        {
            string content = string.IsNullOrWhiteSpace(item.Content) ? "（空日程）" : item.Content;
            using (StringFormat format = CreateStringFormat(StringAlignment.Near, StringAlignment.Near))
            {
                SizeF measured = graphics.MeasureString(content, font,
                    new SizeF(metrics.ContentTextWidth, MillimetersToPixels(500m)), format);
                float textHeight = Math.Max(font.GetHeight(graphics), measured.Height);
                return Math.Max(metrics.MinimumRowHeight, textHeight + metrics.RowPadding * 2f);
            }
        }

        private static StringFormat CreateStringFormat(StringAlignment alignment, StringAlignment lineAlignment)
        {
            StringFormat format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = alignment,
                LineAlignment = lineAlignment,
                Trimming = StringTrimming.Word
            };
            format.FormatFlags |= StringFormatFlags.LineLimit;
            return format;
        }

        private static void ConfigureGraphics(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        private static int MillimetersToPixels(decimal millimeters)
        {
            return Math.Max(1, (int)Math.Round((double)(millimeters * (decimal)DotsPerMm)));
        }

        private sealed class LayoutMetrics
        {
            public float Margin { get; private set; }
            public float ContentWidth { get; private set; }
            public float CheckboxSize { get; private set; }
            public float TimeWidth { get; private set; }
            public float ColumnGap { get; private set; }
            public float RowPadding { get; private set; }
            public float MinimumRowHeight { get; private set; }
            public float ContentTextWidth { get; private set; }

            public static LayoutMetrics Create(ThermalScheduleDocument document, int paperWidth)
            {
                float margin = MillimetersToPixels(document.MarginMm);
                float contentWidth = Math.Max(MillimetersToPixels(20m), paperWidth - margin * 2f);
                float checkbox = document.ShowCheckboxes ? MillimetersToPixels(3m) : 0f;
                float time = Math.Min(MillimetersToPixels(12m), contentWidth * 0.28f);
                float gap = MillimetersToPixels(1.2m);
                float reserved = time + gap + (document.ShowCheckboxes ? checkbox + gap : 0f);
                return new LayoutMetrics
                {
                    Margin = margin,
                    ContentWidth = contentWidth,
                    CheckboxSize = checkbox,
                    TimeWidth = time,
                    ColumnGap = gap,
                    RowPadding = MillimetersToPixels(document.RowSpacingMm),
                    MinimumRowHeight = MillimetersToPixels(document.BodyFontSizeMm + document.RowSpacingMm * 2m),
                    ContentTextWidth = Math.Max(MillimetersToPixels(10m), contentWidth - reserved)
                };
            }
        }

        private sealed class ScheduleFonts : IDisposable
        {
            public Font Title { get; private set; }
            public Font Date { get; private set; }
            public Font Body { get; private set; }
            public Font CompletedBody { get; private set; }

            public static ScheduleFonts Create(ThermalScheduleDocument document)
            {
                float titleSize = (float)document.TitleFontSizeMm * DotsPerMm;
                float bodySize = (float)document.BodyFontSizeMm * DotsPerMm;
                return new ScheduleFonts
                {
                    Title = CreateFont(document.FontFamily, titleSize, FontStyle.Bold),
                    Date = CreateFont(document.FontFamily, Math.Max(8f, bodySize * 0.82f), FontStyle.Regular),
                    Body = CreateFont(document.FontFamily, bodySize, FontStyle.Regular),
                    CompletedBody = CreateFont(document.FontFamily, bodySize, FontStyle.Strikeout)
                };
            }

            public void Dispose()
            {
                if (Title != null) Title.Dispose();
                if (Date != null) Date.Dispose();
                if (Body != null) Body.Dispose();
                if (CompletedBody != null) CompletedBody.Dispose();
            }

            private static Font CreateFont(string requestedFamily, float size, FontStyle style)
            {
                string family = FontCatalog.ResolveFamily(requestedFamily);
                try
                {
                    return new Font(family, Math.Max(1f, size), style, GraphicsUnit.Pixel);
                }
                catch (ArgumentException)
                {
                    return new Font(family, Math.Max(1f, size), FontStyle.Regular, GraphicsUnit.Pixel);
                }
            }
        }
    }
}
