using System;
using System.Collections.Generic;
using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.PDF417;
using ZXing.PDF417.Internal;

namespace T50LabelPrinter
{
    public static class Pdf417Renderer
    {
        public static Bitmap Create(string content, int targetWidth, int targetHeight)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("PDF417 内容不能为空。", "content");
            }
            if (targetWidth < 20 || targetHeight < 10)
            {
                throw new ArgumentOutOfRangeException("targetWidth", "PDF417 尺寸太小，至少需要 20×10 像素。");
            }

            Dictionary<EncodeHintType, object> hints = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.CHARACTER_SET, "UTF-8" },
                { EncodeHintType.MARGIN, 2 },
                { EncodeHintType.ERROR_CORRECTION, PDF417ErrorCorrectionLevel.L2 },
                { EncodeHintType.PDF417_COMPACT, false },
                { EncodeHintType.PDF417_COMPACTION, Compaction.AUTO }
            };

            PDF417Writer writer = new PDF417Writer();
            BitMatrix matrix = writer.encode(content, BarcodeFormat.PDF_417, targetWidth, targetHeight, hints);
            return MatrixBitmapRenderer.Create(matrix, targetWidth, targetHeight);
        }
    }
}
