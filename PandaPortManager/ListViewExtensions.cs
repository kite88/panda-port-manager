namespace PandaPortManager;

/// <summary>ListViewItem 的辅助扩展（筛选、选中恢复、排序都按「整行单元格」拼接比较）。</summary>
internal static class ListViewExtensions
{
    public static IEnumerable<string> SubCells(this ListViewItem item)
    {
        for (int i = 0; i < item.SubItems.Count; i++)
            yield return item.SubItems[i].Text;
    }
}
