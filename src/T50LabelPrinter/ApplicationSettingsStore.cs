using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace T50LabelPrinter
{
    [DataContract]
    public sealed class PaperDefaults
    {
        [DataMember(Order = 1)]
        public decimal WidthMm { get; set; }

        [DataMember(Order = 2)]
        public decimal HeightMm { get; set; }

        [DataMember(Order = 3)]
        public int GapMm { get; set; }

        [DataMember(Order = 4)]
        public int Direction { get; set; }

        public void Normalize()
        {
            WidthMm = Math.Max(5m, Math.Min(50m, WidthMm));
            HeightMm = Math.Max(5m, Math.Min(200m, HeightMm));
            GapMm = Math.Max(0, Math.Min(20, GapMm));
            Direction = Math.Max(0, Math.Min(3, Direction));
        }

        public void ApplyTo(LabelDocument document)
        {
            Normalize();
            document.WidthMm = WidthMm;
            document.HeightMm = HeightMm;
            document.GapMm = GapMm;
            document.Direction = Direction;
            foreach (LabelElement element in document.Elements)
            {
                document.ClampElement(element);
            }
        }
    }

    public static class ApplicationSettingsStore
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(PaperDefaults));

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "T50LabelPrinter",
                    "paper-defaults.json");
            }
        }

        public static bool TryLoad(out PaperDefaults defaults)
        {
            defaults = null;
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return false;
                }
                using (FileStream stream = File.OpenRead(SettingsPath))
                {
                    defaults = Serializer.ReadObject(stream) as PaperDefaults;
                }
                if (defaults == null)
                {
                    return false;
                }
                defaults.Normalize();
                return true;
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

        public static void Save(PaperDefaults defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException("defaults");
            }
            defaults.Normalize();
            string directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = SettingsPath + ".tmp";
            using (FileStream stream = File.Create(temporaryPath))
            {
                Serializer.WriteObject(stream, defaults);
            }
            if (File.Exists(SettingsPath))
            {
                File.Replace(temporaryPath, SettingsPath, null);
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
        }

        public static void Clear()
        {
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
        }
    }
}
