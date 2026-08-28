using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed class ThermalSchedulePage : UserControl
    {
        private readonly ThermalPrinterService _printerService = new ThermalPrinterService();
        private readonly Timer _previewTimer = new Timer { Interval = 160 };
        private ComboBox _printers;
        private Label _deviceState;
        private Label _deviceDetail;
        private TextBox _title;
        private DateTimePicker _date;
        private CheckBox _showDate;
        private CheckBox _showCheckboxes;
        private ComboBox _fontFamily;
        private NumericUpDown _titleFontSize;
        private NumericUpDown _bodyFontSize;
        private NumericUpDown _margin;
        private NumericUpDown _rowSpacing;
        private NumericUpDown _copies;
        private DataGridView _items;
        private ThermalSchedulePreview _preview;
        private Label _previewInfo;
        private Label _status;
        private ProgressBar _progress;
        private Button _printButton;
        private bool _loading;
        private bool _printing;

        public ThermalSchedulePage()
        {
            BuildInterface();
            WireEvents();
            LoadDocument(ThermalScheduleDocument.CreateDefault());
            RefreshPrinters();
            UpdatePreview();
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));
            Controls.Add(root);

            root.Controls.Add(CreateDevicePanel(), 0, 0);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(1000, 600),
                Panel1MinSize = 430,
                Panel2MinSize = 360,
                SplitterDistance = 500
            };
            split.Panel1.Controls.Add(CreateEditorPanel());
            split.Panel2.Controls.Add(CreatePreviewPanel());
            root.Controls.Add(split, 0, 1);
            root.Controls.Add(CreatePrintPanel(), 0, 2);
        }

        private Control CreateDevicePanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 7, 10, 5), BackColor = Color.White };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 2,
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label name = new Label
            {
                Text = "Windows 打印机",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _printers = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            Button refresh = new Button { Text = "刷新设备", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
            Button properties = new Button { Text = "驱动说明", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
            _deviceState = new Label
            {
                Text = "未查询",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.WhiteSmoke
            };
            _deviceDetail = new Label
            {
                Text = "请先在 Windows 中安装 58mm 热敏打印机驱动。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray,
                AutoEllipsis = true
            };

            refresh.Click += (sender, args) => RefreshPrinters();
            properties.Click += (sender, args) => ShowDriverHint();
            layout.Controls.Add(name, 0, 0);
            layout.Controls.Add(_printers, 1, 0);
            layout.Controls.Add(refresh, 2, 0);
            layout.Controls.Add(properties, 3, 0);
            layout.Controls.Add(_deviceState, 4, 0);
            layout.Controls.Add(_deviceDetail, 0, 1);
            layout.SetColumnSpan(_deviceDetail, 5);
            panel.Controls.Add(layout);
            return panel;
        }

        private Control CreateEditorPanel()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 218f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(CreateScheduleSettings(), 0, 0);

            FlowLayoutPanel tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 2)
            };
            Button add = CreateToolButton("+ 添加日程", 96);
            Button remove = CreateToolButton("删除", 64);
            Button up = CreateToolButton("上移", 64);
            Button down = CreateToolButton("下移", 64);
            Button sample = CreateToolButton("恢复示例", 84);
            add.Click += (sender, args) => AddScheduleItem();
            remove.Click += (sender, args) => RemoveSelectedItem();
            up.Click += (sender, args) => MoveSelectedItem(-1);
            down.Click += (sender, args) => MoveSelectedItem(1);
            sample.Click += (sender, args) => LoadDocument(ThermalScheduleDocument.CreateDefault());
            tools.Controls.Add(add);
            tools.Controls.Add(remove);
            tools.Controls.Add(up);
            tools.Controls.Add(down);
            tools.Controls.Add(sample);
            layout.Controls.Add(tools, 0, 1);

            _items = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            _items.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Completed",
                HeaderText = "完成",
                Width = 52,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _items.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Time",
                HeaderText = "时间",
                Width = 76,
                MaxInputLength = 20,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _items.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Content",
                HeaderText = "日程内容",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MaxInputLength = 500,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            layout.Controls.Add(_items, 0, 2);
            return layout;
        }

        private Control CreateScheduleSettings()
        {
            GroupBox group = new GroupBox { Text = "日程设置", Dock = DockStyle.Fill };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5,
                Padding = new Padding(8, 5, 8, 5)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            for (int row = 0; row < 5; row++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            }

            _title = new TextBox { Dock = DockStyle.Fill, MaxLength = 80 };
            _date = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Long };
            _showDate = new CheckBox { Text = "打印日期", Dock = DockStyle.Fill };
            _showCheckboxes = new CheckBox { Text = "打印完成框", Dock = DockStyle.Fill };
            _fontFamily = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FontOption option in FontCatalog.GetOptions())
            {
                _fontFamily.Items.Add(option);
            }
            _titleFontSize = CreateNumeric(2.5m, 10m, 5m, 0.1m);
            _bodyFontSize = CreateNumeric(1.8m, 8m, 3.2m, 0.1m);
            _margin = CreateNumeric(1m, 10m, 3m, 0.5m);
            _rowSpacing = CreateNumeric(0.4m, 6m, 1.2m, 0.1m);
            _copies = CreateNumeric(1m, 99m, 1m, 1m, 0);

            AddSetting(table, 0, "标题", _title, "日期", _date);
            AddSetting(table, 1, "显示选项", _showDate, "", _showCheckboxes);
            AddSetting(table, 2, "字体", _fontFamily, "标题字号 (mm)", _titleFontSize);
            AddSetting(table, 3, "正文字号 (mm)", _bodyFontSize, "左右边距 (mm)", _margin);
            AddSetting(table, 4, "行内边距 (mm)", _rowSpacing, "打印份数", _copies);
            group.Controls.Add(table);
            return group;
        }

        private Control CreatePreviewPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Panel header = new Panel { Dock = DockStyle.Top, Height = 36 };
            Label title = new Label
            {
                Text = "58mm 日程打印预览",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };
            _previewInfo = new Label
            {
                Text = "58 × — mm",
                Dock = DockStyle.Right,
                Width = 190,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.DimGray
            };
            header.Controls.Add(title);
            header.Controls.Add(_previewInfo);
            _preview = new ThermalSchedulePreview { Dock = DockStyle.Fill };
            panel.Controls.Add(_preview);
            panel.Controls.Add(header);
            return panel;
        }

        private Control CreatePrintPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            _status = new Label
            {
                Text = "应用状态：就绪",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Blocks };
            _printButton = new Button
            {
                Text = "打印日程表",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.System,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            };
            table.Controls.Add(_status, 0, 0);
            table.Controls.Add(_progress, 0, 1);
            table.Controls.Add(_printButton, 1, 0);
            table.SetRowSpan(_printButton, 2);
            panel.Controls.Add(table);
            return panel;
        }

        private void WireEvents()
        {
            _previewTimer.Tick += (sender, args) =>
            {
                _previewTimer.Stop();
                UpdatePreview();
            };
            _printers.SelectedIndexChanged += (sender, args) => UpdatePrinterStatus();
            _title.TextChanged += ScheduleChanged;
            _date.ValueChanged += ScheduleChanged;
            _showDate.CheckedChanged += ScheduleChanged;
            _showCheckboxes.CheckedChanged += ScheduleChanged;
            _fontFamily.SelectedIndexChanged += ScheduleChanged;
            _titleFontSize.ValueChanged += ScheduleChanged;
            _bodyFontSize.ValueChanged += ScheduleChanged;
            _margin.ValueChanged += ScheduleChanged;
            _rowSpacing.ValueChanged += ScheduleChanged;
            _copies.ValueChanged += ScheduleChanged;
            _items.CellEndEdit += (sender, args) => ScheduleChanged(sender, EventArgs.Empty);
            _items.CellValueChanged += (sender, args) => ScheduleChanged(sender, EventArgs.Empty);
            _items.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (_items.IsCurrentCellDirty)
                {
                    _items.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _items.DataError += (sender, args) => { args.ThrowException = false; };
            _printButton.Click += async (sender, args) => await PrintAsync();
        }

        private void RefreshPrinters()
        {
            string previous = Convert.ToString(_printers.SelectedItem) ?? string.Empty;
            try
            {
                string defaultPrinter = _printerService.GetDefaultPrinterName();
                IList<string> printers = _printerService.GetInstalledPrinters();
                _loading = true;
                _printers.Items.Clear();
                foreach (string printer in printers)
                {
                    _printers.Items.Add(printer);
                }
                string preferred = printers.FirstOrDefault(item =>
                    string.Equals(item, previous, StringComparison.OrdinalIgnoreCase)) ??
                    printers.FirstOrDefault(item => _printerService.IsLikelyThermalPrinter(item)) ??
                    printers.FirstOrDefault(item =>
                        string.Equals(item, defaultPrinter, StringComparison.OrdinalIgnoreCase)) ??
                    printers.FirstOrDefault();
                if (preferred != null)
                {
                    _printers.SelectedItem = preferred;
                }
                _loading = false;
                UpdatePrinterStatus();
            }
            catch (Exception exception)
            {
                _loading = false;
                SetDeviceState("刷新失败", Color.MistyRose, exception.Message, Color.Firebrick);
            }
        }

        private void UpdatePrinterStatus()
        {
            if (_loading)
            {
                return;
            }
            string printer = Convert.ToString(_printers.SelectedItem) ?? string.Empty;
            ThermalPrinterStatus status;
            try
            {
                status = _printerService.GetStatus(printer);
            }
            catch (Exception exception)
            {
                SetDeviceState("查询失败", Color.MistyRose, exception.Message, Color.Firebrick);
                return;
            }
            if (status.IsValid && !status.HasError)
            {
                string state = status.StateText + (status.IsDefault ? "（默认）" : string.Empty);
                Color background = string.Equals(status.StateText, "就绪", StringComparison.Ordinal)
                    ? Color.Honeydew
                    : Color.LightYellow;
                SetDeviceState(state, background, status.Description, Color.DarkGreen);
            }
            else
            {
                SetDeviceState(status.StateText ?? "不可用", Color.MistyRose, status.Description, Color.Firebrick);
            }
        }

        private void ShowDriverHint()
        {
            string printer = Convert.ToString(_printers.SelectedItem) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(printer))
            {
                MessageBox.Show(this, "请先选择 Windows 打印机。", "打印机属性",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            MessageBox.Show(this,
                "所选打印机：" + printer + "\r\n\r\n" +
                "纸宽由本页面固定为 58 mm。驱动中的介质类型、切纸和浓度请在 Windows“打印机首选项”中设置。",
                "58mm 打印机设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadDocument(ThermalScheduleDocument document)
        {
            document.Normalize();
            _loading = true;
            _title.Text = document.Title;
            _date.Value = document.Date;
            _showDate.Checked = document.ShowDate;
            _showCheckboxes.Checked = document.ShowCheckboxes;
            SelectFont(document.FontFamily);
            SetNumeric(_titleFontSize, document.TitleFontSizeMm);
            SetNumeric(_bodyFontSize, document.BodyFontSizeMm);
            SetNumeric(_margin, document.MarginMm);
            SetNumeric(_rowSpacing, document.RowSpacingMm);
            SetNumeric(_copies, document.Copies);
            _items.Rows.Clear();
            foreach (ThermalScheduleItem item in document.Items)
            {
                _items.Rows.Add(item.Completed, item.Time, item.Content);
            }
            _loading = false;
            ScheduleChanged(this, EventArgs.Empty);
        }

        private ThermalScheduleDocument BuildDocument()
        {
            _items.EndEdit();
            FontOption font = _fontFamily.SelectedItem as FontOption;
            ThermalScheduleDocument document = new ThermalScheduleDocument
            {
                Title = _title.Text,
                Date = _date.Value.Date,
                ShowDate = _showDate.Checked,
                ShowCheckboxes = _showCheckboxes.Checked,
                FontFamily = font == null ? FontCatalog.DefaultSansFamily : font.FamilyName,
                TitleFontSizeMm = _titleFontSize.Value,
                BodyFontSizeMm = _bodyFontSize.Value,
                MarginMm = _margin.Value,
                RowSpacingMm = _rowSpacing.Value,
                Copies = Decimal.ToInt32(_copies.Value),
                Items = new List<ThermalScheduleItem>()
            };
            foreach (DataGridViewRow row in _items.Rows)
            {
                document.Items.Add(new ThermalScheduleItem
                {
                    Completed = Convert.ToBoolean(row.Cells["Completed"].Value ?? false),
                    Time = Convert.ToString(row.Cells["Time"].Value) ?? string.Empty,
                    Content = Convert.ToString(row.Cells["Content"].Value) ?? string.Empty
                });
            }
            document.Normalize();
            return document;
        }

        private void UpdatePreview()
        {
            if (IsDisposed)
            {
                return;
            }
            try
            {
                _preview.SetDocument(BuildDocument());
                _previewInfo.Text = string.Format("58 × {0:0.#} mm  |  203 dpi", _preview.ReceiptHeightMm);
            }
            catch (Exception exception)
            {
                _previewInfo.Text = "预览失败";
                _status.Text = "应用状态：" + exception.Message;
            }
        }

        private async Task PrintAsync()
        {
            if (_printing)
            {
                return;
            }
            string printer = Convert.ToString(_printers.SelectedItem) ?? string.Empty;
            ThermalPrinterStatus printerStatus;
            try
            {
                printerStatus = _printerService.GetStatus(printer);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "无法查询打印机",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!printerStatus.IsValid || printerStatus.HasError)
            {
                MessageBox.Show(this, printerStatus.Description, "不能打印日程表",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ThermalScheduleDocument document;
            try
            {
                document = BuildDocument();
                using (Bitmap validation = ThermalScheduleRenderer.Render(document)) { }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "日程内容无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _printing = true;
            SetEditingEnabled(false);
            _progress.Style = ProgressBarStyle.Marquee;
            _status.Text = "应用状态：正在向 58mm 打印机发送日程表…";
            try
            {
                await Task.Run(() => _printerService.Print(printer, document));
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Maximum = 100;
                _progress.Value = 100;
                _status.Text = "应用状态：日程表已提交到 Windows 打印队列。";
            }
            catch (Exception exception)
            {
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Value = 0;
                _status.Text = "应用状态：打印失败 — " + exception.Message;
                MessageBox.Show(this, exception.Message, "58mm 打印失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _printing = false;
                SetEditingEnabled(true);
            }
        }

        private void ScheduleChanged(object sender, EventArgs args)
        {
            if (_loading)
            {
                return;
            }
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void AddScheduleItem()
        {
            int rowIndex = _items.Rows.Add(false, DateTime.Now.ToString("HH:mm"), "新日程");
            _items.ClearSelection();
            _items.Rows[rowIndex].Selected = true;
            _items.CurrentCell = _items.Rows[rowIndex].Cells["Content"];
            _items.BeginEdit(true);
            ScheduleChanged(this, EventArgs.Empty);
        }

        private void RemoveSelectedItem()
        {
            if (_items.CurrentRow == null)
            {
                return;
            }
            int index = _items.CurrentRow.Index;
            _items.Rows.RemoveAt(index);
            if (_items.Rows.Count > 0)
            {
                int next = Math.Min(index, _items.Rows.Count - 1);
                _items.Rows[next].Selected = true;
                _items.CurrentCell = _items.Rows[next].Cells["Content"];
            }
            ScheduleChanged(this, EventArgs.Empty);
        }

        private void MoveSelectedItem(int offset)
        {
            if (_items.CurrentRow == null)
            {
                return;
            }
            int source = _items.CurrentRow.Index;
            int target = source + offset;
            if (target < 0 || target >= _items.Rows.Count)
            {
                return;
            }

            object[] values = GetRowValues(_items.Rows[source]);
            _items.Rows.RemoveAt(source);
            _items.Rows.Insert(target, values);
            _items.ClearSelection();
            _items.Rows[target].Selected = true;
            _items.CurrentCell = _items.Rows[target].Cells["Content"];
            ScheduleChanged(this, EventArgs.Empty);
        }

        private static object[] GetRowValues(DataGridViewRow row)
        {
            return row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value).ToArray();
        }

        private void SetEditingEnabled(bool enabled)
        {
            _printers.Enabled = enabled;
            _title.Enabled = enabled;
            _date.Enabled = enabled;
            _showDate.Enabled = enabled;
            _showCheckboxes.Enabled = enabled;
            _fontFamily.Enabled = enabled;
            _titleFontSize.Enabled = enabled;
            _bodyFontSize.Enabled = enabled;
            _margin.Enabled = enabled;
            _rowSpacing.Enabled = enabled;
            _copies.Enabled = enabled;
            _items.Enabled = enabled;
            _printButton.Enabled = enabled;
        }

        private void SetDeviceState(string state, Color background, string detail, Color foreground)
        {
            _deviceState.Text = state;
            _deviceState.BackColor = background;
            _deviceState.ForeColor = foreground;
            _deviceDetail.Text = detail;
            _deviceDetail.ForeColor = foreground;
        }

        private void SelectFont(string familyName)
        {
            string resolved = FontCatalog.ResolveFamily(familyName);
            foreach (FontOption option in _fontFamily.Items)
            {
                if (string.Equals(option.FamilyName, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    _fontFamily.SelectedItem = option;
                    return;
                }
            }
            _fontFamily.SelectedIndex = _fontFamily.Items.Count > 0 ? 0 : -1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewTimer.Stop();
                _previewTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Button CreateToolButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.System
            };
        }

        private static NumericUpDown CreateNumeric(
            decimal minimum,
            decimal maximum,
            decimal value,
            decimal increment,
            int decimalPlaces = 1)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimalPlaces
            };
        }

        private static void SetNumeric(NumericUpDown control, decimal value)
        {
            control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
        }

        private static void AddSetting(
            TableLayoutPanel table,
            int row,
            string leftName,
            Control leftControl,
            string rightName,
            Control rightControl)
        {
            Label leftLabel = new Label
            {
                Text = leftName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            Label rightLabel = new Label
            {
                Text = rightName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            leftControl.Margin = new Padding(3, 4, 3, 4);
            rightControl.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(leftLabel, 0, row);
            table.Controls.Add(leftControl, 1, row);
            table.Controls.Add(rightLabel, 2, row);
            table.Controls.Add(rightControl, 3, row);
        }
    }
}
