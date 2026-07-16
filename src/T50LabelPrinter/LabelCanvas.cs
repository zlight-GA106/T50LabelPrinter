using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed class LabelCanvas : Control
    {
        private const float MarginPixels = 28f;
        private LabelDocument _document;
        private LabelElement _selectedElement;
        private bool _dragging;
        private bool _resizing;
        private PointF _dragOffsetMm;
        private Bitmap _dragBackground;
        private Bitmap _dragElementBitmap;
        private TextBox _inlineEditor;
        private LabelElement _editingElement;
        private bool _closingEditor;

        public LabelCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(232, 234, 237);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public LabelDocument Document
        {
            get { return _document; }
            set
            {
                EndDragLayers();
                CloseInlineEditor(false);
                _document = value;
                Invalidate();
            }
        }

        public LabelElement SelectedElement
        {
            get { return _selectedElement; }
            set
            {
                if (ReferenceEquals(_selectedElement, value))
                {
                    return;
                }
                _selectedElement = value;
                Invalidate();
                EventHandler handler = SelectionChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler SelectionChanged;
        public event EventHandler DocumentChanged;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Document == null || Document.WidthMm <= 0m || Document.HeightMm <= 0m)
            {
                return;
            }

            RectangleF labelBounds = GetLabelBounds();
            float scale = labelBounds.Width / (float)Document.WidthMm;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (_dragging && _dragBackground != null)
            {
                e.Graphics.DrawImageUnscaled(_dragBackground, (int)Math.Round(labelBounds.X), (int)Math.Round(labelBounds.Y));
                if (_dragElementBitmap != null && SelectedElement != null)
                {
                    RectangleF movingBounds = ElementToScreen(SelectedElement);
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(_dragElementBitmap, movingBounds);
                }
            }
            else
            {
                using (Bitmap preview = LabelRenderer.Render(Document, DateTime.Now, scale, false))
                {
                    e.Graphics.DrawImageUnscaled(preview, (int)Math.Round(labelBounds.X), (int)Math.Round(labelBounds.Y));
                }
            }
            e.Graphics.DrawRectangle(Pens.DimGray, labelBounds.X, labelBounds.Y, labelBounds.Width, labelBounds.Height);

            if (!Document.PrintBarcodes)
            {
                foreach (LabelElement barcode in Document.Elements.Where(element => element.IsBarcode))
                {
                    RectangleF hiddenBounds = ElementToScreen(barcode);
                    using (Pen hiddenPen = new Pen(Color.Gray, 1f))
                    {
                        hiddenPen.DashStyle = DashStyle.Dot;
                        e.Graphics.DrawRectangle(hiddenPen, hiddenBounds.X, hiddenBounds.Y, hiddenBounds.Width, hiddenBounds.Height);
                    }
                    TextRenderer.DrawText(e.Graphics, "条码打印已关闭", Font, Rectangle.Round(hiddenBounds), Color.Gray,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }

            if (Document.GuideMode != CenterGuideMode.None)
            {
                GraphicsState state = e.Graphics.Save();
                e.Graphics.TranslateTransform(labelBounds.X, labelBounds.Y);
                using (Pen guidePen = new Pen(Color.FromArgb(220, 0, 128, 192), 1f))
                {
                    guidePen.DashStyle = DashStyle.Dash;
                    LabelRenderer.DrawCenterGuide(e.Graphics, Document, scale, guidePen);
                }
                e.Graphics.Restore(state);
            }

            if (SelectedElement != null)
            {
                RectangleF selectedBounds = ElementToScreen(SelectedElement);
                using (Pen pen = new Pen(Color.RoyalBlue, 2f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, selectedBounds.X, selectedBounds.Y, selectedBounds.Width, selectedBounds.Height);
                }
                RectangleF handle = GetResizeHandle(selectedBounds);
                e.Graphics.FillRectangle(Brushes.RoyalBlue, handle);
                e.Graphics.DrawRectangle(Pens.White, handle.X, handle.Y, handle.Width, handle.Height);
            }

            string sizeText = string.Format("{0:0.#} × {1:0.#} mm   缩放 {2:0.0} px/mm", Document.WidthMm, Document.HeightMm, scale);
            TextRenderer.DrawText(e.Graphics, sizeText, Font, new Point((int)labelBounds.X, Math.Max(2, (int)labelBounds.Y - 22)), Color.DimGray);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Document == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            if (SelectedElement != null && GetResizeHandle(ElementToScreen(SelectedElement)).Contains(e.Location))
            {
                _resizing = true;
                _dragging = true;
                PrepareDragLayers();
                Capture = true;
                return;
            }

            LabelElement hit = Document.Elements.AsEnumerable().Reverse().FirstOrDefault(element => ElementToScreen(element).Contains(e.Location));
            SelectedElement = hit;
            if (hit != null)
            {
                PointF mouse = ScreenToMillimeters(e.Location);
                _dragOffsetMm = new PointF(mouse.X - (float)hit.X, mouse.Y - (float)hit.Y);
                _dragging = true;
                _resizing = false;
                PrepareDragLayers();
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || SelectedElement == null || Document == null)
            {
                Cursor = SelectedElement != null && GetResizeHandle(ElementToScreen(SelectedElement)).Contains(e.Location)
                    ? Cursors.SizeNWSE
                    : Cursors.Default;
                return;
            }

            PointF mouse = ScreenToMillimeters(e.Location);
            if (_resizing)
            {
                SelectedElement.Width = Snap(Math.Max(1m, (decimal)mouse.X - SelectedElement.X));
                SelectedElement.Height = Snap(Math.Max(1m, (decimal)mouse.Y - SelectedElement.Y));
            }
            else
            {
                SelectedElement.X = Snap((decimal)(mouse.X - _dragOffsetMm.X));
                SelectedElement.Y = Snap((decimal)(mouse.Y - _dragOffsetMm.Y));
            }
            Document.ClampElement(SelectedElement);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool changed = _dragging;
            _dragging = false;
            _resizing = false;
            Capture = false;
            EndDragLayers();
            if (changed)
            {
                RaiseDocumentChanged();
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (Document == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            LabelElement hit = Document.Elements.AsEnumerable().Reverse()
                .FirstOrDefault(element => element.Kind == LabelElementKind.Text && ElementToScreen(element).Contains(e.Location));
            if (hit != null)
            {
                SelectedElement = hit;
                BeginInlineEdit(hit);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_inlineEditor != null && _editingElement != null)
            {
                _inlineEditor.Bounds = Rectangle.Round(ElementToScreen(_editingElement));
            }
        }

        private void PrepareDragLayers()
        {
            EndDragLayers();
            if (Document == null || SelectedElement == null)
            {
                return;
            }
            RectangleF labelBounds = GetLabelBounds();
            float scale = labelBounds.Width / (float)Document.WidthMm;
            DateTime timestamp = DateTime.Now;
            _dragBackground = LabelRenderer.Render(Document, timestamp, scale, false, SelectedElement);
            if (!SelectedElement.IsBarcode || Document.PrintBarcodes)
            {
                _dragElementBitmap = LabelRenderer.RenderElement(SelectedElement, timestamp, scale);
            }
        }

        private void EndDragLayers()
        {
            if (_dragBackground != null)
            {
                _dragBackground.Dispose();
                _dragBackground = null;
            }
            if (_dragElementBitmap != null)
            {
                _dragElementBitmap.Dispose();
                _dragElementBitmap = null;
            }
        }

        private void BeginInlineEdit(LabelElement element)
        {
            CloseInlineEditor(false);
            _editingElement = element;
            Rectangle bounds = Rectangle.Round(ElementToScreen(element));
            float scale = GetLabelBounds().Width / (float)Document.WidthMm;
            float fontPixels = Math.Max(12f, Math.Min(56f, (float)element.FontSizeMm * scale));
            _inlineEditor = new TextBox
            {
                Bounds = bounds,
                Text = element.Text ?? string.Empty,
                Multiline = true,
                AcceptsReturn = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(FontCatalog.ResolveFamily(element.FontFamily), fontPixels, element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel),
                TextAlign = element.Align == 1 ? HorizontalAlignment.Center : element.Align == 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            _inlineEditor.KeyDown += InlineEditorKeyDown;
            _inlineEditor.Leave += (sender, args) => CloseInlineEditor(true);
            Controls.Add(_inlineEditor);
            _inlineEditor.BringToFront();
            _inlineEditor.SelectAll();
            _inlineEditor.Focus();
        }

        private void InlineEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CloseInlineEditor(false);
            }
            else if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                CloseInlineEditor(true);
            }
        }

        private void CloseInlineEditor(bool commit)
        {
            if (_inlineEditor == null || _closingEditor)
            {
                return;
            }
            _closingEditor = true;
            TextBox editor = _inlineEditor;
            LabelElement element = _editingElement;
            _inlineEditor = null;
            _editingElement = null;
            if (commit && element != null)
            {
                element.Text = editor.Text;
            }
            Controls.Remove(editor);
            editor.Dispose();
            _closingEditor = false;
            if (commit)
            {
                RaiseDocumentChanged();
            }
            Focus();
            Invalidate();
        }

        private RectangleF GetLabelBounds()
        {
            float availableWidth = Math.Max(20f, ClientSize.Width - MarginPixels * 2f);
            float availableHeight = Math.Max(20f, ClientSize.Height - MarginPixels * 2f);
            float scale = Math.Min(availableWidth / (float)Document.WidthMm, availableHeight / (float)Document.HeightMm);
            scale = Math.Max(1f, scale);
            float width = (float)Document.WidthMm * scale;
            float height = (float)Document.HeightMm * scale;
            return new RectangleF((ClientSize.Width - width) / 2f, (ClientSize.Height - height) / 2f, width, height);
        }

        private RectangleF ElementToScreen(LabelElement element)
        {
            RectangleF bounds = GetLabelBounds();
            float scale = bounds.Width / (float)Document.WidthMm;
            return new RectangleF(
                bounds.X + (float)element.X * scale,
                bounds.Y + (float)element.Y * scale,
                (float)element.Width * scale,
                (float)element.Height * scale);
        }

        private PointF ScreenToMillimeters(Point point)
        {
            RectangleF bounds = GetLabelBounds();
            float scale = bounds.Width / (float)Document.WidthMm;
            return new PointF((point.X - bounds.X) / scale, (point.Y - bounds.Y) / scale);
        }

        private static RectangleF GetResizeHandle(RectangleF bounds)
        {
            const float size = 10f;
            return new RectangleF(bounds.Right - size / 2f, bounds.Bottom - size / 2f, size, size);
        }

        private static decimal Snap(decimal value)
        {
            return Math.Round(value * 10m, MidpointRounding.AwayFromZero) / 10m;
        }

        private void RaiseDocumentChanged()
        {
            EventHandler handler = DocumentChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
