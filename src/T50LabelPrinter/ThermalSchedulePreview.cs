using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed class ThermalSchedulePreview : ScrollableControl
    {
        private Bitmap _receipt;
        private string _error;
        private ThermalScheduleDocument _document;
        private IList<RectangleF> _rows = new List<RectangleF>();
        private readonly HashSet<int> _selectedRows = new HashSet<int>();
        private ThermalScheduleImage _dragStart;
        private Point _dragPoint;
        private int _handle = -1;
        public ThermalScheduleImage SelectedImage { get; private set; }
        public bool EditingEnabled { get; set; } = true;
        public event Action<int, Keys> RowSelected;
        public event EventHandler ImageChanged;
        public event EventHandler DeleteImageRequested;
        public event EventHandler EditImageRequested;

        public ThermalSchedulePreview()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            TabStop = true;
            BackColor = Color.FromArgb(232, 234, 237);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }
        public decimal ReceiptHeightMm { get { return _receipt == null ? 0m : ThermalScheduleRenderer.GetHeightMm(_receipt); } }
        public Rectangle ReceiptBounds { get { return GetReceiptBounds(); } }
        public void SelectImage(ThermalScheduleImage image) { SelectedImage = image; Invalidate(); }
        public void SetSelectedRows(IEnumerable<int> rows)
        {
            _selectedRows.Clear();
            foreach (int row in rows) _selectedRows.Add(row);
            Invalidate();
        }
        public int HitTestRow(Point point)
        {
            Rectangle paper = GetReceiptBounds();
            PointF relative = new PointF(point.X - paper.X, point.Y - paper.Y);
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Contains(relative)) return i;
            return -1;
        }
        public void SetDocument(ThermalScheduleDocument document)
        {
            _document = document;
            if (SelectedImage != null && !document.Images.Contains(SelectedImage)) SelectedImage = null;
            RenderReceipt();
        }
        private void RenderReceipt()
        {
            DisposeReceipt();
            _error = null;
            try
            {
                _receipt = ThermalScheduleRenderer.Render(_document);
                _rows = ThermalScheduleRenderer.GetRowBounds(_document);
                AutoScrollMinSize = new Size(_receipt.Width + 48, _receipt.Height + 64);
            }
            catch (Exception exception)
            {
                _error = exception.Message;
                _rows = new List<RectangleF>();
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
            if (_receipt == null) return;
            Rectangle paper = GetReceiptBounds();
            using (Brush shadow = new SolidBrush(Color.FromArgb(70, Color.Black)))
                e.Graphics.FillRectangle(shadow, paper.X + 5, paper.Y + 5, paper.Width, paper.Height);
            e.Graphics.DrawImageUnscaled(_receipt, paper.Location);
            e.Graphics.DrawRectangle(Pens.Gray, paper);
            using (Brush selection = new SolidBrush(Color.FromArgb(30, Color.DodgerBlue)))
                foreach (int index in _selectedRows.Where(index => index >= 0 && index < _rows.Count))
                {
                    RectangleF row = _rows[index];
                    row.Offset(paper.Location);
                    e.Graphics.FillRectangle(selection, row);
                }
            if (SelectedImage != null)
            {
                RectangleF bounds = ImageBounds(SelectedImage);
                e.Graphics.DrawRectangle(Pens.DodgerBlue, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                foreach (RectangleF handle in Handles(bounds))
                {
                    e.Graphics.FillRectangle(Brushes.White, handle);
                    e.Graphics.DrawRectangle(Pens.DodgerBlue, handle.X, handle.Y, handle.Width, handle.Height);
                }
            }
        }
        private RectangleF ImageBounds(ThermalScheduleImage image)
        {
            Rectangle paper = GetReceiptBounds();
            return new RectangleF(paper.X + (float)image.X * 8f, paper.Y + (float)image.Y * 8f,
                (float)image.Width * 8f, (float)image.Height * 8f);
        }
        private RectangleF[] Handles(RectangleF bounds)
        {
            float x = bounds.X, y = bounds.Y, r = bounds.Right, b = bounds.Bottom;
            float cx = (x + r) / 2, cy = (y + b) / 2;
            float size = Math.Max(8f, DeviceDpi / 12f);
            return new[] { new PointF(x,y), new PointF(cx,y), new PointF(r,y), new PointF(r,cy),
                new PointF(r,b), new PointF(cx,b), new PointF(x,b), new PointF(x,cy) }
                .Select(point => new RectangleF(point.X - size / 2, point.Y - size / 2, size, size)).ToArray();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (_document != null && EditingEnabled && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right))
            {
                _handle = -1;
                if (SelectedImage != null)
                {
                    RectangleF[] handles = Handles(ImageBounds(SelectedImage));
                    for (int i = 0; i < handles.Length; i++) if (handles[i].Contains(e.Location)) { _handle = i; break; }
                }
                if (_handle < 0) SelectedImage = _document.Images.LastOrDefault(image => ImageBounds(image).Contains(e.Location));
                if (SelectedImage != null && e.Button == MouseButtons.Left)
                {
                    _dragStart = SelectedImage.DeepClone();
                    _dragPoint = e.Location;
                    Capture = true;
                }
                else if (SelectedImage == null)
                {
                    int row = HitTestRow(e.Location);
                    if (row >= 0 && RowSelected != null) RowSelected(row, ModifierKeys);
                }
                Invalidate();
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragStart != null && SelectedImage != null && Capture)
            {
                decimal dx = (e.X - _dragPoint.X) / 8m, dy = (e.Y - _dragPoint.Y) / 8m;
                if (_handle >= 0) SelectedImage.ResizeFrom(_dragStart, _handle, dx, dy,
                    SelectedImage.KeepAspect || (ModifierKeys & Keys.Shift) != 0);
                else
                {
                    SelectedImage.X = Math.Max(0m, Math.Min(58m - SelectedImage.Width, _dragStart.X + dx));
                    SelectedImage.Y = Math.Max(0m, Math.Min(1000m - SelectedImage.Height, _dragStart.Y + dy));
                }
                RenderReceipt();
            }
            else
            {
                int handle = SelectedImage == null ? -1 : Array.FindIndex(Handles(ImageBounds(SelectedImage)), bounds => bounds.Contains(e.Location));
                Cursor = handle == 0 || handle == 4 ? Cursors.SizeNWSE : handle == 2 || handle == 6 ? Cursors.SizeNESW :
                    handle == 1 || handle == 5 ? Cursors.SizeNS : handle == 3 || handle == 7 ? Cursors.SizeWE :
                    _document != null && _document.Images.Any(image => ImageBounds(image).Contains(e.Location)) ? Cursors.SizeAll : Cursors.Default;
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragStart != null)
            {
                _dragStart = null;
                Capture = false;
                if (ImageChanged != null) ImageChanged(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) _dragStart = null;
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (SelectedImage != null && EditingEnabled && EditImageRequested != null) EditImageRequested(this, EventArgs.Empty);
            base.OnMouseDoubleClick(e);
        }
        protected override bool IsInputKey(Keys keyData) { return SelectedImage != null || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedImage != null && EditingEnabled)
            {
                if (e.KeyCode == Keys.Delete && DeleteImageRequested != null) DeleteImageRequested(this, EventArgs.Empty);
                if (SelectedImage == null) { e.Handled = true; return; }
                decimal step = e.Shift ? 1m : 0.125m;
                if (e.KeyCode == Keys.Left) SelectedImage.X -= step;
                if (e.KeyCode == Keys.Right) SelectedImage.X += step;
                if (e.KeyCode == Keys.Up) SelectedImage.Y -= step;
                if (e.KeyCode == Keys.Down) SelectedImage.Y += step;
                if (e.KeyCode >= Keys.Left && e.KeyCode <= Keys.Down)
                {
                    SelectedImage.Normalize();
                    RenderReceipt();
                    if (ImageChanged != null) ImageChanged(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
        protected override void Dispose(bool disposing) { if (disposing) DisposeReceipt(); base.Dispose(disposing); }
        private void DisposeReceipt() { if (_receipt != null) { _receipt.Dispose(); _receipt = null; } }
        private Rectangle GetReceiptBounds()
        {
            if (_receipt == null) return Rectangle.Empty;
            int availableWidth = Math.Max(0, ClientSize.Width - 48);
            return new Rectangle(Math.Max(24, (availableWidth - _receipt.Width) / 2 + 24) + AutoScrollPosition.X,
                30 + AutoScrollPosition.Y, _receipt.Width, _receipt.Height);
        }
    }
}
