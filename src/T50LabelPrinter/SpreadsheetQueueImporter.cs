using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace T50LabelPrinter
{
    public static class SpreadsheetQueueImporter
    {
        public static PrintQueueData Import(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("未指定 Excel 文件。", "path");
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".xlsx")
            {
                return ImportXlsx(path);
            }
            if (extension == ".csv" || extension == ".tsv")
            {
                return ImportDelimited(path, extension == ".tsv" ? '\t' : ',');
            }
            if (extension == ".xls")
            {
                throw new NotSupportedException("旧版 .xls 暂不支持，请在 Excel 中另存为 .xlsx 后再导入。");
            }
            throw new NotSupportedException("仅支持 .xlsx、.csv 和 .tsv 文件。");
        }

        private static PrintQueueData ImportXlsx(string path)
        {
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

                XDocument workbook = LoadXml(archive, "xl/workbook.xml");
                XElement sheet = workbook.Descendants(spreadsheet + "sheet").FirstOrDefault();
                if (sheet == null)
                {
                    throw new InvalidDataException("Excel 工作簿中没有可读取的工作表。");
                }

                string relationshipId = (string)sheet.Attribute(relationships + "id");
                XDocument relationDocument = LoadXml(archive, "xl/_rels/workbook.xml.rels");
                XElement relation = relationDocument.Descendants(packageRelationships + "Relationship")
                    .FirstOrDefault(item => string.Equals((string)item.Attribute("Id"), relationshipId, StringComparison.Ordinal));
                if (relation == null)
                {
                    throw new InvalidDataException("Excel 工作表关系无效。");
                }

                string sheetPart = ResolvePartPath("xl/workbook.xml", (string)relation.Attribute("Target"));
                XDocument worksheet = LoadXml(archive, sheetPart);
                List<string> sharedStrings = LoadSharedStrings(archive, spreadsheet);
                HashSet<int> dateStyles = LoadDateStyles(archive, spreadsheet);

                List<ParsedRow> parsedRows = new List<ParsedRow>();
                foreach (XElement rowElement in worksheet.Descendants(spreadsheet + "row"))
                {
                    ParsedRow row = new ParsedRow
                    {
                        RowNumber = ParsePositiveInt((string)rowElement.Attribute("r"), parsedRows.Count + 1),
                        Cells = new Dictionary<int, string>()
                    };
                    int inferredColumn = 0;
                    foreach (XElement cell in rowElement.Elements(spreadsheet + "c"))
                    {
                        int column = GetColumnIndex((string)cell.Attribute("r"), inferredColumn);
                        inferredColumn = column + 1;
                        int styleIndex = ParsePositiveInt((string)cell.Attribute("s"), 0);
                        row.Cells[column] = ReadCellValue(cell, spreadsheet, sharedStrings, dateStyles.Contains(styleIndex));
                    }
                    if (row.Cells.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    {
                        parsedRows.Add(row);
                    }
                }

                if (parsedRows.Count == 0)
                {
                    throw new InvalidDataException("Excel 第一张工作表没有数据。");
                }
                return BuildResult(path, (string)sheet.Attribute("name") ?? "Sheet1", parsedRows);
            }
        }

        private static PrintQueueData ImportDelimited(string path, char delimiter)
        {
            string text = ReadTextFile(path);
            List<List<string>> records = ParseDelimited(text, delimiter);
            List<ParsedRow> rows = new List<ParsedRow>();
            for (int index = 0; index < records.Count; index++)
            {
                List<string> values = records[index];
                if (!values.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    continue;
                }
                ParsedRow row = new ParsedRow { RowNumber = index + 1, Cells = new Dictionary<int, string>() };
                for (int column = 0; column < values.Count; column++)
                {
                    row.Cells[column] = values[column];
                }
                rows.Add(row);
            }
            if (rows.Count == 0)
            {
                throw new InvalidDataException("文件中没有数据。");
            }
            return BuildResult(path, Path.GetFileNameWithoutExtension(path), rows);
        }

        private static PrintQueueData BuildResult(string path, string sheetName, List<ParsedRow> parsedRows)
        {
            ParsedRow headerRow = parsedRows[0];
            int maximumColumn = parsedRows.SelectMany(row => row.Cells.Keys).DefaultIfEmpty(0).Max();
            List<string> headers = new List<string>();
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column <= maximumColumn; column++)
            {
                string value;
                headerRow.Cells.TryGetValue(column, out value);
                string baseName = string.IsNullOrWhiteSpace(value) ? "列" + (column + 1) : value.Trim();
                string name = baseName;
                int suffix = 2;
                while (!used.Add(name))
                {
                    name = baseName + " (" + suffix++ + ")";
                }
                headers.Add(name);
            }

            List<PrintQueueRow> rows = new List<PrintQueueRow>();
            foreach (ParsedRow parsed in parsedRows.Skip(1))
            {
                List<string> values = new List<string>();
                for (int column = 0; column < headers.Count; column++)
                {
                    string value;
                    parsed.Cells.TryGetValue(column, out value);
                    values.Add(value ?? string.Empty);
                }
                rows.Add(new PrintQueueRow
                {
                    ExcelRowNumber = parsed.RowNumber,
                    Enabled = true,
                    Values = values,
                    State = PrintQueueItemState.Pending,
                    Error = string.Empty
                });
            }
            if (rows.Count == 0)
            {
                throw new InvalidDataException("第一行会作为列名；文件中没有可加入队列的数据行。");
            }
            return new PrintQueueData
            {
                SourcePath = path,
                SheetName = sheetName,
                Headers = headers,
                Rows = rows
            };
        }

        private static XDocument LoadXml(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path.Replace('\\', '/'));
            if (entry == null)
            {
                throw new InvalidDataException("Excel 文件缺少必要内容：" + path);
            }
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream, LoadOptions.None);
            }
        }

        private static string ResolvePartPath(string sourcePart, string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidDataException("Excel 工作表路径为空。");
            }
            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                return target.TrimStart('/');
            }
            Uri baseUri = new Uri("http://excel/" + sourcePart);
            return new Uri(baseUri, target).AbsolutePath.TrimStart('/');
        }

        private static List<string> LoadSharedStrings(ZipArchive archive, XNamespace spreadsheet)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }
            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                return document.Descendants(spreadsheet + "si")
                    .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
                    .ToList();
            }
        }

        private static HashSet<int> LoadDateStyles(ZipArchive archive, XNamespace spreadsheet)
        {
            HashSet<int> styles = new HashSet<int>();
            ZipArchiveEntry entry = archive.GetEntry("xl/styles.xml");
            if (entry == null)
            {
                return styles;
            }
            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                Dictionary<int, string> customFormats = document.Descendants(spreadsheet + "numFmt")
                    .Where(item => item.Attribute("numFmtId") != null)
                    .ToDictionary(
                        item => ParsePositiveInt((string)item.Attribute("numFmtId"), -1),
                        item => (string)item.Attribute("formatCode") ?? string.Empty);
                XElement cellFormats = document.Descendants(spreadsheet + "cellXfs").FirstOrDefault();
                if (cellFormats == null)
                {
                    return styles;
                }
                int index = 0;
                foreach (XElement format in cellFormats.Elements(spreadsheet + "xf"))
                {
                    int numberFormat = ParsePositiveInt((string)format.Attribute("numFmtId"), 0);
                    string custom;
                    bool builtIn = numberFormat >= 14 && numberFormat <= 22;
                    bool customDate = customFormats.TryGetValue(numberFormat, out custom) && LooksLikeDateFormat(custom);
                    if (builtIn || customDate)
                    {
                        styles.Add(index);
                    }
                    index++;
                }
            }
            return styles;
        }

        private static bool LooksLikeDateFormat(string format)
        {
            string normalized = (format ?? string.Empty).ToLowerInvariant()
                .Replace("\\", string.Empty)
                .Replace("\"", string.Empty);
            return normalized.Contains("yy") || normalized.Contains("dd") ||
                   (normalized.Contains("hh") && normalized.Contains("mm"));
        }

        private static string ReadCellValue(
            XElement cell,
            XNamespace spreadsheet,
            IList<string> sharedStrings,
            bool isDate)
        {
            string type = (string)cell.Attribute("t") ?? string.Empty;
            if (type == "inlineStr")
            {
                return string.Concat(cell.Descendants(spreadsheet + "t").Select(item => item.Value));
            }
            string raw = (string)cell.Element(spreadsheet + "v") ?? string.Empty;
            int index;
            if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : raw;
            }
            if (type == "b")
            {
                return raw == "1" ? "TRUE" : "FALSE";
            }
            double serial;
            if (isDate && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out serial))
            {
                try
                {
                    DateTime date = DateTime.FromOADate(serial);
                    return date.TimeOfDay == TimeSpan.Zero
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
                catch (ArgumentException)
                {
                    return raw;
                }
            }
            return raw;
        }

        private static int GetColumnIndex(string cellReference, int fallback)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return fallback;
            }
            int value = 0;
            foreach (char character in cellReference)
            {
                if (character < 'A' || character > 'Z')
                {
                    break;
                }
                value = value * 26 + character - 'A' + 1;
            }
            return value > 0 ? value - 1 : fallback;
        }

        private static int ParsePositiveInt(string value, int fallback)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= 0
                ? result
                : fallback;
        }

        private static string ReadTextFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private static List<List<string>> ParseDelimited(string text, char delimiter)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder value = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quoted)
                {
                    if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else if (character == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        value.Append(character);
                    }
                }
                else if (character == '"')
                {
                    quoted = true;
                }
                else if (character == delimiter)
                {
                    row.Add(value.ToString());
                    value.Clear();
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                    row.Add(value.ToString());
                    value.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    value.Append(character);
                }
            }
            if (value.Length > 0 || row.Count > 0)
            {
                row.Add(value.ToString());
                rows.Add(row);
            }
            return rows;
        }

        private sealed class ParsedRow
        {
            public int RowNumber { get; set; }
            public Dictionary<int, string> Cells { get; set; }
        }
    }
}
