using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace T50LabelPrinter
{
    [DataContract]
    public sealed class EasyUpdateRelease
    {
        [DataMember(Name = "package_name")] public string PackageName { get; set; }
        [DataMember(Name = "update_available")] public bool UpdateAvailable { get; set; }
        [DataMember(Name = "version_name")] public string VersionName { get; set; }
        [DataMember(Name = "version_code")] public int VersionCode { get; set; }
        [DataMember(Name = "latest_version_name")] public string LatestVersionName { get; set; }
        [DataMember(Name = "latest_version_code")] public int LatestVersionCode { get; set; }
        [DataMember(Name = "mandatory")] public bool Mandatory { get; set; }
        [DataMember(Name = "release_notes")] public string ReleaseNotes { get; set; }
        [DataMember(Name = "download_url")] public string DownloadUrl { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
        [DataMember(Name = "artifact_type")] public string ArtifactType { get; set; }
    }

    [DataContract]
    public sealed class EasyUpdateSettings
    {
        public const string DefaultPackage = "com.zlight.t50labelprinter";
        [DataMember] public string Server { get; set; } = "http://192.168.95.55:19910";
        [DataMember] public string PackageName { get; set; } = DefaultPackage;
        private static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "T50LabelPrinter", "easyupdate.json"); } }
        public static EasyUpdateSettings Load()
        {
            try
            {
                using (FileStream stream = File.OpenRead(SettingsPath))
                    return (EasyUpdateSettings)new DataContractJsonSerializer(typeof(EasyUpdateSettings)).ReadObject(stream);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException) { return new EasyUpdateSettings(); }
        }
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            using (FileStream stream = File.Create(SettingsPath))
                new DataContractJsonSerializer(typeof(EasyUpdateSettings)).WriteObject(stream, this);
        }
    }

    public sealed class EasyUpdateClient : IDisposable
    {
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        public static string CurrentVersion { get { return typeof(EasyUpdateClient).Assembly.GetName().Version.ToString(3); } }
        public static int CurrentVersionCode
        {
            get { Version version = typeof(EasyUpdateClient).Assembly.GetName().Version; return version.Major * 10000 + version.Minor * 100 + version.Build; }
        }
        public static Uri GetServerUri(string server)
        {
            Uri uri;
            if (!Uri.TryCreate((server ?? string.Empty).Trim().TrimEnd('/') + "/", UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("请输入有效的 HTTP 或 HTTPS 服务地址。");
            return uri;
        }
        public async Task<EasyUpdateRelease> CheckAsync(string server, string package, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(package)) throw new InvalidOperationException("应用标识不能为空。");
            Uri address = new Uri(GetServerUri(server), "api/v1/apps/" + Uri.EscapeDataString(package.Trim()) + "/latest?version_code=" + CurrentVersionCode);
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using (HttpResponseMessage response = await _http.GetAsync(address, timeout.Token).ConfigureAwait(false))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        throw new InvalidOperationException("服务中尚无此应用的已发布版本，请先在 EasyUpdate 后台发布 ZIP。");
                    response.EnsureSuccessStatusCode();
                    using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        EasyUpdateRelease release = (EasyUpdateRelease)new DataContractJsonSerializer(typeof(EasyUpdateRelease)).ReadObject(stream);
                        if (release == null || !string.Equals(release.PackageName, package.Trim(), StringComparison.Ordinal))
                            throw new InvalidDataException("服务返回的应用标识不匹配。");
                        if (release.UpdateAvailable)
                        {
                            if (release.VersionCode <= CurrentVersionCode) throw new InvalidDataException("服务返回的版本号不是更新版本。");
                            if (!string.Equals(release.ArtifactType, "zip", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("此版本不是 ZIP 更新包，请在服务端上传并发布 ZIP。");
                            ValidateRelease(release);
                        }
                        return release;
                    }
                }
            }
        }
        private static void ValidateRelease(EasyUpdateRelease release)
        {
            Uri address;
            if (release == null || release.Size <= 0 || release.Size > 512L * 1024 * 1024 ||
                release.Sha256 == null || !System.Text.RegularExpressions.Regex.IsMatch(release.Sha256, "\\A[0-9a-fA-F]{64}\\z") ||
                !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out address) ||
                (address.Scheme != "http" && address.Scheme != "https") || !string.IsNullOrEmpty(address.UserInfo))
                throw new InvalidDataException("更新信息缺少有效的地址、大小或 SHA256。");
        }
        public async Task DownloadAsync(EasyUpdateRelease release, string destination, IProgress<int> progress, CancellationToken token)
        {
            ValidateRelease(release);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (HttpResponseMessage response = await _http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != release.Size)
                        throw new InvalidDataException("更新包大小与版本信息不一致。");
                    using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                        {
                            total += read;
                            if (total > release.Size) throw new InvalidDataException("更新包超出声明的大小。");
                            await target.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                            if (progress != null) progress.Report((int)(total * 100 / release.Size));
                        }
                        if (total != release.Size) throw new InvalidDataException("更新包下载不完整，请重试。");
                    }
                }
                token.ThrowIfCancellationRequested();
                using (SHA256 sha = SHA256.Create())
                using (FileStream file = File.OpenRead(temporary))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                    if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("更新包 SHA256 校验失败，请重试。");
                }
                using (ZipArchive zip = ZipFile.OpenRead(temporary))
                {
                    if (zip.Entries.Count == 0) throw new InvalidDataException("更新 ZIP 为空。");
                    ZipArchiveEntry manifest = zip.GetEntry("easyupdate.json");
                    if (manifest == null || manifest.Length > 4096) throw new InvalidDataException("更新 ZIP 缺少 easyupdate.json 版本信息。");
                    using (Stream stream = manifest.Open())
                    {
                        EasyUpdateRelease metadata = (EasyUpdateRelease)new DataContractJsonSerializer(typeof(EasyUpdateRelease)).ReadObject(stream);
                        if (metadata.PackageName != release.PackageName || metadata.VersionCode != release.VersionCode || metadata.VersionName != release.VersionName)
                            throw new InvalidDataException("ZIP 内版本信息与服务返回的信息不匹配。");
                    }
                    if (zip.GetEntry("T50LabelPrinter.exe") == null) throw new InvalidDataException("ZIP 缺少 T50LabelPrinter.exe。");
                }
                token.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public void Dispose() { _http.Dispose(); }
    }
}
