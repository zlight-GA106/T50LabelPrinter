using System;
using System.Collections.Generic;
using System.Linq;

namespace T50LabelPrinter
{
    public enum PrintQueueItemState
    {
        Pending,
        Printing,
        Completed,
        Failed,
        Skipped
    }

    public sealed class PrintQueueRow
    {
        public int ExcelRowNumber { get; set; }
        public bool Enabled { get; set; }
        public List<string> Values { get; set; }
        public PrintQueueItemState State { get; set; }
        public string Error { get; set; }

        public string StateText
        {
            get
            {
                switch (State)
                {
                    case PrintQueueItemState.Printing: return "打印中";
                    case PrintQueueItemState.Completed: return "已完成";
                    case PrintQueueItemState.Failed: return "失败";
                    case PrintQueueItemState.Skipped: return "已跳过";
                    default: return "待打印";
                }
            }
        }
    }

    public sealed class PrintQueueData
    {
        public string SourcePath { get; set; }
        public string SheetName { get; set; }
        public List<string> Headers { get; set; }
        public List<PrintQueueRow> Rows { get; set; }
    }

    public sealed class QueueObjectMapping
    {
        public int ObjectId { get; set; }
        public int ColumnIndex { get; set; }
    }

    public static class QueueDocumentMapper
    {
        public static LabelDocument Apply(
            LabelDocument template,
            PrintQueueRow row,
            IEnumerable<QueueObjectMapping> mappings)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }
            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            LabelDocument document = template.DeepClone();
            Dictionary<int, int> columns = mappings
                .Where(mapping => mapping.ColumnIndex >= 0)
                .GroupBy(mapping => mapping.ObjectId)
                .ToDictionary(group => group.Key, group => group.Last().ColumnIndex);

            foreach (LabelElement element in document.Elements)
            {
                int columnIndex;
                if (!columns.TryGetValue(element.ObjectId, out columnIndex) ||
                    columnIndex < 0 || columnIndex >= row.Values.Count || element.IsImage)
                {
                    continue;
                }

                string value = row.Values[columnIndex] ?? string.Empty;
                if (element.Kind == LabelElementKind.Text)
                {
                    element.Text = value;
                }
                else if (element.IsBarcode)
                {
                    element.QueueMappedContent = value;
                    if (element.PrintDigits)
                    {
                        element.DigitsText = new string(value.Where(char.IsDigit).ToArray());
                    }
                }
            }
            return document;
        }
    }
}
