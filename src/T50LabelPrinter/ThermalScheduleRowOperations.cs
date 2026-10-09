using System;
using System.Collections.Generic;
using System.Linq;

namespace T50LabelPrinter
{
    public static class ThermalScheduleRowOperations
    {
        public static bool CanMerge(IList<ThermalScheduleItem> items, IEnumerable<int> selection)
        {
            int[] indices = selection.Distinct().OrderBy(index => index).ToArray();
            return indices.Length >= 2 && indices.All(index => index >= 0 && index < items.Count &&
                items[index].IsEmptySchedule) && indices.Last() - indices.First() + 1 == indices.Length;
        }

        public static int Merge(IList<ThermalScheduleItem> items, IEnumerable<int> selection)
        {
            int[] indices = selection.Distinct().OrderBy(index => index).ToArray();
            if (!CanMerge(items, indices))
                throw new InvalidOperationException("请选择两个或以上连续的空日程行；时间、内容和完成标记都必须为空。");
            int first = indices[0];
            items[first].RowSpan = indices.Sum(index => Math.Max(1, items[index].RowSpan));
            for (int i = indices.Length - 1; i > 0; i--) items.RemoveAt(indices[i]);
            return first;
        }

        public static bool CanSplit(IList<ThermalScheduleItem> items, IEnumerable<int> selection)
        {
            int[] indices = selection.Distinct().ToArray();
            return indices.Length > 0 && indices.All(index => index >= 0 && index < items.Count &&
                items[index].IsEmptySchedule) && indices.Any(index => items[index].RowSpan > 1);
        }

        public static void Split(IList<ThermalScheduleItem> items, IEnumerable<int> selection)
        {
            int[] indices = selection.Distinct().OrderByDescending(index => index).ToArray();
            if (!CanSplit(items, indices))
                throw new InvalidOperationException("只能拆分空的合并行；请先清空时间、内容和完成标记。");
            foreach (int index in indices)
            {
                int span = Math.Max(1, items[index].RowSpan);
                items[index].RowSpan = 1;
                items[index].MergedHeightMm = 0m;
                for (int i = 1; i < span; i++) items.Insert(index + i, items[index].DeepClone());
            }
        }
    }
}
