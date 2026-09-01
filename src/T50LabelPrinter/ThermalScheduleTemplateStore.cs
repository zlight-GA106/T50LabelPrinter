using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace T50LabelPrinter
{
    public sealed class ThermalScheduleTemplateStore
    {
        public const string FileExtension = "t58schedule";
        private const string FormatName = "T50LabelPrinter.ThermalSchedule";
        private const int CurrentVersion = 1;
        private const long MaximumTemplateBytes = 4L * 1024L * 1024L;

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(ThermalScheduleTemplateEnvelope));

        public static string DefaultTemplatePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "T50LabelPrinter",
                    "default-schedule." + FileExtension);
            }
        }

        public static bool IsSupportedFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }
            try
            {
                return string.Equals(
                    Path.GetExtension(fileName),
                    "." + FileExtension,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public void Save(string fileName, ThermalScheduleDocument document)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("模板文件路径不能为空。", "fileName");
            }
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            ThermalScheduleTemplateEnvelope envelope = new ThermalScheduleTemplateEnvelope
            {
                Format = FormatName,
                Version = CurrentVersion,
                Document = document.DeepClone()
            };
            string directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            using (FileStream stream = new FileStream(
                fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Serializer.WriteObject(stream, envelope);
                stream.Flush(true);
            }
        }

        public ThermalScheduleDocument Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("模板文件路径不能为空。", "fileName");
            }
            FileInfo file = new FileInfo(fileName);
            if (!file.Exists)
            {
                throw new FileNotFoundException("找不到日程模板文件。", fileName);
            }
            if (file.Length <= 0 || file.Length > MaximumTemplateBytes)
            {
                throw new InvalidDataException("日程模板为空或超过 4 MB 限制。");
            }

            ThermalScheduleTemplateEnvelope envelope;
            using (FileStream stream = new FileStream(
                fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                envelope = Serializer.ReadObject(stream) as ThermalScheduleTemplateEnvelope;
            }
            if (envelope == null ||
                !string.Equals(envelope.Format, FormatName, StringComparison.Ordinal) ||
                envelope.Version <= 0 || envelope.Version > CurrentVersion ||
                envelope.Document == null)
            {
                throw new InvalidDataException("这不是受支持的 58mm 日程模板。");
            }

            ThermalScheduleDocument document = envelope.Document.DeepClone();
            document.Normalize();
            return document;
        }

        public void SaveDefault(ThermalScheduleDocument document)
        {
            Save(DefaultTemplatePath, document);
        }

        public bool TryLoadDefault(out ThermalScheduleDocument document)
        {
            document = null;
            try
            {
                if (!File.Exists(DefaultTemplatePath))
                {
                    return false;
                }
                document = Load(DefaultTemplatePath);
                return document != null;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (SerializationException)
            {
                return false;
            }
        }

        [DataContract]
        private sealed class ThermalScheduleTemplateEnvelope
        {
            [DataMember(Order = 1)]
            public string Format { get; set; }

            [DataMember(Order = 2)]
            public int Version { get; set; }

            [DataMember(Order = 3)]
            public ThermalScheduleDocument Document { get; set; }
        }
    }
}
