using System;
using System.Drawing;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed class ThermalSchedulePreview : ScrollableControl
    {
        private Bitmap _receipt;
        private string _error;

        public ThermalSchedulePreview()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(232, 234, 237);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public decimal ReceiptHeightMm
        {
            get { return _receipt == null ? 0m : ThermalScheduleRenderer.GetHeightMm(_receipt); }
        }

        public Rectangle ReceiptBounds
        {
            get { return GetReceiptBounds(); }
        }

        public void SetDocument(ThermalScheduleDocument document)
        {
            DisposeReceipt();
            _error = null;
            try
            {
                _receipt = ThermalScheduleRenderer.Render(document);
                AutoScrollMinSize = new Size(_receipt.Width + 48, _receipt.Height + 64);
            }
            catch (Exception exception)
            {
                _error = exception.Message;
                AutoScrollMinSize = Size.Empty;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!string.IsNullOrWhiteSpace(_error))
            {
                TextRenderer.DrawText(e.Graphics, _error, Font,
                    new Rectangle(24, 24, Math.Max(100, ClientSize.Width - 48), 80),
                    Color.Firebrick, TextFormatFlags.WordBreak);
                return;
            }
            if (_receipt == null)
            {
                TextRenderer.DrawText(e.Graphics, "正在生成日程预览…", Font,
                    new Point(24, 24), Color.DimGray);
                return;
            }

            Rectangle paper = GetReceiptBounds();
            using (Brush shadow = new SolidBrush(Color.FromArgb(70, Color.Black)))
            {
                e.Graphics.FillRectangle(shadow, paper.X + 5, paper.Y + 5, paper.Width, paper.Height);
            }
            e.Graphics.DrawImageUnscaled(_receipt, paper.Location);
            e.Graphics.DrawRectangle(Pens.Gray, paper);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeReceipt();
            }
            base.Dispose(disposing);
        }

        private void DisposeReceipt()
        {
            if (_receipt == null)
            {
                return;
            }
            _receipt.Dispose();
            _receipt = null;
        }

        private Rectangle GetReceiptBounds()
        {
            if (_receipt == null)
            {
                return Rectangle.Empty;
            }
            int availableWidth = Math.Max(0, ClientSize.Width - 48);
            int x = Math.Max(24, (availableWidth - _receipt.Width) / 2 + 24) + AutoScrollPosition.X;
            int y = 30 + AutoScrollPosition.Y;
            return new Rectangle(x, y, _receipt.Width, _receipt.Height);
        }
    }
}
