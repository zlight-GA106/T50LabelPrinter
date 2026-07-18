using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed partial class MainForm
    {
        private ContextMenuStrip _canvasContextMenu;
        private ToolStripMenuItem _canvasFontMenu;
        private ToolStripComboBox _canvasFontCombo;
        private ToolStripMenuItem _canvasFontSizeMenu;
        private ToolStripComboBox _canvasFontSizeCombo;
        private ToolStripMenuItem _canvasBoldMenu;
        private ToolStripMenuItem _canvasItalicMenu;
        private ToolStripMenuItem _canvasDeleteMenu;
        private bool _loadingCanvasContextMenu;

        private ContextMenuStrip CreateCanvasContextMenu()
        {
            _canvasContextMenu = new ContextMenuStrip { ShowImageMargin = false };

            _canvasFontMenu = new ToolStripMenuItem("字体");
            _canvasFontCombo = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                AutoSize = false,
                Width = 230,
                DropDownWidth = 300
            };
            foreach (FontOption option in FontCatalog.GetOptions())
            {
                _canvasFontCombo.Items.Add(option);
            }
            _canvasFontCombo.SelectedIndexChanged += CanvasFontSelectionChanged;
            _canvasFontMenu.DropDownItems.Add(_canvasFontCombo);

            _canvasFontSizeMenu = new ToolStripMenuItem("字号高度 (mm)");
            _canvasFontSizeCombo = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoSize = false,
                Width = 110
            };
            foreach (decimal size in new[] { 0.8m, 1m, 1.5m, 2m, 2.5m, 3m, 3.5m, 4m, 5m, 6m, 8m, 10m, 12m, 16m, 20m })
            {
                _canvasFontSizeCombo.Items.Add(size.ToString("0.#", CultureInfo.CurrentCulture));
            }
            _canvasFontSizeCombo.SelectedIndexChanged += CanvasFontSizeSelectionChanged;
            _canvasFontSizeCombo.KeyDown += CanvasFontSizeKeyDown;
            _canvasFontSizeMenu.DropDownItems.Add(_canvasFontSizeCombo);

            _canvasBoldMenu = new ToolStripMenuItem("加粗");
            _canvasItalicMenu = new ToolStripMenuItem("斜体");
            _canvasBoldMenu.Click += (sender, args) =>
                ApplySelectedTextFormatting(element => element.Bold = !element.Bold);
            _canvasItalicMenu.Click += (sender, args) =>
                ApplySelectedTextFormatting(element => element.Italic = !element.Italic);

            ToolStripMenuItem addDataMatrix = new ToolStripMenuItem("添加 Data Matrix 对象");
            ToolStripMenuItem addPdf417 = new ToolStripMenuItem("添加 PDF417 对象");
            addDataMatrix.Click += (sender, args) =>
                AddElement(LabelElement.CreateDataMatrix(_document.WidthMm, _document.HeightMm));
            addPdf417.Click += (sender, args) =>
                AddElement(LabelElement.CreatePdf417(_document.WidthMm, _document.HeightMm));

            _canvasDeleteMenu = new ToolStripMenuItem("删除所选对象");
            _canvasDeleteMenu.Click += (sender, args) => DeleteSelectedElement();

            _canvasContextMenu.Items.Add(_canvasFontMenu);
            _canvasContextMenu.Items.Add(_canvasFontSizeMenu);
            _canvasContextMenu.Items.Add(_canvasBoldMenu);
            _canvasContextMenu.Items.Add(_canvasItalicMenu);
            _canvasContextMenu.Items.Add(new ToolStripSeparator());
            _canvasContextMenu.Items.Add(addDataMatrix);
            _canvasContextMenu.Items.Add(addPdf417);
            _canvasContextMenu.Items.Add(new ToolStripSeparator());
            _canvasContextMenu.Items.Add(_canvasDeleteMenu);
            _canvasContextMenu.Opening += CanvasContextMenuOpening;
            return _canvasContextMenu;
        }

        private void CanvasContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs args)
        {
            LabelElement selected = _canvas == null ? null : _canvas.SelectedElement;
            bool textSelected = selected != null && selected.Kind == LabelElementKind.Text;

            _loadingCanvasContextMenu = true;
            _canvasFontMenu.Enabled = textSelected;
            _canvasFontSizeMenu.Enabled = textSelected;
            _canvasBoldMenu.Enabled = textSelected;
            _canvasItalicMenu.Enabled = textSelected;
            _canvasDeleteMenu.Enabled = selected != null;
            _canvasBoldMenu.Checked = textSelected && selected.Bold;
            _canvasItalicMenu.Checked = textSelected && selected.Italic;

            if (textSelected)
            {
                FontOption selectedFont = _canvasFontCombo.Items.Cast<FontOption>().FirstOrDefault(option =>
                    string.Equals(option.FamilyName, FontCatalog.ResolveFamily(selected.FontFamily), StringComparison.OrdinalIgnoreCase));
                _canvasFontCombo.SelectedItem = selectedFont;
                _canvasFontSizeCombo.Text = selected.FontSizeMm.ToString("0.#", CultureInfo.CurrentCulture);
            }
            _loadingCanvasContextMenu = false;
        }

        private void CanvasFontSelectionChanged(object sender, EventArgs args)
        {
            if (_loadingCanvasContextMenu)
            {
                return;
            }
            FontOption option = _canvasFontCombo.SelectedItem as FontOption;
            if (option != null)
            {
                ApplySelectedTextFormatting(element => element.FontFamily = option.FamilyName);
            }
        }

        private void CanvasFontSizeSelectionChanged(object sender, EventArgs args)
        {
            if (!_loadingCanvasContextMenu)
            {
                ApplyCanvasFontSize();
            }
        }

        private void CanvasFontSizeKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Enter)
            {
                return;
            }
            args.Handled = true;
            args.SuppressKeyPress = true;
            ApplyCanvasFontSize();
            _canvasContextMenu.Close();
        }

        private void ApplyCanvasFontSize()
        {
            decimal size;
            bool parsed = decimal.TryParse(
                _canvasFontSizeCombo.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out size);
            if (!parsed)
            {
                parsed = decimal.TryParse(
                    _canvasFontSizeCombo.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out size);
            }
            if (!parsed)
            {
                return;
            }

            size = Math.Max(0.8m, Math.Min(20m, size));
            ApplySelectedTextFormatting(element => element.FontSizeMm = size);
        }

        private void ApplySelectedTextFormatting(Action<LabelElement> update)
        {
            LabelElement selected = _canvas == null ? null : _canvas.SelectedElement;
            if (selected == null || selected.Kind != LabelElementKind.Text || update == null)
            {
                return;
            }

            update(selected);
            if (IsQueuePreviewActive)
            {
                LabelElement template = _document.Elements.FirstOrDefault(element => element.ObjectId == selected.ObjectId);
                if (template != null && !ReferenceEquals(template, selected))
                {
                    update(template);
                }
            }
            else
            {
                LoadSelectedElement();
            }

            _elementList.Invalidate();
            _canvas.Invalidate();
        }
    }
}
