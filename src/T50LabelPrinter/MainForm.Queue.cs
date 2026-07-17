using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Supvan.T50PRO.SDK;

namespace T50LabelPrinter
{
    public sealed partial class MainForm
    {
        private TabPage _queueTab;
        private Button _queueImportButton;
        private Button _queueClearButton;
        private Button _queueResetButton;
        private Button _queueStartButton;
        private Button _queueStopButton;
        private Label _queueSourceLabel;
        private DataGridView _mappingGrid;
        private DataGridView _queueGrid;
        private PrintQueueData _queueData;
        private readonly Dictionary<int, int> _queueMappings = new Dictionary<int, int>();
        private LabelDocument _queuePreviewDocument;
        private bool _queueGridLoading;
        private bool _queueRunning;
        private bool _queueStopRequested;
        private int _queueCompletedCount;
        private int _queueTotalCount;
        private int _queueCurrentOrdinal;
        private bool _queueTaskSubmitted;

        private bool IsQueuePreviewActive
        {
            get
            {
                return _queueTab != null && _tabs != null && _tabs.SelectedTab == _queueTab &&
                       _queuePreviewDocument != null;
            }
        }

        private TabPage CreateQueueTab()
        {
            _queueTab = new TabPage("Excel 队列") { Padding = new Padding(7) };
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));

            FlowLayoutPanel importTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _queueImportButton = new Button { Text = "导入 Excel…", Width = 112, Height = 31 };
            _queueClearButton = new Button { Text = "清空", Width = 64, Height = 31 };
            _queueResetButton = new Button { Text = "重置状态", Width = 88, Height = 31 };
            importTools.Controls.Add(_queueImportButton);
            importTools.Controls.Add(_queueClearButton);
            importTools.Controls.Add(_queueResetButton);
            root.Controls.Add(importTools, 0, 0);

            _queueSourceLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "第一行作为列名；支持 .xlsx、.csv、.tsv。",
                ForeColor = Color.DimGray,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_queueSourceLabel, 0, 1);
            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "对象映射（选中对象右上角显示 ID）",
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);

            _mappingGrid = CreateQueueGrid();
            _mappingGrid.MultiSelect = false;
            _mappingGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ObjectId",
                HeaderText = "ID",
                Width = 42,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ObjectType",
                HeaderText = "对象",
                Width = 82,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _mappingGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "MappedColumn",
                HeaderText = "Excel 列",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FlatStyle = FlatStyle.System,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            });
            root.Controls.Add(_mappingGrid, 0, 3);

            _queueGrid = CreateQueueGrid();
            _queueGrid.MultiSelect = false;
            _queueGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _queueGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            root.Controls.Add(_queueGrid, 0, 4);

            FlowLayoutPanel queueActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 5, 0, 0)
            };
            _queueStartButton = new Button { Text = "开始队列打印", Width = 122, Height = 32 };
            _queueStopButton = new Button { Text = "完成当前项后停止", Width = 144, Height = 32, Enabled = false };
            queueActions.Controls.Add(_queueStartButton);
            queueActions.Controls.Add(_queueStopButton);
            root.Controls.Add(queueActions, 0, 5);

            _queueImportButton.Click += (sender, args) => ImportQueueFile();
            _queueClearButton.Click += (sender, args) => ClearQueue();
            _queueResetButton.Click += (sender, args) => ResetQueueStates();
            _queueStartButton.Click += async (sender, args) => await StartQueueAsync();
            _queueStopButton.Click += (sender, args) => RequestQueueStop();
            _mappingGrid.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (_mappingGrid.IsCurrentCellDirty)
                {
                    _mappingGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _mappingGrid.CellValueChanged += MappingGridCellValueChanged;
            _mappingGrid.SelectionChanged += (sender, args) => SelectCanvasObjectForMapping();
            _mappingGrid.DataError += (sender, args) => { args.ThrowException = false; };
            _queueGrid.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (_queueGrid.IsCurrentCellDirty)
                {
                    _queueGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _queueGrid.CellValueChanged += QueueGridCellValueChanged;
            _queueGrid.SelectionChanged += (sender, args) => PreviewSelectedQueueRow();
            _queueGrid.DataError += (sender, args) => { args.ThrowException = false; };

            _queueTab.Controls.Add(root);
            return _queueTab;
        }

        private static DataGridView CreateQueueGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 24 }
            };
        }

        private void ImportQueueFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "从 Excel 导入打印队列",
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV/TSV 文本 (*.csv;*.tsv)|*.csv;*.tsv|所有支持的文件 (*.xlsx;*.csv;*.tsv)|*.xlsx;*.csv;*.tsv",
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
                    PrintQueueData imported = SpreadsheetQueueImporter.Import(dialog.FileName);
                    _queueData = imported;
                    _queueMappings.Clear();
                    List<LabelElement> targets = _document.Elements.Where(element => !element.IsImage).OrderBy(element => element.ObjectId).ToList();
                    for (int index = 0; index < targets.Count && index < imported.Headers.Count; index++)
                    {
                        _queueMappings[targets[index].ObjectId] = index;
                    }
                    RefreshQueueMappings();
                    RefreshQueueGrid();
                    _queueSourceLabel.Text = Path.GetFileName(imported.SourcePath) + "  |  工作表：" + imported.SheetName +
                                             "  |  " + imported.Rows.Count + " 条";
                    _printState.Text = "应用状态：已导入 " + imported.Rows.Count + " 条队列数据；请检查对象映射。";
                    if (_queueGrid.Rows.Count > 0)
                    {
                        _queueGrid.ClearSelection();
                        _queueGrid.Rows[0].Selected = true;
                        _queueGrid.CurrentCell = _queueGrid.Rows[0].Cells[Math.Min(4, _queueGrid.Columns.Count - 1)];
                    }
                }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException ||
                    exception is InvalidDataException || exception is NotSupportedException ||
                    exception is ArgumentException)
                {
                    MessageBox.Show(this, exception.Message, "无法导入打印队列", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ClearQueue()
        {
            if (_queueRunning)
            {
                return;
            }
            _queueData = null;
            _queueMappings.Clear();
            _queuePreviewDocument = null;
            _queueSourceLabel.Text = "第一行作为列名；支持 .xlsx、.csv、.tsv。";
            RefreshQueueMappings();
            RefreshQueueGrid();
            RestoreTemplateCanvas();
            _printState.Text = "应用状态：打印队列已清空。";
        }

        private void ResetQueueStates()
        {
            if (_queueData == null || _queueRunning)
            {
                return;
            }
            foreach (PrintQueueRow row in _queueData.Rows)
            {
                row.State = PrintQueueItemState.Pending;
                row.Error = string.Empty;
            }
            RefreshQueueGridRows();
            _printState.Text = "应用状态：队列状态已重置。";
        }

        private void RefreshQueueMappings()
        {
            if (_mappingGrid == null || _document == null)
            {
                return;
            }
            _queueGridLoading = true;
            try
            {
                _mappingGrid.Rows.Clear();
                DataGridViewComboBoxColumn column = _mappingGrid.Columns["MappedColumn"] as DataGridViewComboBoxColumn;
                column.Items.Clear();
                column.Items.Add("（不映射）");
                if (_queueData != null)
                {
                    foreach (string header in _queueData.Headers)
                    {
                        column.Items.Add(header);
                    }
                }

                foreach (LabelElement element in _document.Elements.OrderBy(item => item.ObjectId))
                {
                    int mappedColumn;
                    string selected = "（不映射）";
                    if (!element.IsImage && _queueData != null && _queueMappings.TryGetValue(element.ObjectId, out mappedColumn) &&
                        mappedColumn >= 0 && mappedColumn < _queueData.Headers.Count)
                    {
                        selected = _queueData.Headers[mappedColumn];
                    }
                    int rowIndex = _mappingGrid.Rows.Add(element.ObjectId, GetElementTypeText(element), selected);
                    DataGridViewRow row = _mappingGrid.Rows[rowIndex];
                    row.Tag = element.ObjectId;
                    if (element.IsImage)
                    {
                        row.Cells["MappedColumn"].ReadOnly = true;
                        row.Cells["MappedColumn"].Style.ForeColor = Color.DimGray;
                        row.Cells["MappedColumn"].ToolTipText = "图片对象不支持单元格内容映射。";
                    }
                }
            }
            finally
            {
                _queueGridLoading = false;
            }
        }

        private static string GetElementTypeText(LabelElement element)
        {
            if (element.Kind == LabelElementKind.Text) return "文字";
            if (element.Kind == LabelElementKind.Pdf417) return "PDF417";
            if (element.Kind == LabelElementKind.DataMatrix) return "Data Matrix";
            return "图片";
        }

        private void RefreshQueueGrid()
        {
            if (_queueGrid == null)
            {
                return;
            }
            _queueGridLoading = true;
            try
            {
                _queueGrid.Columns.Clear();
                _queueGrid.Rows.Clear();
                _queueGrid.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = "Enabled",
                    HeaderText = "打印",
                    Width = 48,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                _queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Sequence",
                    HeaderText = "序号",
                    Width = 48,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                _queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "ExcelRow",
                    HeaderText = "Excel 行",
                    Width = 68,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                _queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Status",
                    HeaderText = "状态",
                    Width = 74,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
                if (_queueData != null)
                {
                    for (int index = 0; index < _queueData.Headers.Count; index++)
                    {
                        _queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            Name = "Value" + index,
                            HeaderText = _queueData.Headers[index],
                            Width = 120,
                            SortMode = DataGridViewColumnSortMode.NotSortable
                        });
                    }
                }
                RefreshQueueGridRows();
            }
            finally
            {
                _queueGridLoading = false;
            }
        }

        private void RefreshQueueGridRows()
        {
            if (_queueGrid == null)
            {
                return;
            }
            int selectedIndex = _queueGrid.CurrentRow == null ? -1 : _queueGrid.CurrentRow.Index;
            bool previousLoading = _queueGridLoading;
            _queueGridLoading = true;
            try
            {
                _queueGrid.Rows.Clear();
                if (_queueData == null)
                {
                    return;
                }
                for (int index = 0; index < _queueData.Rows.Count; index++)
                {
                    PrintQueueRow item = _queueData.Rows[index];
                    List<object> values = new List<object>
                    {
                        item.Enabled,
                        index + 1,
                        item.ExcelRowNumber,
                        item.StateText
                    };
                    values.AddRange(item.Values.Cast<object>());
                    int rowIndex = _queueGrid.Rows.Add(values.ToArray());
                    DataGridViewRow gridRow = _queueGrid.Rows[rowIndex];
                    gridRow.Tag = item;
                    gridRow.Cells["Status"].ToolTipText = item.Error ?? string.Empty;
                    ApplyQueueRowStyle(gridRow, item.State);
                }
                if (selectedIndex >= 0 && selectedIndex < _queueGrid.Rows.Count)
                {
                    _queueGrid.Rows[selectedIndex].Selected = true;
                    _queueGrid.CurrentCell = _queueGrid.Rows[selectedIndex].Cells[Math.Min(4, _queueGrid.Columns.Count - 1)];
                }
            }
            finally
            {
                _queueGridLoading = previousLoading;
            }
        }

        private static void ApplyQueueRowStyle(DataGridViewRow row, PrintQueueItemState state)
        {
            row.DefaultCellStyle.BackColor = state == PrintQueueItemState.Completed
                ? Color.Honeydew
                : state == PrintQueueItemState.Failed
                    ? Color.MistyRose
                    : state == PrintQueueItemState.Printing ? Color.LightCyan : SystemColors.Window;
            row.DefaultCellStyle.ForeColor = state == PrintQueueItemState.Failed ? Color.Firebrick : SystemColors.ControlText;
        }

        private void MappingGridCellValueChanged(object sender, DataGridViewCellEventArgs args)
        {
            if (_queueGridLoading || args.RowIndex < 0 || args.ColumnIndex != _mappingGrid.Columns["MappedColumn"].Index)
            {
                return;
            }
            DataGridViewRow row = _mappingGrid.Rows[args.RowIndex];
            int objectId = (int)row.Tag;
            LabelElement element = _document.Elements.FirstOrDefault(item => item.ObjectId == objectId);
            if (element == null || element.IsImage)
            {
                return;
            }
            string header = Convert.ToString(row.Cells[args.ColumnIndex].Value);
            int columnIndex = _queueData == null ? -1 : _queueData.Headers.FindIndex(value => string.Equals(value, header, StringComparison.Ordinal));
            if (columnIndex < 0)
            {
                _queueMappings.Remove(objectId);
            }
            else
            {
                _queueMappings[objectId] = columnIndex;
            }
            PreviewSelectedQueueRow();
        }

        private void QueueGridCellValueChanged(object sender, DataGridViewCellEventArgs args)
        {
            if (_queueGridLoading || _queueData == null || args.RowIndex < 0 || args.RowIndex >= _queueData.Rows.Count)
            {
                return;
            }
            PrintQueueRow row = _queueData.Rows[args.RowIndex];
            if (args.ColumnIndex == 0)
            {
                row.Enabled = Convert.ToBoolean(_queueGrid.Rows[args.RowIndex].Cells[args.ColumnIndex].Value ?? false);
            }
            else if (args.ColumnIndex >= 4)
            {
                int valueIndex = args.ColumnIndex - 4;
                if (valueIndex >= 0 && valueIndex < row.Values.Count)
                {
                    row.Values[valueIndex] = Convert.ToString(_queueGrid.Rows[args.RowIndex].Cells[args.ColumnIndex].Value) ?? string.Empty;
                    row.State = PrintQueueItemState.Pending;
                    row.Error = string.Empty;
                    _queueGrid.Rows[args.RowIndex].Cells["Status"].Value = row.StateText;
                    ApplyQueueRowStyle(_queueGrid.Rows[args.RowIndex], row.State);
                }
            }
            PreviewSelectedQueueRow();
        }

        private void QueueTabSelectionChanged(object sender, EventArgs args)
        {
            if (_tabs.SelectedTab == _queueTab)
            {
                PreviewSelectedQueueRow();
            }
            else if (!_queueRunning)
            {
                RestoreTemplateCanvas();
            }
        }

        private void PreviewSelectedQueueRow()
        {
            if (_queueGridLoading || _tabs.SelectedTab != _queueTab || _queueData == null || _queueGrid.CurrentRow == null)
            {
                return;
            }
            int index = _queueGrid.CurrentRow.Index;
            if (index < 0 || index >= _queueData.Rows.Count)
            {
                return;
            }
            int selectedObjectId = _canvas.SelectedElement == null ? -1 : _canvas.SelectedElement.ObjectId;
            _queuePreviewDocument = QueueDocumentMapper.Apply(_document, _queueData.Rows[index], GetQueueMappings());
            _canvas.ReadOnly = true;
            _canvas.Document = _queuePreviewDocument;
            _canvas.SelectedElement = _queuePreviewDocument.Elements.FirstOrDefault(item => item.ObjectId == selectedObjectId) ??
                                      _queuePreviewDocument.Elements.FirstOrDefault();
            _canvas.Invalidate();
        }

        private IEnumerable<QueueObjectMapping> GetQueueMappings()
        {
            return _queueMappings.Select(item => new QueueObjectMapping
            {
                ObjectId = item.Key,
                ColumnIndex = item.Value
            }).ToList();
        }

        private void RestoreTemplateCanvas()
        {
            if (_canvas == null || _document == null)
            {
                return;
            }
            int selectedId = _canvas.SelectedElement == null ? -1 : _canvas.SelectedElement.ObjectId;
            _canvas.ReadOnly = false;
            _canvas.Document = _document;
            _canvas.SelectedElement = _document.Elements.FirstOrDefault(item => item.ObjectId == selectedId) ??
                                      _document.Elements.FirstOrDefault();
            _canvas.Invalidate();
        }

        private void SelectCanvasObjectForMapping()
        {
            if (!IsQueuePreviewActive || _mappingGrid.CurrentRow == null)
            {
                return;
            }
            int objectId = (int)_mappingGrid.CurrentRow.Tag;
            _canvas.SelectedElement = _queuePreviewDocument.Elements.FirstOrDefault(item => item.ObjectId == objectId);
        }

        private void SelectMappingForCanvasObject()
        {
            if (_canvas.SelectedElement == null || _mappingGrid == null)
            {
                return;
            }
            foreach (DataGridViewRow row in _mappingGrid.Rows)
            {
                if ((int)row.Tag == _canvas.SelectedElement.ObjectId)
                {
                    row.Selected = true;
                    _mappingGrid.CurrentCell = row.Cells["MappedColumn"];
                    break;
                }
            }
        }

        private async Task StartQueueAsync()
        {
            if (_queueRunning || _queueData == null)
            {
                return;
            }
            _queueGrid.EndEdit();
            _mappingGrid.EndEdit();
            List<PrintQueueRow> jobs = _queueData.Rows
                .Where(row => row.Enabled && row.State != PrintQueueItemState.Completed)
                .ToList();
            if (jobs.Count == 0)
            {
                MessageBox.Show(this, "没有勾选的待打印队列项。已完成的项目可先单击“重置状态”再重新打印。",
                    "打印队列", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string path = GetSelectedDevice();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "未选择 USB 打印机。请先刷新设备。", "不能打印队列", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PrinterStatusSnapshot ready = await QueryStatusAsync(true);
            if (ready == null || ready.State != DeviceState.Waiting)
            {
                MessageBox.Show(this, "打印机必须处于“就绪”状态才能开始队列。", "打印机未就绪", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LabelDocument queueTemplate = _document.DeepClone();
            List<QueueObjectMapping> mappings = GetQueueMappings().ToList();
            _queueRunning = true;
            _isPrinting = true;
            _queueStopRequested = false;
            _queueCompletedCount = 0;
            _queueTotalCount = jobs.Count;
            _queueCurrentOrdinal = 0;
            _timer.Stop();
            SetQueueRunningUi(true);
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            _progress.Value = 0;

            try
            {
                for (int index = 0; index < jobs.Count; index++)
                {
                    if (_queueStopRequested)
                    {
                        break;
                    }
                    PrintQueueRow row = jobs[index];
                    _queueCurrentOrdinal = index + 1;
                    row.State = PrintQueueItemState.Printing;
                    row.Error = string.Empty;
                    RefreshQueueGridRows();
                    _printState.Text = "队列状态：正在打印 " + _queueCurrentOrdinal + "/" + _queueTotalCount +
                                       "（Excel 第 " + row.ExcelRowNumber + " 行）";

                    DateTime timestamp = _autoRefresh.Checked ? DateTime.Now : _previewTimestamp;
                    LabelDocument document = QueueDocumentMapper.Apply(queueTemplate, row, mappings);
                    string validationError = ValidateDocument(document, timestamp);
                    if (validationError != null)
                    {
                        throw new InvalidDataException(validationError);
                    }

                    _queueTaskSubmitted = false;
                    PrinterStatusSnapshot baseline = await WaitUntilQueueReadyAsync(path);
                    string bitmapPath = null;
                    try
                    {
                        string directory = Path.Combine(Path.GetTempPath(), "T50LabelPrinter", "queue");
                        Directory.CreateDirectory(directory);
                        bitmapPath = Path.Combine(directory, "queue-" + Guid.NewGuid().ToString("N") + ".bmp");
                        using (Bitmap bitmap = LabelRenderer.RenderForPrinter(document, timestamp))
                        {
                            bitmap.Save(bitmapPath, ImageFormat.Bmp);
                        }
                        bool accepted = await Task.Run(() => _printer.Print(document, bitmapPath, path));
                        _queueTaskSubmitted = true;
                        await WaitForQueueCompletionAsync(path, document, baseline, accepted);
                    }
                    finally
                    {
                        if (!string.IsNullOrWhiteSpace(bitmapPath))
                        {
                            try { File.Delete(bitmapPath); }
                            catch (IOException) { }
                            catch (UnauthorizedAccessException) { }
                        }
                    }

                    row.State = PrintQueueItemState.Completed;
                    _queueTaskSubmitted = false;
                    _queueCompletedCount++;
                    UpdateQueueProgress(null);
                    RefreshQueueGridRows();
                    await Task.Delay(200);
                }

                if (_queueStopRequested)
                {
                    _printState.Text = "队列状态：已停止；已完成 " + _queueCompletedCount + "/" + _queueTotalCount + "，未打印项已保留。";
                }
                else
                {
                    _progress.Value = 100;
                    _printState.Text = "队列状态：打印完成 " + _queueCompletedCount + "/" + _queueTotalCount + "。";
                }
            }
            catch (Exception exception)
            {
                PrintQueueRow failed = jobs.FirstOrDefault(row => row.State == PrintQueueItemState.Printing);
                if (failed != null)
                {
                    failed.State = PrintQueueItemState.Failed;
                    failed.Error = exception.Message;
                }
                RefreshQueueGridRows();
                _printState.Text = "队列状态：已在第 " + _queueCurrentOrdinal + " 项停止 — " + exception.Message;
                MessageBox.Show(this, _printState.Text + "\r\n未发送的任务仍保留在队列中。",
                    "队列打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _queueRunning = false;
                _isPrinting = false;
                _queueStopRequested = false;
                _queueTaskSubmitted = false;
                SetQueueRunningUi(false);
                _timer.Start();
            }
        }

        private async Task<PrinterStatusSnapshot> WaitUntilQueueReadyAsync(string path)
        {
            PrinterStatusSnapshot last = null;
            int stableCompletedCount = 0;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                last = await QueryStatusAsync(false);
                if (last != null && last.State == DeviceState.Waiting && !IsFailureStatus(last))
                {
                    return last;
                }
                if (last != null && last.State == DeviceState.Completed && !IsFailureStatus(last))
                {
                    stableCompletedCount++;
                    if (stableCompletedCount >= 3)
                    {
                        return last;
                    }
                }
                else
                {
                    stableCompletedCount = 0;
                }
                if (IsFailureStatus(last))
                {
                    throw new InvalidOperationException("打印机未就绪：" + last.StateText + BuildStatusDetail(last));
                }
                await Task.Delay(300);
            }
            throw new TimeoutException("等待打印机回到就绪状态超时" + (last == null ? "。" : "：" + last.StateText + BuildStatusDetail(last)));
        }

        private async Task WaitForQueueCompletionAsync(
            string path,
            LabelDocument document,
            PrinterStatusSnapshot baseline,
            bool accepted)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool observedActivity = false;
            int quietWaitingCount = 0;
            int minimumQuietMilliseconds = Math.Max(
                2500,
                (int)Math.Ceiling((double)document.HeightMm / Math.Max(1, document.Speed) * 1000d * document.Copies) + 1800);
            int quietCompletionMilliseconds = accepted
                ? minimumQuietMilliseconds
                : Math.Max(5000, minimumQuietMilliseconds + 2500);

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
            {
                await Task.Delay(300);
                PrinterStatusSnapshot status = await QueryStatusAsync(false);
                if (status == null)
                {
                    continue;
                }
                if (IsFailureStatus(status))
                {
                    throw new InvalidOperationException("打印机报告失败：" + status.StateText + BuildStatusDetail(status));
                }

                observedActivity = observedActivity || status.State == DeviceState.Printting || status.State == DeviceState.CheckDevice ||
                    status.State != baseline.State || status.PrintedPages != baseline.PrintedPages || status.TotalPages != baseline.TotalPages;
                if (IsCompletedStatus(status) && (observedActivity || stopwatch.ElapsedMilliseconds >= quietCompletionMilliseconds))
                {
                    return;
                }

                if (status.State == DeviceState.Waiting)
                {
                    quietWaitingCount++;
                    if (quietWaitingCount >= 3 && stopwatch.ElapsedMilliseconds >= quietCompletionMilliseconds)
                    {
                        // 某些 SDK 即使已出纸也会让 DoPrint 返回 false，并只报告 Waiting。
                        return;
                    }
                }
                else
                {
                    quietWaitingCount = 0;
                }

                if (!accepted && stopwatch.Elapsed > TimeSpan.FromSeconds(15) && !observedActivity)
                {
                    throw new InvalidOperationException("SDK 未确认任务，且打印状态在 15 秒内没有变化。为避免连续发送导致卡死，队列已停止。");
                }
            }
            throw new TimeoutException("等待当前标签打印完成超时，队列已停止。请检查耗材和 SDK 状态栏。");
        }

        private void RequestQueueStop()
        {
            if (!_queueRunning)
            {
                return;
            }
            _queueStopRequested = true;
            _queueStopButton.Enabled = false;
            _printButton.Enabled = false;
            _printState.Text = "队列状态：已请求停止；当前标签完成后不会再发送下一项。";
        }

        private void SetQueueRunningUi(bool running)
        {
            _tabs.Enabled = !running;
            _queueImportButton.Enabled = !running;
            _queueClearButton.Enabled = !running;
            _queueResetButton.Enabled = !running;
            _queueStartButton.Enabled = !running;
            _queueStopButton.Enabled = running;
            _mappingGrid.Enabled = !running;
            _queueGrid.Enabled = !running;
            _canvas.Enabled = !running;
            _devicePaths.Enabled = !running;
            _scanButton.Enabled = !running;
            _queryButton.Enabled = !running;
            _autoRefresh.Enabled = !running;
            _printButton.Text = running ? "停止队列" : "打印标签";
            _printButton.Enabled = true;
        }

        private void UpdateQueueProgress(PrinterStatusSnapshot status)
        {
            if (!_queueRunning || _queueTotalCount <= 0)
            {
                return;
            }
            double currentFraction = 0d;
            if (_queueTaskSubmitted && status != null)
            {
                if (status.TotalPages > 0)
                {
                    currentFraction = Math.Max(0d, Math.Min(1d, (double)status.PrintedPages / status.TotalPages));
                }
                else if (status.State == DeviceState.CheckDevice)
                {
                    currentFraction = 0.1d;
                }
                else if (status.State == DeviceState.Printting)
                {
                    currentFraction = 0.5d;
                }
                else if (IsCompletedStatus(status))
                {
                    currentFraction = 1d;
                }
            }
            int percent = (int)Math.Floor((_queueCompletedCount + currentFraction) * 100d / _queueTotalCount);
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Maximum = 100;
            _progress.Value = Math.Max(0, Math.Min(100, percent));
            if (_queueCurrentOrdinal > 0 && !_queueStopRequested)
            {
                _printState.Text = "队列状态：正在打印 " + _queueCurrentOrdinal + "/" + _queueTotalCount +
                                   "  |  总进度 " + _progress.Value + "%";
            }
        }
    }
}
