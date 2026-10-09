using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed partial class ThermalSchedulePage
    {
        private List<ThermalScheduleImage> _images = new List<ThermalScheduleImage>();
        private readonly Timer _dayTimer = new Timer { Interval = 30000 };
        private DateTime _lastPreviewDate = DateTime.Today;
        private ToolStripMenuItem _mergeRowsMenu, _splitRowsMenu, _inlineDateMenu;
        private ToolStripMenuItem _imageSizeMenu, _imageAspectMenu, _imageFrontMenu, _imageBackMenu;
        private int _selectionAnchor;

        private void AddObjectContextMenu(ContextMenuStrip menu)
        {
            menu.Items.Add(new ToolStripSeparator());
            _inlineDateMenu = new ToolStripMenuItem("插入当前日期到行内", null, (sender, args) => InsertInlineDate());
            _mergeRowsMenu = new ToolStripMenuItem("合并空日程行", null, (sender, args) => ChangeRowGrouping(false));
            _splitRowsMenu = new ToolStripMenuItem("拆分空合并行", null, (sender, args) => ChangeRowGrouping(true));
            menu.Items.Add(_inlineDateMenu);
            menu.Items.Add(_mergeRowsMenu);
            menu.Items.Add(_splitRowsMenu);
            menu.Items.Add(new ToolStripMenuItem("添加日期对象", null, (sender, args) => AddDateItem()));
            menu.Items.Add(new ToolStripMenuItem("添加空日程行", null, (sender, args) => AddEmptyItem()));
            menu.Items.Add(new ToolStripMenuItem("导入图片…", null, (sender, args) => ImportScheduleImage()));
            _imageSizeMenu = new ToolStripMenuItem("图片位置和大小…", null, (sender, args) => EditSelectedImage());
            _imageAspectMenu = new ToolStripMenuItem("锁定图片纵横比", null, (sender, args) =>
            {
                ThermalScheduleImage image = _preview.SelectedImage;
                if (image != null && !_printing) { image.KeepAspect = !image.KeepAspect; ScheduleChanged(this, EventArgs.Empty); }
            });
            _imageFrontMenu = new ToolStripMenuItem("图片置于顶层", null, (sender, args) => ReorderImage(true));
            _imageBackMenu = new ToolStripMenuItem("图片置于底层", null, (sender, args) => ReorderImage(false));
            menu.Items.Add(_imageSizeMenu);
            menu.Items.Add(_imageAspectMenu);
            menu.Items.Add(_imageFrontMenu);
            menu.Items.Add(_imageBackMenu);
        }

        private int[] GetSelectedRowIndices()
        {
            return _items.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).OrderBy(index => index).ToArray();
        }

        private void UpdateObjectContextMenu()
        {
            bool image = _contextFromPreview && _preview.SelectedImage != null;
            int[] selected = GetSelectedRowIndices();
            ThermalScheduleDocument document = BuildDocument(false);
            _mergeRowsMenu.Enabled = !image && !_contextTitle && !_printing && ThermalScheduleRowOperations.CanMerge(document.Items, selected);
            _splitRowsMenu.Enabled = !image && !_contextTitle && !_printing && ThermalScheduleRowOperations.CanSplit(document.Items, selected);
            _mergeRowsMenu.ToolTipText = "Ctrl/Shift 选择两个以上连续空行；时间、内容、完成标记必须为空";
            _splitRowsMenu.ToolTipText = "清空合并行的时间、内容及完成标记后可拆分";
            _inlineDateMenu.Enabled = !image && !_contextTitle && GetContextRow() != null && !_printing && _showContentColumn;
            _imageSizeMenu.Enabled = _imageAspectMenu.Enabled = _imageFrontMenu.Enabled = _imageBackMenu.Enabled = image && !_printing;
            _imageAspectMenu.Checked = image && _preview.SelectedImage.KeepAspect;
            if (image)
            {
                _scheduleFontMenu.Enabled = _scheduleFontSizeMenu.Enabled = _scheduleBoldMenu.Enabled = _scheduleItalicMenu.Enabled = false;
                _deleteScheduleRowMenu.Text = "删除图片";
                _deleteScheduleRowMenu.Enabled = !_printing;
                _deleteScheduleColumnMenu.Enabled = false;
            }
        }

        private void SelectPreviewRow(int index, Keys modifiers)
        {
            if (index < 0 || index >= _items.Rows.Count || _printing) return;
            bool range = (modifiers & Keys.Shift) != 0, toggle = (modifiers & Keys.Control) != 0;
            if (range)
            {
                _items.ClearSelection();
                int start = Math.Min(_selectionAnchor, index), end = Math.Max(_selectionAnchor, index);
                for (int i = start; i <= end && i < _items.Rows.Count; i++) _items.Rows[i].Selected = true;
            }
            else if (toggle) _items.Rows[index].Selected = !_items.Rows[index].Selected;
            else if (!_items.Rows[index].Selected)
            {
                _items.ClearSelection();
                _items.CurrentCell = _items.Rows[index].Cells["Type"];
                _items.Rows[index].Selected = true;
            }
            if (!range) _selectionAnchor = index;
            _contextRowIndex = index;
        }

        private bool CanAddRow()
        {
            if (_printing) return false;
            int count = _items.Rows.Cast<DataGridViewRow>().Sum(row => Math.Max(1, GetRowMetadata(row).RowSpan));
            if (count < 200) return true;
            _status.Text = "应用状态：日程最多支持 200 行。";
            return false;
        }

        private void AddDateItem()
        {
            AddSimpleItem(ThermalScheduleItemKind.Date, ThermalScheduleItem.DateToken);
        }
        private void AddEmptyItem() { AddSimpleItem(ThermalScheduleItemKind.Schedule, string.Empty); }
        private void AddSimpleItem(ThermalScheduleItemKind kind, string content)
        {
            if (!CanAddRow()) return;
            _items.EndEdit();
            int index = _items.CurrentRow == null ? _items.Rows.Count : _items.CurrentRow.Index + 1;
            _items.Rows.Insert(index, GetItemKindText(kind), false, string.Empty, content, string.Empty);
            _items.Rows[index].Tag = new ThermalScheduleItem { Kind = kind, Content = content, RowSpan = 1 };
            ApplyRowKindStyle(_items.Rows[index]);
            _items.ClearSelection();
            _items.CurrentCell = _items.Rows[index].Cells["Type"];
            _items.Rows[index].Selected = true;
            _preview.SelectImage(null);
            ScheduleChanged(this, EventArgs.Empty);
        }
        private void InsertInlineDate()
        {
            DataGridViewRow row = GetContextRow();
            if (row == null || _printing || !_showContentColumn) return;
            DataGridViewCell cell = row.Cells["Content"];
            if (!ReferenceEquals(_items.CurrentCell, cell)) { _items.EndEdit(); _items.CurrentCell = cell; }
            bool editing = _items.IsCurrentCellInEditMode;
            _items.BeginEdit(false);
            TextBoxBase editor = _items.EditingControl as TextBoxBase;
            if (editor != null)
            {
                if (!editing) { editor.SelectionStart = editor.TextLength; editor.SelectionLength = 0; }
                if (editor.TextLength - editor.SelectionLength + ThermalScheduleItem.DateToken.Length <= 500)
                    editor.SelectedText = ThermalScheduleItem.DateToken;
            }
            else cell.Value = Convert.ToString(cell.Value) + ThermalScheduleItem.DateToken;
            _items.EndEdit();
            ScheduleChanged(this, EventArgs.Empty);
        }
        private void ChangeRowGrouping(bool split)
        {
            if (_printing) return;
            try
            {
                int[] selection = GetSelectedRowIndices();
                ThermalScheduleDocument document = BuildDocument(true);
                decimal mergedHeight = 0m;
                if (!split && ThermalScheduleRowOperations.CanMerge(document.Items, selection))
                {
                    IList<RectangleF> bounds = ThermalScheduleRenderer.GetRowBounds(document);
                    mergedHeight = selection.Sum(selected => (decimal)bounds[selected].Height / 8m);
                }
                int index = selection.Length == 0 ? 0 : selection[0];
                if (split) ThermalScheduleRowOperations.Split(document.Items, selection);
                else index = ThermalScheduleRowOperations.Merge(document.Items, selection);
                if (!split) document.Items[index].MergedHeightMm = mergedHeight;
                LoadDocument(document);
                _items.ClearSelection();
                _items.CurrentCell = _items.Rows[index].Cells["Type"];
                _items.Rows[index].Selected = true;
                UpdatePreview();
            }
            catch (Exception exception) { _status.Text = "应用状态：" + exception.Message; }
        }
        private void ImportScheduleImage()
        {
            if (_printing) return;
            if (_images.Count >= 30) { _status.Text = "应用状态：最多支持 30 个图片对象。"; return; }
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "导入日程图片",
                Filter = "常用图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
                CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ImageImportData data = ImageAssetService.Import(dialog.FileName);
                    decimal width = Math.Min(30m, 58m - _margin.Value * 2m);
                    decimal height = width * data.PixelHeight / data.PixelWidth;
                    if (height > 100m) { width *= 100m / height; height = 100m; }
                    ThermalScheduleImage image = new ThermalScheduleImage { FileName = data.FileName, ImageData = data.PngBase64,
                        X = _margin.Value, Y = Math.Max(_margin.Value, _preview.ReceiptHeightMm),
                        Width = width, Height = height, KeepAspect = true, Threshold = 128, Dither = true };
                    image.Normalize();
                    _images.Add(image);
                    UpdatePreview();
                    _preview.SelectImage(image);
                    _preview.Focus();
                    _preview.AutoScrollPosition = new Point(0, (int)Math.Max(0, image.Y * 8m - 60m));
                    _status.Text = "应用状态：拖动图片移动；拖动八个手柄缩放；右键可切换比例锁定，Shift 临时等比缩放。";
                }
                catch (Exception exception) { MessageBox.Show(this, exception.Message, "无法导入图片", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        private void DeleteSelectedImage()
        {
            if (_printing || _preview.SelectedImage == null) return;
            _images.Remove(_preview.SelectedImage);
            _preview.SelectImage(null);
            UpdatePreview();
        }
        private void ReorderImage(bool front)
        {
            ThermalScheduleImage image = _preview.SelectedImage;
            if (image == null || _printing) return;
            _images.Remove(image);
            if (front) _images.Add(image); else _images.Insert(0, image);
            UpdatePreview();
        }
        private void EditSelectedImage()
        {
            ThermalScheduleImage image = _preview.SelectedImage;
            if (image == null || _printing) return;
            using (Form dialog = new Form { Text = "图片位置和大小 (mm)", ClientSize = new Size(390, 360),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false, AutoScaleMode = AutoScaleMode.Dpi })
            {
                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 9 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
                NumericUpDown x = CreateNumeric(0, 57, image.X, 0.125m, 3), y = CreateNumeric(0, 999, image.Y, 0.125m, 3);
                NumericUpDown w = CreateNumeric(1, 58, image.Width, 0.125m, 3), h = CreateNumeric(1, 990, image.Height, 0.125m, 3);
                NumericUpDown threshold = CreateNumeric(0, 255, image.Threshold, 1, 0);
                CheckBox aspect = new CheckBox { Text = "锁定纵横比", Checked = image.KeepAspect, Dock = DockStyle.Fill };
                CheckBox dither = new CheckBox { Text = "黑白抖动（适合照片）", Checked = image.Dither, Dock = DockStyle.Fill };
                string[] names = { "X", "Y", "宽度", "高度", "黑白阈值" };
                Control[] controls = { x, y, w, h, threshold };
                for (int i = 0; i < 5; i++) { layout.Controls.Add(new Label { Text = names[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i); layout.Controls.Add(controls[i], 1, i); }
                layout.Controls.Add(aspect, 0, 5); layout.SetColumnSpan(aspect, 2);
                layout.Controls.Add(dither, 0, 6); layout.SetColumnSpan(dither, 2);
                layout.Controls.Add(new Label { Text = "取消锁定可分别调整宽高；Shift 拖动时临时等比。", AutoSize = true }, 0, 7); layout.SetColumnSpan(layout.GetControlFromPosition(0, 7), 2);
                FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                Button ok = new Button { Text = "确定", DialogResult = DialogResult.OK }, cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
                buttons.Controls.Add(ok); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 8); layout.SetColumnSpan(buttons, 2);
                dialog.AcceptButton = ok; dialog.CancelButton = cancel; dialog.Controls.Add(layout);
                decimal ratio = image.Width / image.Height;
                bool sizing = false;
                w.ValueChanged += (sender, args) => { if (sizing || !aspect.Checked) return; sizing = true; SetNumeric(h, w.Value / ratio); sizing = false; };
                h.ValueChanged += (sender, args) => { if (sizing || !aspect.Checked) return; sizing = true; SetNumeric(w, h.Value * ratio); sizing = false; };
                aspect.CheckedChanged += (sender, args) => { if (aspect.Checked) ratio = w.Value / h.Value; };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                image.X = x.Value; image.Y = y.Value; image.Width = w.Value; image.Height = h.Value;
                image.KeepAspect = aspect.Checked; image.Dither = dither.Checked; image.Threshold = (int)threshold.Value;
                image.Normalize();
                UpdatePreview();
            }
        }
    }
}
