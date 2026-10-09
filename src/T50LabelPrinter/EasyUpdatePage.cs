using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    public sealed class EasyUpdatePage : UserControl
    {
        private readonly EasyUpdateClient _client = new EasyUpdateClient();
        private readonly TextBox _server = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _package = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _notes = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ProgressBar _progress = new ProgressBar { Dock = DockStyle.Fill };
        private readonly Button _check = new Button { Text = "检查更新", AutoSize = true };
        private readonly Button _download = new Button { Text = "下载新版 ZIP…", AutoSize = true, Enabled = false };
        private readonly Button _cancel = new Button { Text = "取消", AutoSize = true, Enabled = false };
        private readonly Button _folder = new Button { Text = "打开下载位置", AutoSize = true, Enabled = false };
        private EasyUpdateRelease _release;
        private CancellationTokenSource _operation;
        private string _downloadedFile;
        public EasyUpdatePage()
        {
            Padding = new Padding(20);
            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; i++) table.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.Absolute, i == 4 ? 100 : i == 7 ? 64 : 40));
            table.Controls.Add(new Label { Text = "当前版本", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            table.Controls.Add(new Label { Text = EasyUpdateClient.CurrentVersion, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
            table.Controls.Add(new Label { Text = "EasyUpdate 地址", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1); table.Controls.Add(_server, 1, 1);
            table.Controls.Add(new Label { Text = "应用标识", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2); table.Controls.Add(_package, 1, 2);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            buttons.Controls.AddRange(new Control[] { _check, _download, _cancel, _folder });
            table.Controls.Add(buttons, 0, 3); table.SetColumnSpan(buttons, 2);
            table.Controls.Add(_notes, 0, 4); table.SetColumnSpan(_notes, 2);
            table.Controls.Add(_progress, 0, 5); table.SetColumnSpan(_progress, 2);
            table.Controls.Add(_status, 0, 6); table.SetColumnSpan(_status, 2);
            Label hint = new Label { Text = "下载后会校验文件大小、SHA256 和 ZIP 内版本信息。关闭软件后，将新版 ZIP 解压到新目录，再运行其中的程序。模板和用户预设可继续使用。", Dock = DockStyle.Fill, AutoSize = false };
            table.Controls.Add(hint, 0, 7); table.SetColumnSpan(hint, 2); Controls.Add(table);
            EasyUpdateSettings settings = EasyUpdateSettings.Load(); _server.Text = settings.Server; _package.Text = settings.PackageName;
            _status.Text = "点击“检查更新”连接 EasyUpdate 服务。";
            _check.Click += async (sender, args) => await CheckAsync();
            _download.Click += async (sender, args) => await DownloadAsync();
            _cancel.Click += (sender, args) => { if (_operation != null) _operation.Cancel(); };
            _folder.Click += (sender, args) => { if (File.Exists(_downloadedFile)) Process.Start("explorer.exe", "/select,\"" + _downloadedFile + "\""); };
            _server.TextChanged += ResetRelease; _package.TextChanged += ResetRelease;
        }
        private void ResetRelease(object sender, EventArgs args) { _release = null; _download.Enabled = false; _notes.Clear(); }
        private void SetBusy(bool busy)
        {
            _server.Enabled = _package.Enabled = _check.Enabled = !busy;
            _download.Enabled = !busy && _release != null && _release.UpdateAvailable;
            _cancel.Enabled = busy;
        }
        private async Task CheckAsync()
        {
            if (_operation != null) return;
            _operation = new CancellationTokenSource(); SetBusy(true); _release = null; _notes.Clear(); _progress.Value = 0;
            _status.Text = "正在检查更新…";
            try
            {
                EasyUpdateSettings settings = new EasyUpdateSettings { Server = _server.Text.Trim(), PackageName = _package.Text.Trim() };
                EasyUpdateClient.GetServerUri(settings.Server); settings.Save();
                _release = await _client.CheckAsync(settings.Server, settings.PackageName, _operation.Token);
                if (IsDisposed) return;
                _status.Text = _release.UpdateAvailable ? "可下载新版本 " + _release.VersionName + (_release.Mandatory ? "（建议立即更新）" : "") : "已是最新版本（服务端：" + _release.LatestVersionName + "）。";
                _notes.Text = _release.UpdateAvailable ? "版本 " + _release.VersionName + " · " + (_release.Size / 1048576d).ToString("0.00") + " MB\r\n\r\n" + _release.ReleaseNotes : "暂无新版本。";
            }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "检查已取消或连接超时。"; }
            catch (Exception exception) { if (!IsDisposed) _status.Text = "检查失败：" + exception.Message; }
            finally { _operation.Dispose(); _operation = null; if (!IsDisposed) SetBusy(false); }
        }
        private async Task DownloadAsync()
        {
            if (_operation != null || _release == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "ZIP 更新包 (*.zip)|*.zip", DefaultExt = "zip", AddExtension = true,
                FileName = "T50LabelPrinter-v" + string.Join("_", (_release.VersionName ?? "latest").Split(Path.GetInvalidFileNameChars())) + ".zip" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _operation = new CancellationTokenSource(); SetBusy(true); _progress.Value = 0; _folder.Enabled = false;
                _status.Text = "正在下载并校验更新包…";
                try
                {
                    await _client.DownloadAsync(_release, dialog.FileName, new Progress<int>(value => { if (!IsDisposed) _progress.Value = value; }), _operation.Token);
                    if (IsDisposed) return;
                    _downloadedFile = dialog.FileName; _folder.Enabled = true; _status.Text = "下载完成，大小、SHA256 和 ZIP 版本信息校验通过。";
                }
                catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "下载已取消。"; }
                catch (Exception exception) { if (!IsDisposed) _status.Text = "下载失败：" + exception.Message; }
                finally { _operation.Dispose(); _operation = null; if (!IsDisposed) SetBusy(false); }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (_operation != null) _operation.Cancel(); _client.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
