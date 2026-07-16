using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;

namespace T50LabelPrinter
{
    public sealed class FontOption
    {
        public FontOption(string displayName, string familyName)
        {
            DisplayName = displayName;
            FamilyName = familyName;
        }

        public string DisplayName { get; private set; }
        public string FamilyName { get; private set; }
        public override string ToString() { return DisplayName; }
    }

    public static class FontCatalog
    {
        public const string DefaultSansFamily = "Noto Sans SC";
        public const string DefaultSerifFamily = "Noto Serif SC";

        private static readonly HashSet<string> Installed = new HashSet<string>(
            new InstalledFontCollection().Families.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        public static IList<FontOption> GetOptions()
        {
            List<FontOption> options = new List<FontOption>();
            string sans = ResolveFamily(DefaultSansFamily, "Source Han Sans SC", "Source Han Sans CN", "思源黑体", "Source Han Sans JP", "Microsoft YaHei UI");
            string serif = ResolveFamily(DefaultSerifFamily, "Source Han Serif SC", "Source Han Serif CN", "思源宋体", "SimSun");
            options.Add(new FontOption("思源黑体 / " + sans, sans));
            options.Add(new FontOption("思源宋体 / " + serif, serif));

            foreach (string family in Installed.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (string.Equals(family, sans, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(family, serif, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                options.Add(new FontOption(family, family));
            }
            return options;
        }

        public static string ResolveFamily(string requested)
        {
            return ResolveFamily(requested, DefaultSansFamily, "Source Han Sans SC", "Source Han Sans JP", "Microsoft YaHei UI", FontFamily.GenericSansSerif.Name);
        }

        private static string ResolveFamily(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && Installed.Contains(candidate))
                {
                    return candidate;
                }
            }
            return FontFamily.GenericSansSerif.Name;
        }

        public static bool HasPreferredFonts
        {
            get { return Installed.Contains(DefaultSansFamily) && Installed.Contains(DefaultSerifFamily); }
        }
    }
}
