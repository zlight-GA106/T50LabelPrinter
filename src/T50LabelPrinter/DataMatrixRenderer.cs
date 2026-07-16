using System;
using System.Collections.Generic;
using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Datamatrix.Encoder;

namespace T50LabelPrinter
{
    public static class DataMatrixRenderer
    {
        public static Bitmap Create(string content, int targetWidth, int targetHeight)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Data Matrix 内容不能为空。", "content");
            }
            if (targetWidth < 12 || targetHeight < 12)
            {
                throw new ArgumentOutOfRangeException("targetWidth", "Data Matrix 尺寸太小，至少需要 12×12 像素。");
            }

            Dictionary<EncodeHintType, object> hints = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.CHARACTER_SET, "UTF-8" },
                { EncodeHintType.MARGIN, 1 },
                { EncodeHintType.DATA_MATRIX_SHAPE, SymbolShapeHint.FORCE_SQUARE }
            };
            DataMatrixWriter writer = new DataMatrixWriter();
            BitMatrix matrix = writer.encode(content, BarcodeFormat.DATA_MATRIX, targetWidth, targetHeight, hints);
            return MatrixBitmapRenderer.Create(matrix, targetWidth, targetHeight);
        }
    }
}
