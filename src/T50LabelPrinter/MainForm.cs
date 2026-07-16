using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Supvan.T50PRO.SDK;

namespace T50LabelPrinter
{
    public sealed class MainForm : Form
    {
        private readonly PrinterService _printer = new PrinterService();
        private readonly Timer _timer = new Timer();
        private LabelDocument _document;
        private bool _loading;
        private bool _statusBusy;
        private bool _isPrinting;
        private bool _syncingImageSize;
        private DateTime _previewTimestamp = DateTime.Now;
        private Image _brandImage;

        private ComboBox _devicePaths;
        private Button _scanButton;
        private Button _queryButton;
        private Label _deviceState;
        private Label _deviceDetail;

        private TabControl _tabs;
        private LabelCanvas _canvas;

        private NumericUpDown _labelWidth;
        private NumericUpDown _labelHeight;
        private NumericUpDown _gap;
        private ComboBox _paperType;
        private ComboBox _direction;
        private NumericUpDown _speed;
        private ComboBox _deepness;
        private NumericUpDown _copies;
        private CheckBox _oneByOne;
        private ComboBox _guideMode;
        private CheckBox _printGuide;
        private NumericUpDown _guideThickness;
        private CheckBox _savePaperDefaults;

        private ListBox _elementList;
        private Label _elementKind;
        private NumericUpDown _elementX;
        private NumericUpDown _elementY;
        private NumericUpDown _elementWidth;
        private NumericUpDown _elementHeight;
        private TextBox _textContent;
        private ComboBox _fontFamily;
        private NumericUpDown _fontSize;
        private CheckBox _bold;
        private ComboBox _align;
        private TextBox _pdfPrefix;
        private CheckBox _pdfUseTimestamp;
        private TextBox _pdfPayload;
        private CheckBox _printBarcodesToggle;
        private CheckBox _printDigits;
        private TextBox _digitsText;
        private Label _pdfEncodedContent;

        private TabPage _imageTab;
        private NumericUpDown _imageWidth;
        private NumericUpDown _imageHeight;
        private NumericUpDown _imageThreshold;
        private CheckBox _imageDither;
        private CheckBox _imageKeepAspect;
        private Label _imageInfo;
        private CheckBox _autoRefresh;

        private Button _printButton;
        private ProgressBar _progress;
        private Label _printState;
        private Label _sdkStatusLine;

        public MainForm()
        {
            Text = "硕方t50pro打印上位机（by zlight106）";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 640);
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(
                Math.Max(880, Math.Min(1280, workingArea.Width - 32)),
                Math.Max(600, Math.Min(800, workingArea.Height - 32)));
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            BuildInterface();
            WireEvents();
            LabelDocument initialDocument = LabelDocument.CreateDefault();
            PaperDefaults paperDefaults;
            bool hasPaperDefaults = ApplicationSettingsStore.TryLoad(out paperDefaults);
            if (hasPaperDefaults)
            {
                paperDefaults.ApplyTo(initialDocument);
            }
            LoadDocument(initialDocument);
            _loading = true;
            _savePaperDefaults.Checked = hasPaperDefaults;
            UpdatePaperDefaultsText();
            _loading = false;
            _canvas.PreviewTimestamp = _previewTimestamp;

            _timer.Interval = 1000;
            _timer.Tick += async (sender, args) =>
            {
                if (_autoRefresh.Checked)
                {
                    _previewTimestamp = DateTime.Now;
                    _canvas.PreviewTimestamp = _previewTimestamp;
                    UpdateEncodedContent();
                }
                if (_isPrinting)
                {
                    await QueryStatusAsync(false);
                }
            };
            _timer.Start();

            Shown += async (sender, args) => await ScanDevicesAsync();
            FormClosed += (sender, args) =>
            {
                _timer.Stop();
                if (_brandImage != null)
                {
                    _brandImage.Dispose();
                    _brandImage = null;
                }
                // SDK 的部分版本会保留工作线程。程序是单窗体工具，主窗体关闭后应立即退出进程。
                Environment.Exit(0);
            };
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116f));
            Controls.Add(root);

            root.Controls.Add(CreateDevicePanel(), 0, 0);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.FixedSingle
            };
            split.Size = new Size(Math.Max(860, ClientSize.Width), Math.Max(470, ClientSize.Height - 168));
            split.Panel1MinSize = 340;
            split.Panel2MinSize = 420;
            split.SplitterDistance = 380;
            root.Controls.Add(split, 0, 1);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(CreateLabelTab());
            _tabs.TabPages.Add(CreateElementTab());
            _tabs.TabPages.Add(CreateFileTab());
            _tabs.TabPages.Add(CreateImageTab());
            split.Panel1.Controls.Add(_tabs);

            Panel previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Panel previewHeader = new Panel { Dock = DockStyle.Top, Height = 34 };
            Label previewTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "标签预览（拖动对象；拖右下角蓝点缩放；双击文字快速编辑）",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };
            _autoRefresh = new CheckBox
            {
                Appearance = Appearance.Button,
                Dock = DockStyle.Right,
                Width = 112,
                Text = "✓ 自动刷新",
                Checked = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(4, 2, 0, 2)
            };
            previewHeader.Controls.Add(previewTitle);
            previewHeader.Controls.Add(_autoRefresh);
            _canvas = new LabelCanvas { Dock = DockStyle.Fill };
            previewPanel.Controls.Add(_canvas);
            previewPanel.Controls.Add(previewHeader);
            split.Panel2.Controls.Add(previewPanel);

            root.Controls.Add(CreatePrintPanel(), 0, 2);
        }

        private Control CreateDevicePanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 10, 5), BackColor = Color.White };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _brandImage = LoadBrandImage();
            PictureBox brand = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = _brandImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 8, 0)
            };
            layout.Controls.Add(brand, 0, 0);
            layout.SetRowSpan(brand, 2);
            layout.Controls.Add(new Label
            {
                Text = "USB 打印机",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 4, 2)
            }, 1, 0);

            _devicePaths = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 6, 3)
            };
            _scanButton = new Button { Text = "刷新设备", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 6, 3) };
            _queryButton = new Button { Text = "查询状态", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 6, 3) };
            _deviceState = new Label
            {
                Text = "未查询",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 0, 4)
            };
            layout.Controls.Add(_devicePaths, 2, 0);
            layout.Controls.Add(_scanButton, 3, 0);
            layout.Controls.Add(_queryButton, 4, 0);
            layout.Controls.Add(_deviceState, 5, 0);

            _deviceDetail = new Label
            {
                Dock = DockStyle.Fill,
                Text = "请连接 T50 Pro USB 数据线并打开打印机。",
                ForeColor = Color.DimGray,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 0)
            };
            layout.Controls.Add(_deviceDetail, 1, 1);
            layout.SetColumnSpan(_deviceDetail, 5);
            panel.Controls.Add(layout);
            return panel;
        }

        private TabPage CreateLabelTab()
        {
            TabPage tab = new TabPage("标签与纸张") { Padding = new Padding(8) };
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel table = CreatePropertyTable();

            _labelWidth = CreateNumeric(5m, 50m, 50m, 1m, 0);
            _labelHeight = CreateNumeric(5m, 200m, 30m, 1m, 0);
            _gap = CreateNumeric(0m, 20m, 3m, 1m, 0);
            _paperType = CreateCombo("间隙纸", "中间黑标", "黑标卡纸");
            _direction = CreateCombo("向上打印", "向下打印", "向左打印", "向右打印");
            _savePaperDefaults = new CheckBox
            {
                Text = "设为默认",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _speed = CreateNumeric(20m, 60m, 40m, 5m, 0);
            _deepness = CreateCombo("0 - 最淡", "1", "2", "3", "4 - 标准", "5", "6", "7", "8", "9 - 最深");
            _copies = CreateNumeric(1m, 99m, 1m, 1m, 0);
            _oneByOne = new CheckBox { Text = "逐份打印", Dock = DockStyle.Fill, Checked = true, TextAlign = ContentAlignment.MiddleLeft };
            _guideMode = CreateCombo("无", "垂直中心线", "水平中心线", "十字中心线");
            _printGuide = new CheckBox { Text = "把辅助线印到标签上", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _guideThickness = CreateNumeric(0.1m, 2m, 0.25m, 0.05m, 2);

            AddPropertyRow(table, "标签宽度 (mm)", _labelWidth);
            AddPropertyRow(table, "标签高度 (mm)", _labelHeight);
            AddPropertyRow(table, "纸张间隙 (mm)", _gap);
            AddPropertyRow(table, "纸张类型", _paperType);
            AddPropertyRow(table, "打印方向", _direction);
            AddPropertyRow(table, "启动默认值", _savePaperDefaults);
            AddPropertyRow(table, "速度 (mm/s)", _speed);
            AddPropertyRow(table, "打印浓度", _deepness);
            AddPropertyRow(table, "打印份数", _copies);
            AddPropertyRow(table, "逐份模式", _oneByOne);
            AddPropertyRow(table, "居中辅助线", _guideMode);
            AddPropertyRow(table, "打印辅助线", _printGuide);
            AddPropertyRow(table, "辅助线粗细 (mm)", _guideThickness);

            Label widthHint = new Label
            {
                Text = "T50 Pro 标签宽度限制为 5–50 mm。勾选“设为默认”会保存宽度、高度、间隙和打印方向。辅助线始终在预览中显示，仅在勾选“打印辅助线”后印到标签上。",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.Firebrick,
                BackColor = Color.FromArgb(255, 247, 247),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8)
            };
            AddWideRow(table, widthHint, 92f);

            scroll.Controls.Add(table);
            tab.Controls.Add(scroll);
            return tab;
        }

        private TabPage CreateElementTab()
        {
            TabPage tab = new TabPage("标签内容") { Padding = new Padding(7) };
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
            Button addText = new Button { Text = "+ 文字", Width = 78, Height = 29 };
            Button addPdf = new Button { Text = "+ PDF417", Width = 92, Height = 29 };
            Button addDataMatrix = new Button { Text = "+ DataMatrix", Width = 112, Height = 29 };
            Button delete = new Button { Text = "删除", Width = 64, Height = 29 };
            _printBarcodesToggle = new CheckBox
            {
                Appearance = Appearance.Button,
                Text = "✓ 打印条码",
                Width = 112,
                Height = 29,
                Checked = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            addText.Click += (sender, args) => AddElement(LabelElement.CreateText(_document.WidthMm, _document.HeightMm));
            addPdf.Click += (sender, args) => AddElement(LabelElement.CreatePdf417(_document.WidthMm, _document.HeightMm));
            addDataMatrix.Click += (sender, args) => AddElement(LabelElement.CreateDataMatrix(_document.WidthMm, _document.HeightMm));
            delete.Click += (sender, args) => DeleteSelectedElement();
            tools.Controls.Add(addText);
            tools.Controls.Add(addPdf);
            tools.Controls.Add(addDataMatrix);
            tools.Controls.Add(delete);
            tools.Controls.Add(_printBarcodesToggle);
            root.Controls.Add(tools, 0, 0);

            _elementList = new ListBox { Dock = DockStyle.Fill, DisplayMember = "DisplayName", IntegralHeight = false };
            root.Controls.Add(_elementList, 0, 1);

            Panel propertyScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel table = CreatePropertyTable();
            _elementKind = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = "未选择" };
            _elementX = CreateNumeric(0m, 200m, 0m, 0.1m, 1);
            _elementY = CreateNumeric(0m, 200m, 0m, 0.1m, 1);
            _elementWidth = CreateNumeric(1m, 200m, 10m, 0.1m, 1);
            _elementHeight = CreateNumeric(1m, 200m, 5m, 0.1m, 1);
            _textContent = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 58 };
            _fontFamily = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FontOption option in FontCatalog.GetOptions())
            {
                _fontFamily.Items.Add(option);
            }
            _fontSize = CreateNumeric(0.8m, 20m, 4m, 0.1m, 1);
            _bold = new CheckBox { Text = "加粗", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _align = CreateCombo("左对齐", "居中", "右对齐");
            _pdfPrefix = new TextBox { Dock = DockStyle.Fill, MaxLength = 3, CharacterCasing = CharacterCasing.Upper };
            _pdfUseTimestamp = new CheckBox { Text = "自动时间（精确到秒）", Dock = DockStyle.Fill, Checked = true };
            _pdfPayload = new TextBox { Dock = DockStyle.Fill };
            _printDigits = new CheckBox { Text = "随条码打印附加数位码", Dock = DockStyle.Fill };
            _digitsText = new TextBox { Dock = DockStyle.Fill };
            _pdfEncodedContent = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.Navy,
                TextAlign = ContentAlignment.MiddleLeft
            };

            AddPropertyRow(table, "对象类型", _elementKind);
            AddPropertyRow(table, "X (mm)", _elementX);
            AddPropertyRow(table, "Y (mm)", _elementY);
            AddPropertyRow(table, "宽 (mm)", _elementWidth);
            AddPropertyRow(table, "高 (mm)", _elementHeight);
            AddPropertyRow(table, "文字内容", _textContent, 66f);
            AddPropertyRow(table, "字体", _fontFamily);
            AddPropertyRow(table, "字号高度 (mm)", _fontSize);
            AddPropertyRow(table, "字形", _bold);
            AddPropertyRow(table, "文字对齐", _align);
            AddPropertyRow(table, "条码头部", _pdfPrefix);
            AddPropertyRow(table, "条码内容", _pdfUseTimestamp);
            AddPropertyRow(table, "自定义字符串", _pdfPayload);
            AddPropertyRow(table, "附加数位码", _printDigits);
            AddPropertyRow(table, "数位码", _digitsText);
            AddPropertyRow(table, "实际编码", _pdfEncodedContent, 48f);
            propertyScroll.Controls.Add(table);
            root.Controls.Add(propertyScroll, 0, 2);
            tab.Controls.Add(root);
            return tab;
        }

        private TabPage CreateFileTab()
        {
            TabPage tab = new TabPage("文件") { Padding = new Padding(14) };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            Button open = new Button { Text = "打开标签模板…", Width = 210, Height = 34 };
            Button save = new Button { Text = "保存标签模板…", Width = 210, Height = 34 };
            Button export = new Button { Text = "导出打印预览 PNG…", Width = 210, Height = 34 };
            open.Click += (sender, args) => OpenTemplate();
            save.Click += (sender, args) => SaveTemplate();
            export.Click += (sender, args) => ExportPreview();
            flow.Controls.Add(open);
            flow.Controls.Add(save);
            flow.Controls.Add(export);
            layout.Controls.Add(flow, 0, 0);

            Label note = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 8, 0, 0),
                Text = "模板文件保存标签尺寸、纸张参数、文字、条码和已导入图片。\r\n\r\n" +
                       "条码可统一关闭；每个条码还可选择是否附印数位码。\r\n\r\n" +
                       "关闭“自动刷新”后，预览与打印会继续使用当前固定时间码。\r\n\r\n" +
                       "自动时间格式：yyyyMMddHHmmss，例如 ABC20260716153042。\r\n\r\n" +
                       "打印预览按设备的 8 点/mm（约 203 dpi）输出。",
                ForeColor = Color.DimGray
            };
            layout.Controls.Add(note, 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private TabPage CreateImageTab()
        {
            _imageTab = new TabPage("图片导入") { Padding = new Padding(8) };
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FlowLayoutPanel tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            Button import = new Button { Text = "导入图片…", Width = 118, Height = 32 };
            Button delete = new Button { Text = "删除当前图片", Width = 126, Height = 32 };
            import.Click += (sender, args) => ImportImage();
            delete.Click += (sender, args) =>
            {
                if (_canvas.SelectedElement != null && _canvas.SelectedElement.IsImage)
                {
                    DeleteSelectedElement();
                }
            };
            tools.Controls.Add(import);
            tools.Controls.Add(delete);
            root.Controls.Add(tools, 0, 0);

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel table = CreatePropertyTable();
            _imageInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "尚未选择图片",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _imageWidth = CreateNumeric(1m, 200m, 20m, 0.1m, 1);
            _imageHeight = CreateNumeric(1m, 200m, 20m, 0.1m, 1);
            _imageKeepAspect = new CheckBox { Text = "保持原图比例", Dock = DockStyle.Fill, Checked = true };
            _imageThreshold = CreateNumeric(0m, 255m, 128m, 1m, 0);
            _imageDither = new CheckBox { Text = "启用抖动（适合照片）", Dock = DockStyle.Fill, Checked = true };

            AddPropertyRow(table, "当前图片", _imageInfo, 48f);
            AddPropertyRow(table, "图片宽度 (mm)", _imageWidth);
            AddPropertyRow(table, "图片高度 (mm)", _imageHeight);
            AddPropertyRow(table, "缩放方式", _imageKeepAspect);
            AddPropertyRow(table, "黑白阈值", _imageThreshold);
            AddPropertyRow(table, "单色处理", _imageDither);
            Label hint = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 8, 4, 4),
                Text = "支持 PNG、JPG/JPEG、BMP、GIF 和 TIFF。导入后图片会嵌入模板，打印与预览都转换为纯黑白。可在此输入宽高，也可在画布中拖动右下角蓝点缩放。"
            };
            AddWideRow(table, hint, 94f);
            scroll.Controls.Add(table);
            root.Controls.Add(scroll, 0, 1);
            _imageTab.Controls.Add(root);
            return _imageTab;
        }

        private Control CreatePrintPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _printButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "打印标签",
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                BackColor = Color.FromArgb(31, 111, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 0, 0, 0)
            };
            _printButton.FlatAppearance.BorderSize = 0;
            _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Margin = new Padding(0, 5, 0, 0) };
            _printState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "应用状态：就绪",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _sdkStatusLine = new Label
            {
                Dock = DockStyle.Fill,
                Text = "SDK 状态：未查询  |  描述：—  |  错误：—  |  页数：0/0",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.DimGray
            };
            layout.Controls.Add(_printState, 0, 0);
            layout.Controls.Add(_sdkStatusLine, 0, 1);
            layout.Controls.Add(_progress, 0, 2);
            layout.Controls.Add(_printButton, 1, 0);
            layout.SetRowSpan(_printButton, 3);
            panel.Controls.Add(layout);
            return panel;
        }

        private void WireEvents()
        {
            _scanButton.Click += async (sender, args) => await ScanDevicesAsync();
            _queryButton.Click += async (sender, args) => await QueryStatusAsync(true);
            _printButton.Click += async (sender, args) => await PrintAsync();

            _devicePaths.SelectedIndexChanged += async (sender, args) =>
            {
                if (!_loading)
                {
                    await QueryStatusAsync(false);
                }
            };

            _labelWidth.ValueChanged += SettingsChanged;
            _labelHeight.ValueChanged += SettingsChanged;
            _gap.ValueChanged += SettingsChanged;
            _paperType.SelectedIndexChanged += SettingsChanged;
            _direction.SelectedIndexChanged += SettingsChanged;
            _speed.ValueChanged += SettingsChanged;
            _deepness.SelectedIndexChanged += SettingsChanged;
            _copies.ValueChanged += SettingsChanged;
            _oneByOne.CheckedChanged += SettingsChanged;
            _guideMode.SelectedIndexChanged += SettingsChanged;
            _printGuide.CheckedChanged += SettingsChanged;
            _guideThickness.ValueChanged += SettingsChanged;
            _savePaperDefaults.CheckedChanged += PaperDefaultsChanged;

            _elementList.SelectedIndexChanged += (sender, args) =>
            {
                if (!_loading)
                {
                    _canvas.SelectedElement = _elementList.SelectedItem as LabelElement;
                }
            };
            _canvas.SelectionChanged += (sender, args) =>
            {
                _loading = true;
                _elementList.SelectedItem = _canvas.SelectedElement;
                _loading = false;
                LoadSelectedElement();
                LoadSelectedImage();
            };
            _canvas.DocumentChanged += (sender, args) =>
            {
                LoadSelectedElement();
                LoadSelectedImage();
                _elementList.Invalidate();
            };
            _canvas.DeleteRequested += (sender, args) => DeleteSelectedElement();

            _elementX.ValueChanged += ElementPropertyChanged;
            _elementY.ValueChanged += ElementPropertyChanged;
            _elementWidth.ValueChanged += ElementPropertyChanged;
            _elementHeight.ValueChanged += ElementPropertyChanged;
            _textContent.TextChanged += ElementPropertyChanged;
            _fontFamily.SelectedIndexChanged += ElementPropertyChanged;
            _fontSize.ValueChanged += ElementPropertyChanged;
            _bold.CheckedChanged += ElementPropertyChanged;
            _align.SelectedIndexChanged += ElementPropertyChanged;
            _pdfUseTimestamp.CheckedChanged += ElementPropertyChanged;
            _pdfPayload.TextChanged += ElementPropertyChanged;
            _printDigits.CheckedChanged += ElementPropertyChanged;
            _digitsText.TextChanged += ElementPropertyChanged;
            _pdfPrefix.TextChanged += PdfPrefixChanged;
            _imageWidth.ValueChanged += ImagePropertyChanged;
            _imageHeight.ValueChanged += ImagePropertyChanged;
            _imageThreshold.ValueChanged += ImagePropertyChanged;
            _imageDither.CheckedChanged += ImagePropertyChanged;
            _imageKeepAspect.CheckedChanged += ImagePropertyChanged;
            _autoRefresh.CheckedChanged += AutoRefreshChanged;
            _printBarcodesToggle.CheckedChanged += (sender, args) =>
            {
                if (_loading || _document == null)
                {
                    return;
                }
                _document.PrintBarcodes = _printBarcodesToggle.Checked;
                _printBarcodesToggle.Text = _document.PrintBarcodes ? "✓ 打印条码" : "✕ 不打印条码";
                UpdateEncodedContent();
                _canvas.Invalidate();
            };
        }

        private async Task ScanDevicesAsync()
        {
            if (_isPrinting)
            {
                return;
            }

            string previous = GetSelectedDevice();
            SetDeviceUiBusy(true, "正在搜索 USB 打印机…");
            try
            {
                IList<string> paths = await Task.Run(() => _printer.GetDevicePaths());
                _loading = true;
                _devicePaths.Items.Clear();
                foreach (string path in paths)
                {
                    _devicePaths.Items.Add(path);
                }
                int previousIndex = string.IsNullOrWhiteSpace(previous) ? -1 : _devicePaths.Items.IndexOf(previous);
                if (previousIndex >= 0)
                {
                    _devicePaths.SelectedIndex = previousIndex;
                }
                else if (_devicePaths.Items.Count > 0)
                {
                    _devicePaths.SelectedIndex = _devicePaths.Items.Count - 1;
                }
                _loading = false;

                if (_devicePaths.Items.Count == 0)
                {
                    SetDeviceState("未连接", Color.MistyRose, "未找到 T50 Pro。请检查电源、USB 数据线和设备驱动。", Color.Firebrick);
                }
                else
                {
                    await QueryStatusAsync(false);
                }
            }
            catch (Exception exception)
            {
                _loading = false;
                SetDeviceState("搜索失败", Color.MistyRose, exception.Message, Color.Firebrick);
            }
            finally
            {
                SetDeviceUiBusy(false, null);
            }
        }

        private async Task<PrinterStatusSnapshot> QueryStatusAsync(bool showErrors)
        {
            string path = GetSelectedDevice();
            if (string.IsNullOrWhiteSpace(path))
            {
                if (showErrors)
                {
                    MessageBox.Show(this, "未选择 USB 打印机。请先单击“刷新设备”。", "查询状态", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return null;
            }
            if (_statusBusy)
            {
                return null;
            }

            _statusBusy = true;
            try
            {
                PrinterStatusSnapshot status = await Task.Run(() => _printer.GetStatus(path));
                if (status == null)
                {
                    SetDeviceState("无响应", Color.MistyRose, "SDK 未返回打印机状态。", Color.Firebrick);
                    SetSdkStatusText("SDK 状态：无响应  |  描述：—  |  错误：SDK 未返回打印机状态  |  页数：0/0", Color.Firebrick);
                    return null;
                }
                ApplyStatus(status);
                return status;
            }
            catch (Exception exception)
            {
                SetDeviceState("查询失败", Color.MistyRose, exception.Message, Color.Firebrick);
                SetSdkStatusText("SDK 状态：查询失败  |  描述：—  |  错误：" + SingleLine(exception.Message) + "  |  页数：0/0", Color.Firebrick);
                if (showErrors)
                {
                    MessageBox.Show(this, exception.Message, "查询状态失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return null;
            }
            finally
            {
                _statusBusy = false;
            }
        }

        private async Task PrintAsync()
        {
            if (_isPrinting)
            {
                return;
            }

            string validationError = ValidateDocument();
            if (validationError != null)
            {
                MessageBox.Show(this, validationError, "不能打印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string path = GetSelectedDevice();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "未选择 USB 打印机。请先刷新设备。", "不能打印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PrinterStatusSnapshot before = await QueryStatusAsync(true);
            if (before == null)
            {
                return;
            }
            if (before.State != DeviceState.Waiting)
            {
                MessageBox.Show(this, "打印机当前状态为“" + before.StateText + "”，只有“就绪”状态可以开始打印。", "打印机未就绪", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string temporaryBitmap = null;
            _isPrinting = true;
            _timer.Stop();
            SetEditingEnabled(false);
            _progress.Style = ProgressBarStyle.Marquee;
            _printState.Text = "应用状态：正在生成并发送标签数据…";
            DateTime timestamp = _autoRefresh.Checked ? DateTime.Now : _previewTimestamp;

            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "T50LabelPrinter");
                Directory.CreateDirectory(directory);
                temporaryBitmap = Path.Combine(directory, "label-" + Guid.NewGuid().ToString("N") + ".bmp");
                using (Bitmap bitmap = LabelRenderer.RenderForPrinter(_document, timestamp))
                {
                    bitmap.Save(temporaryBitmap, ImageFormat.Bmp);
                }

                bool accepted = await Task.Run(() => _printer.Print(_document, temporaryBitmap, path));
                PrinterStatusSnapshot finalStatus = null;
                for (int attempt = 0; attempt < 7; attempt++)
                {
                    await Task.Delay(attempt == 0 ? 250 : 450);
                    finalStatus = await QueryStatusAsync(false);
                    if (IsCompletedStatus(finalStatus) || IsFailureStatus(finalStatus))
                    {
                        break;
                    }
                }

                if (IsCompletedStatus(finalStatus))
                {
                    SetProgressComplete();
                    _printState.Text = accepted
                        ? "应用状态：打印完成（SDK 状态已确认）"
                        : "应用状态：打印完成（设备状态已确认；SDK 提交返回 false）";
                }
                else if (IsFailureStatus(finalStatus))
                {
                    _printState.Text = "应用状态：打印机报告失败" + BuildStatusDetail(finalStatus);
                    MessageBox.Show(this, _printState.Text, "打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (accepted)
                {
                    _printState.Text = "应用状态：打印任务已发送；请查看 SDK 状态栏确认设备进度。";
                }
                else
                {
                    _printState.Text = "应用状态：打印指令已调用；SDK 未返回提交确认，请以出纸结果和状态栏为准。";
                }
            }
            catch (Exception exception)
            {
                _printState.Text = "应用状态：打印失败 — " + exception.Message;
                MessageBox.Show(this, exception.Message, "打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isPrinting = false;
                if (_progress.Style == ProgressBarStyle.Marquee)
                {
                    _progress.Style = ProgressBarStyle.Blocks;
                    _progress.Maximum = 100;
                    _progress.Value = 0;
                }
                SetEditingEnabled(true);
                _timer.Start();
                if (!string.IsNullOrWhiteSpace(temporaryBitmap))
                {
                    try { File.Delete(temporaryBitmap); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private void SettingsChanged(object sender, EventArgs args)
        {
            if (_loading || _document == null)
            {
                return;
            }

            _document.WidthMm = _labelWidth.Value;
            _document.HeightMm = _labelHeight.Value;
            _document.GapMm = Decimal.ToInt32(_gap.Value);
            int[] paperTypes = { 1, 2, 5 };
            _document.PaperType = paperTypes[Math.Max(0, _paperType.SelectedIndex)];
            _document.Direction = Math.Max(0, _direction.SelectedIndex);
            _document.Speed = Decimal.ToInt32(_speed.Value);
            _document.Deepness = Math.Max(0, _deepness.SelectedIndex);
            _document.Copies = Decimal.ToInt32(_copies.Value);
            _document.OneByOne = _oneByOne.Checked;
            _document.GuideMode = (CenterGuideMode)Math.Max(0, _guideMode.SelectedIndex);
            _document.PrintGuide = _printGuide.Checked;
            _document.GuideThicknessMm = _guideThickness.Value;
            _printGuide.Enabled = _document.GuideMode != CenterGuideMode.None;
            _guideThickness.Enabled = _document.GuideMode != CenterGuideMode.None;

            foreach (LabelElement element in _document.Elements)
            {
                _document.ClampElement(element);
            }
            LoadSelectedElement();
            LoadSelectedImage();
            _canvas.Invalidate();
            if (_savePaperDefaults.Checked)
            {
                try
                {
                    SavePaperDefaults();
                }
                catch (IOException exception)
                {
                    _printState.Text = "应用状态：无法保存默认纸张参数 — " + exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    _printState.Text = "应用状态：无法保存默认纸张参数 — " + exception.Message;
                }
            }
        }

        private void PaperDefaultsChanged(object sender, EventArgs args)
        {
            if (_loading)
            {
                return;
            }
            try
            {
                if (_savePaperDefaults.Checked)
                {
                    SavePaperDefaults();
                }
                else
                {
                    ApplicationSettingsStore.Clear();
                }
                UpdatePaperDefaultsText();
            }
            catch (IOException exception)
            {
                _printState.Text = "应用状态：无法保存默认纸张参数 — " + exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                _printState.Text = "应用状态：无法保存默认纸张参数 — " + exception.Message;
            }
        }

        private void SavePaperDefaults()
        {
            if (_document == null)
            {
                return;
            }
            ApplicationSettingsStore.Save(new PaperDefaults
            {
                WidthMm = _document.WidthMm,
                HeightMm = _document.HeightMm,
                GapMm = _document.GapMm,
                Direction = _document.Direction
            });
        }

        private void UpdatePaperDefaultsText()
        {
            if (_savePaperDefaults != null)
            {
                _savePaperDefaults.Text = _savePaperDefaults.Checked ? "✓ 已设为默认" : "设为默认";
            }
        }

        private void AutoRefreshChanged(object sender, EventArgs args)
        {
            if (_loading)
            {
                return;
            }
            _previewTimestamp = DateTime.Now;
            _canvas.PreviewTimestamp = _previewTimestamp;
            _autoRefresh.Text = _autoRefresh.Checked ? "✓ 自动刷新" : "⏸ 时间已固定";
            UpdateEncodedContent();
        }

        private void ImagePropertyChanged(object sender, EventArgs args)
        {
            if (_loading || _syncingImageSize || _canvas.SelectedElement == null || !_canvas.SelectedElement.IsImage)
            {
                return;
            }

            LabelElement element = _canvas.SelectedElement;
            element.ImageKeepAspect = _imageKeepAspect.Checked;
            element.ImageDither = _imageDither.Checked;
            element.ImageThreshold = Decimal.ToInt32(_imageThreshold.Value);

            decimal aspect = element.ImagePixelWidth > 0 && element.ImagePixelHeight > 0
                ? (decimal)element.ImagePixelWidth / element.ImagePixelHeight
                : 1m;
            decimal width = _imageWidth.Value;
            decimal height = _imageHeight.Value;
            if (element.ImageKeepAspect)
            {
                if (ReferenceEquals(sender, _imageHeight))
                {
                    width = height * aspect;
                }
                else
                {
                    height = width / Math.Max(0.01m, aspect);
                }
            }
            element.Width = width;
            element.Height = height;
            _document.ClampElement(element);

            _syncingImageSize = true;
            _loading = true;
            SetNumeric(_imageWidth, element.Width);
            SetNumeric(_imageHeight, element.Height);
            SetNumeric(_elementWidth, element.Width);
            SetNumeric(_elementHeight, element.Height);
            _loading = false;
            _syncingImageSize = false;
            _canvas.Invalidate();
            _elementList.Invalidate();
        }

        private void ElementPropertyChanged(object sender, EventArgs args)
        {
            if (_loading || _canvas.SelectedElement == null)
            {
                return;
            }

            LabelElement element = _canvas.SelectedElement;
            element.X = _elementX.Value;
            element.Y = _elementY.Value;
            element.Width = _elementWidth.Value;
            element.Height = _elementHeight.Value;
            if (element.IsImage && element.ImageKeepAspect &&
                element.ImagePixelWidth > 0 && element.ImagePixelHeight > 0)
            {
                decimal aspect = (decimal)element.ImagePixelWidth / element.ImagePixelHeight;
                if (ReferenceEquals(sender, _elementWidth))
                {
                    element.Height = element.Width / aspect;
                }
                else if (ReferenceEquals(sender, _elementHeight))
                {
                    element.Width = element.Height * aspect;
                }
            }
            element.Text = _textContent.Text;
            FontOption font = _fontFamily.SelectedItem as FontOption;
            if (font != null)
            {
                element.FontFamily = font.FamilyName;
            }
            element.FontSizeMm = _fontSize.Value;
            element.Bold = _bold.Checked;
            element.Align = Math.Max(0, _align.SelectedIndex);
            element.PdfUseTimestamp = _pdfUseTimestamp.Checked;
            element.PdfPayload = _pdfPayload.Text;
            element.PrintDigits = _printDigits.Checked;
            string digits = new string((_digitsText.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            if (!string.Equals(_digitsText.Text, digits, StringComparison.Ordinal))
            {
                int caret = Math.Min(digits.Length, _digitsText.SelectionStart);
                _loading = true;
                _digitsText.Text = digits;
                _digitsText.SelectionStart = caret;
                _loading = false;
            }
            element.DigitsText = digits;
            _document.ClampElement(element);
            if (element.IsImage)
            {
                _loading = true;
                SetNumeric(_elementWidth, element.Width);
                SetNumeric(_elementHeight, element.Height);
                _loading = false;
            }
            _pdfPayload.Enabled = element.IsBarcode && !element.PdfUseTimestamp;
            _digitsText.Enabled = element.IsBarcode && element.PrintDigits;
            UpdateEncodedContent();
            LoadSelectedImage();
            _elementList.Invalidate();
            _canvas.Invalidate();
        }

        private void PdfPrefixChanged(object sender, EventArgs args)
        {
            if (_loading || _canvas.SelectedElement == null)
            {
                return;
            }

            string normalized = new string(_pdfPrefix.Text.Where(character =>
                (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z')).Take(3).ToArray()).ToUpperInvariant();
            if (!string.Equals(_pdfPrefix.Text, normalized, StringComparison.Ordinal))
            {
                int caret = Math.Min(normalized.Length, _pdfPrefix.SelectionStart);
                _loading = true;
                _pdfPrefix.Text = normalized;
                _pdfPrefix.SelectionStart = caret;
                _loading = false;
            }
            _canvas.SelectedElement.PdfPrefix = normalized;
            UpdateEncodedContent();
            _elementList.Invalidate();
            _canvas.Invalidate();
        }

        private void AddElement(LabelElement element)
        {
            _document.Elements.Add(element);
            RefreshElementList();
            _canvas.SelectedElement = element;
            _tabs.SelectedIndex = 1;
            _canvas.Invalidate();
        }

        private void ImportImage()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "导入标签图片",
                Filter = "支持的图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 图片 (*.bmp)|*.bmp|GIF 图片 (*.gif)|*.gif|TIFF 图片 (*.tif;*.tiff)|*.tif;*.tiff",
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    ImageImportData image = ImageAssetService.Import(dialog.FileName);
                    LabelElement element = LabelElement.CreateImage(
                        _document.WidthMm,
                        _document.HeightMm,
                        image.PngBase64,
                        image.FileName,
                        image.PixelWidth,
                        image.PixelHeight);
                    AddElement(element);
                    _tabs.SelectedTab = _imageTab;
                    LoadSelectedImage();
                    _printState.Text = "应用状态：已导入图片 " + image.FileName + "，打印时自动转换为单色。";
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is InvalidDataException ||
                    exception is OutOfMemoryException)
                {
                    MessageBox.Show(this, exception.Message, "无法导入图片", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadSelectedImage()
        {
            if (_imageInfo == null)
            {
                return;
            }
            LabelElement element = _canvas == null ? null : _canvas.SelectedElement;
            bool isImage = element != null && element.IsImage;
            _loading = true;
            _imageInfo.Text = isImage
                ? (element.ImageFileName ?? "未命名图片") + "  " + element.ImagePixelWidth + "×" + element.ImagePixelHeight + " px"
                : "尚未选择图片";
            foreach (Control control in new Control[]
            {
                _imageWidth, _imageHeight, _imageThreshold, _imageDither, _imageKeepAspect
            })
            {
                control.Enabled = isImage;
            }
            if (isImage)
            {
                SetNumeric(_imageWidth, element.Width);
                SetNumeric(_imageHeight, element.Height);
                SetNumeric(_imageThreshold, element.ImageThreshold);
                _imageDither.Checked = element.ImageDither;
                _imageKeepAspect.Checked = element.ImageKeepAspect;
            }
            _loading = false;
        }

        private void DeleteSelectedElement()
        {
            LabelElement selected = _canvas.SelectedElement;
            if (selected == null)
            {
                return;
            }
            _document.Elements.Remove(selected);
            _canvas.SelectedElement = null;
            RefreshElementList();
            LoadSelectedImage();
            _canvas.Invalidate();
        }

        private void LoadDocument(LabelDocument document)
        {
            document.Normalize();
            _document = document;
            _canvas.Document = document;

            _loading = true;
            SetNumeric(_labelWidth, document.WidthMm);
            SetNumeric(_labelHeight, document.HeightMm);
            SetNumeric(_gap, document.GapMm);
            int[] paperTypes = { 1, 2, 5 };
            _paperType.SelectedIndex = Math.Max(0, Array.IndexOf(paperTypes, document.PaperType));
            _direction.SelectedIndex = Math.Max(0, Math.Min(3, document.Direction));
            SetNumeric(_speed, document.Speed);
            _deepness.SelectedIndex = Math.Max(0, Math.Min(_deepness.Items.Count - 1, document.Deepness));
            SetNumeric(_copies, document.Copies);
            _oneByOne.Checked = document.OneByOne;
            _guideMode.SelectedIndex = Math.Max(0, Math.Min(3, (int)document.GuideMode));
            _printGuide.Checked = document.PrintGuide;
            SetNumeric(_guideThickness, document.GuideThicknessMm);
            _printBarcodesToggle.Checked = document.PrintBarcodes;
            _printBarcodesToggle.Text = document.PrintBarcodes ? "✓ 打印条码" : "✕ 不打印条码";
            _loading = false;

            RefreshElementList();
            _canvas.SelectedElement = document.Elements.FirstOrDefault();
            _printGuide.Enabled = document.GuideMode != CenterGuideMode.None;
            _guideThickness.Enabled = document.GuideMode != CenterGuideMode.None;
            LoadSelectedImage();
            _canvas.Invalidate();
        }

        private void LoadSelectedElement()
        {
            LabelElement element = _canvas.SelectedElement;
            _loading = true;
            bool hasElement = element != null;
            _elementKind.Text = !hasElement
                ? "未选择"
                : element.Kind == LabelElementKind.Text
                    ? "文字"
                    : element.IsImage
                        ? "单色图片"
                        : element.Kind == LabelElementKind.DataMatrix ? "Data Matrix 条码" : "PDF417 条码";
            foreach (Control control in new Control[]
            {
                _elementX, _elementY, _elementWidth, _elementHeight, _textContent, _fontFamily,
                _fontSize, _bold, _align, _pdfPrefix, _pdfUseTimestamp, _pdfPayload, _printDigits, _digitsText
            })
            {
                control.Enabled = hasElement;
            }

            if (hasElement)
            {
                SetNumeric(_elementX, element.X);
                SetNumeric(_elementY, element.Y);
                SetNumeric(_elementWidth, element.Width);
                SetNumeric(_elementHeight, element.Height);
                _textContent.Text = element.Text ?? string.Empty;
                SelectFont(element.FontFamily);
                SetNumeric(_fontSize, element.FontSizeMm);
                _bold.Checked = element.Bold;
                _align.SelectedIndex = Math.Max(0, Math.Min(2, element.Align));
                _pdfPrefix.Text = element.PdfPrefix ?? string.Empty;
                _pdfUseTimestamp.Checked = element.PdfUseTimestamp;
                _pdfPayload.Text = element.PdfPayload ?? string.Empty;
                _printDigits.Checked = element.PrintDigits;
                _digitsText.Text = element.DigitsText ?? string.Empty;

                bool text = element.Kind == LabelElementKind.Text;
                bool barcode = element.IsBarcode;
                _textContent.Enabled = text;
                _fontFamily.Enabled = text;
                _fontSize.Enabled = text;
                _bold.Enabled = text;
                _align.Enabled = text;
                _pdfPrefix.Enabled = barcode;
                _pdfUseTimestamp.Enabled = barcode;
                _pdfPayload.Enabled = barcode && !element.PdfUseTimestamp;
                _printDigits.Enabled = barcode;
                _digitsText.Enabled = barcode && element.PrintDigits;
            }
            else
            {
                _textContent.Text = string.Empty;
                _pdfPrefix.Text = string.Empty;
                _pdfPayload.Text = string.Empty;
                _printDigits.Checked = false;
                _digitsText.Text = string.Empty;
            }
            _loading = false;
            UpdateEncodedContent();
        }

        private void RefreshElementList()
        {
            LabelElement selected = _canvas == null ? null : _canvas.SelectedElement;
            _loading = true;
            _elementList.Items.Clear();
            foreach (LabelElement element in _document.Elements)
            {
                _elementList.Items.Add(element);
            }
            _elementList.SelectedItem = selected;
            _loading = false;
        }

        private void UpdateEncodedContent()
        {
            LabelElement element = _canvas == null ? null : _canvas.SelectedElement;
            if (element == null || !element.IsBarcode)
            {
                _pdfEncodedContent.Text = "—";
                _pdfEncodedContent.ForeColor = Color.DimGray;
                return;
            }
            DateTime timestamp = _previewTimestamp;
            string content = element.GetBarcodeContent(timestamp);
            string suffix = element.PrintDigits ? "  |  数位码：" + element.GetDigitsContent(timestamp) : string.Empty;
            bool prefixValid = Regex.IsMatch(element.PdfPrefix ?? string.Empty, "^[A-Za-z]{3}$");
            _pdfEncodedContent.Text = (_document.PrintBarcodes ? string.Empty : "[条码打印已关闭]  ") +
                                      content + suffix + (prefixValid ? string.Empty : "  （头部必须为 3 位英文字母）");
            _pdfEncodedContent.ForeColor = !prefixValid ? Color.Firebrick : _document.PrintBarcodes ? Color.Navy : Color.DimGray;
        }

        private string ValidateDocument()
        {
            if (_document.WidthMm > 50m)
            {
                return "标签宽度不能超过 50 mm。";
            }
            if (_document.Elements.Count == 0)
            {
                return "标签中没有任何内容。";
            }
            if (!_document.PrintBarcodes && _document.Elements.All(item => item.IsBarcode))
            {
                return "标签中只有条码，但“打印条码”已经关闭。";
            }
            foreach (LabelElement element in _document.Elements.Where(item => item.IsBarcode && _document.PrintBarcodes))
            {
                if (!Regex.IsMatch(element.PdfPrefix ?? string.Empty, "^[A-Za-z]{3}$"))
                {
                    return "每个条码的头部必须恰好是 3 位英文字母。";
                }
                if (!element.PdfUseTimestamp && string.IsNullOrWhiteSpace(element.PdfPayload))
                {
                    return "条码未使用自动时间时，自定义字符串不能为空。";
                }
                if (element.PrintDigits && string.IsNullOrWhiteSpace(element.GetDigitsContent(_previewTimestamp)))
                {
                    return element.DisplayName + " 已选择打印附加数位码，但没有可打印的数字。";
                }
            }
            foreach (LabelElement element in _document.Elements.Where(item => item.IsImage))
            {
                if (!ImageAssetService.IsValidImageData(element.ImageData))
                {
                    return element.DisplayName + " 的图片数据无效，请重新导入。";
                }
            }
            return null;
        }

        private void SaveTemplate()
        {
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "T50 标签模板 (*.t50label)|*.t50label|JSON 文件 (*.json)|*.json",
                DefaultExt = "t50label",
                AddExtension = true,
                FileName = "label.t50label"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LabelDocument));
                using (FileStream stream = File.Create(dialog.FileName))
                {
                    serializer.WriteObject(stream, _document);
                }
                _printState.Text = "模板已保存：" + dialog.FileName;
            }
        }

        private void OpenTemplate()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "T50 标签模板 (*.t50label;*.json)|*.t50label;*.json|所有文件 (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LabelDocument));
                    using (FileStream stream = File.OpenRead(dialog.FileName))
                    {
                        LabelDocument document = serializer.ReadObject(stream) as LabelDocument;
                        if (document == null)
                        {
                            throw new InvalidDataException("模板内容无效。");
                        }
                        LoadDocument(document);
                    }
                    _printState.Text = "模板已打开：" + dialog.FileName;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "无法打开模板", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportPreview()
        {
            string error = ValidateDocument();
            if (error != null)
            {
                MessageBox.Show(this, error, "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "PNG 图像 (*.png)|*.png",
                DefaultExt = "png",
                AddExtension = true,
                FileName = "label-preview.png"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                DateTime timestamp = _autoRefresh.Checked ? DateTime.Now : _previewTimestamp;
                using (Bitmap bitmap = LabelRenderer.RenderForPrinter(_document, timestamp))
                {
                    bitmap.Save(dialog.FileName, ImageFormat.Png);
                }
                _printState.Text = "打印预览已导出：" + dialog.FileName;
            }
        }

        private void ApplyStatus(PrinterStatusSnapshot status)
        {
            Color background;
            Color foreground = Color.Black;
            switch (status.State)
            {
                case DeviceState.Waiting:
                    background = Color.Honeydew;
                    foreground = Color.DarkGreen;
                    break;
                case DeviceState.Printting:
                case DeviceState.CheckDevice:
                    background = Color.LightCyan;
                    foreground = Color.Navy;
                    break;
                case DeviceState.Completed:
                    background = Color.Honeydew;
                    foreground = Color.DarkGreen;
                    break;
                default:
                    background = Color.MistyRose;
                    foreground = Color.Firebrick;
                    break;
            }
            SetDeviceState(status.StateText, background, BuildStatusDetail(status), foreground);
            SetSdkStatusText(
                "SDK 状态：" + status.StateText + " (" + (int)status.State + ")" +
                "  |  描述：" + StatusValue(status.Description) +
                "  |  错误：" + StatusValue(status.ErrorMessage) +
                "  |  页数：" + status.PrintedPages + "/" + status.TotalPages,
                IsFailureStatus(status) ? Color.Firebrick : Color.DimGray);

            if (IsCompletedStatus(status))
            {
                SetProgressComplete();
            }
            else if (_isPrinting && status.TotalPages > 0 && status.State == DeviceState.Printting)
            {
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Maximum = Math.Max(1, status.TotalPages);
                _progress.Value = Math.Max(0, Math.Min(_progress.Maximum, status.PrintedPages));
            }
            else if (!_isPrinting)
            {
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Maximum = 100;
                _progress.Value = 0;
            }
            if (_isPrinting)
            {
                _printState.Text = "应用状态：" + status.StateText + BuildStatusDetail(status);
            }
        }

        private static bool IsCompletedStatus(PrinterStatusSnapshot status)
        {
            if (status == null || IsFailureStatus(status))
            {
                return false;
            }
            return status.State == DeviceState.Completed ||
                   (status.TotalPages > 0 && status.PrintedPages >= status.TotalPages) ||
                   ContainsStatusWord(status.Description, "完成") ||
                   ContainsStatusWord(status.Description, "成功");
        }

        private static bool IsFailureStatus(PrinterStatusSnapshot status)
        {
            return status != null &&
                   (status.State == DeviceState.AbortPrint ||
                    status.State == DeviceState.ResetDevice ||
                    !string.IsNullOrWhiteSpace(status.ErrorMessage));
        }

        private static bool ContainsStatusWord(string value, string word)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetProgressComplete()
        {
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Maximum = 100;
            _progress.Value = 100;
        }

        private void SetSdkStatusText(string text, Color color)
        {
            if (_sdkStatusLine == null)
            {
                return;
            }
            _sdkStatusLine.Text = text;
            _sdkStatusLine.ForeColor = color;
        }

        private static string StatusValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : SingleLine(value);
        }

        private static string SingleLine(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string BuildStatusDetail(PrinterStatusSnapshot status)
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(status.Description))
            {
                builder.Append("  ").Append(status.Description.Trim());
            }
            if (status.TotalPages > 0)
            {
                builder.Append("  ").Append(status.PrintedPages).Append('/').Append(status.TotalPages).Append(" 页");
            }
            if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
            {
                builder.Append("  ").Append(status.ErrorMessage.Trim());
            }
            return builder.ToString();
        }

        private string GetSelectedDevice()
        {
            object selected = _devicePaths.SelectedItem;
            return selected == null ? string.Empty : selected.ToString();
        }

        private void SetDeviceState(string state, Color background, string detail, Color foreground)
        {
            _deviceState.Text = state;
            _deviceState.BackColor = background;
            _deviceState.ForeColor = foreground;
            _deviceDetail.Text = string.IsNullOrWhiteSpace(detail) ? "—" : detail;
            _deviceDetail.ForeColor = foreground;
        }

        private void SetDeviceUiBusy(bool busy, string text)
        {
            _scanButton.Enabled = !busy && !_isPrinting;
            _queryButton.Enabled = !busy && !_isPrinting;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _deviceDetail.Text = text;
                _deviceDetail.ForeColor = Color.DimGray;
            }
        }

        private void SetEditingEnabled(bool enabled)
        {
            _tabs.Enabled = enabled;
            _canvas.Enabled = enabled;
            _printButton.Enabled = enabled;
            _devicePaths.Enabled = enabled;
            _scanButton.Enabled = enabled;
            _queryButton.Enabled = enabled;
            _autoRefresh.Enabled = enabled;
        }

        private static Image LoadBrandImage()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dragon.png");
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (Image source = Image.FromStream(stream, true, true))
                {
                    return new Bitmap(source);
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void SelectFont(string familyName)
        {
            for (int index = 0; index < _fontFamily.Items.Count; index++)
            {
                FontOption option = _fontFamily.Items[index] as FontOption;
                if (option != null && string.Equals(option.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
                {
                    _fontFamily.SelectedIndex = index;
                    return;
                }
            }
            _fontFamily.SelectedIndex = _fontFamily.Items.Count > 0 ? 0 : -1;
        }

        private static TableLayoutPanel CreatePropertyTable()
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(2)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return table;
        }

        private static void AddPropertyRow(TableLayoutPanel table, string name, Control control, float height = 34f)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Label label = new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static void AddWideRow(TableLayoutPanel table, Control control, float height)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }

        private static NumericUpDown CreateNumeric(decimal minimum, decimal maximum, decimal value, decimal increment, int decimalPlaces)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimalPlaces,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Right
            };
        }

        private static ComboBox CreateCombo(params string[] items)
        {
            ComboBox combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
            return combo;
        }

        private static void SetNumeric(NumericUpDown control, decimal value)
        {
            control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
        }
    }
}
