using System.Runtime.InteropServices;

namespace PandaPortManager;

/// <summary>
/// 布局与高 DPI 适配：所有尺寸常量都以 96-DPI 逻辑像素为基准，
/// 运行时按 DeviceDpi 绝对重算（基准 × 新DPI/96），避免与框架自身缩放叠加造成双重缩放。
/// </summary>
public partial class MainForm
{
    private const int DefaultWindowWidth = 1080;
    private const int DefaultWindowHeight = 640;
    private const int MinWindowWidth = 820;
    private const int MinWindowHeight = 480;
    private const int FilterDefaultWidth = 240;
    private const int FilterMinWidth = 100;
    private const int RefreshWidth = 80;
    private const int KillWidth = 100;
    private const int IconBtnWidth = 68;
    private const int IconBtnHeight = 42;
    /// <summary>工具栏图标按钮中心 Y 坐标（96-DPI 逻辑像素），随 DeviceDpi 缩放。</summary>
    private const int IconCenterY = 14;
    /// <summary>工具栏图标按钮文字标签高度（96-DPI 逻辑像素）。</summary>
    private const int IconLabelHeight = 18;
    /// <summary>工具栏图标按钮文字标签距底边距离（96-DPI 逻辑像素）。</summary>
    private const int IconLabelBottom = 18;
    /// <summary>按钮高度余量（96-DPI 逻辑像素，随 ScaleToolbarFor 一起按 DPI 缩放）：
    /// 覆盖 GetPreferredSize 未计入的 1px 边框上下各 1px。</summary>
    private const int ButtonHeightSlack = 4;

    /// <summary>各列的基准宽度（96-DPI 逻辑像素，运行时按 DeviceDpi 缩放）。</summary>
    private static readonly int[] ColWidths = { 60, 170, 80, 170, 80, 110, 60, 120, 180 };

    // 工具栏控件在 96-DPI 下的基准尺寸/边距（Load 时捕获一次）。
    // DPI 变化时按「基准 × 新DPI/96」绝对重算并覆写，避免与框架自身缩放叠加造成双重缩放
    // （双重缩放曾导致跨屏后按钮高度小于文字而被截断）。
    private int _baseTopH, _baseIconH, _baseRefreshH, _baseKillH;
    private Padding _baseTopPad, _baseFilterLabelMargin, _baseAutoRefreshMargin, _baseFilterPanelPad,
                    _baseRefreshMargin, _baseKillMargin, _baseIconMargin;
    private bool _baseCaptured;
    // Load 时的基准字体（96-DPI 设计字号），跨屏 DPI 变化时钳回该 pt 值
    private Font? _baseFont;

    /// <summary>DPI 变化后的统一重排：窗口逻辑尺寸保持、字体绝对重设、工具栏/列宽按新 DPI 绝对重算。</summary>
    private void ApplyDpiLayout(int dpiNew, int dpiOld, Size physOld)
    {
        try
        {
#if DPI_TEST
            System.IO.File.AppendAllText(@"e:\xproject\panda-port-manager\tools\dpitest\dpilog.txt",
                $"invoked new={dpiNew} old={dpiOld} top={_top.Height} refresh={_btnRefresh.Width}x{_btnRefresh.Height} " +
                $"kill={_btnKill.Width}x{_btnKill.Height} filterPanel={_filterPanel.Width}x{_filterPanel.Height} " +
                $"fontPt={Font.Size:0.##} fontPx={Font.Height} win={Width}x{Height} client={ClientSize.Width} t={DateTime.Now:HH:mm:ss.fff}\r\n");
#endif
            // 关键：保持窗口逻辑尺寸不变（旧物理 / 旧DPI × 新DPI）。
            // 框架/系统在某些路径（最大化恢复、程序化移动等）不会按建议矩形放大窗口，
            // 导致 2K 尺寸的内容挤在小窗里（筛选框被压缩、按钮显得过大）。
            int wantW = (int)Math.Round(physOld.Width * dpiNew / (float)dpiOld);
            int wantH = (int)Math.Round(physOld.Height * dpiNew / (float)dpiOld);
            if (dpiOld > 0 && (Math.Abs(Width - wantW) > 2 || Math.Abs(Height - wantH) > 2))
                Size = new Size(wantW, wantH);

            // 注意顺序：必须先更新按钮 MinimumSize，再重设字体——
            // 字体变化会触发 AutoSize 按钮按「新字体 + 新最小宽度」重新布局；
            // 反过来先改字体时 MinimumSize 还是上一屏的值，按钮会被撑大且
            // 之后仅改 MinimumSize 不会再触发布局（按钮将卡在大尺寸）。
            float f = dpiNew / 96f;
            ScaleToolbarFor(f);

            // 字号 pt 钳回基准值：pt 是绝对单位（渲染像素 = pt × DPI/72 自动随屏幕缩放），
            // 任何 DPI 下都应保持基准 10pt。框架在 WM_DPICHANGED 时会按比例乱改 pt
            // （下降时砍半、上升时翻倍），这里统一钳回，双向幂等。
            if (_baseFont != null && Math.Abs(Font.Size - _baseFont.Size) > 0.01f)
                Font = new Font(_baseFont.FontFamily, _baseFont.Size, _baseFont.Style);

            // 按钮为显式尺寸（ScaleToolbarFor 已按新 DPI 设置），无需额外处理

            LayoutFilterPanel();

            // 字体/尺寸切换后强制重绘，清除旧字号的残影
            _top.Invalidate(true);
            _filterPanel.Invalidate();
            _filter.Invalidate();
            for (int i = 0; i < _list.Columns.Count && i < ColWidths.Length; i++)
                _list.Columns[i].Width = (int)Math.Round(ColWidths[i] * dpiNew / 96f);
            CenterToolbarControls();
            AdjustLastColumnFill();
            AdjustToolbarSpacer();
#if DPI_TEST
            System.IO.File.AppendAllText(@"e:\xproject\panda-port-manager\tools\dpitest\dpilog.txt",
                $"done    new={dpiNew} top={_top.Height} refresh={_btnRefresh.Width}x{_btnRefresh.Height} " +
                $"kill={_btnKill.Width}x{_btnKill.Height} filterPanel={_filterPanel.Width}x{_filterPanel.Height} " +
                $"fontPt={Font.Size:0.##} fontPx={Font.Height} win={Width}x{Height} client={ClientSize.Width} t={DateTime.Now:HH:mm:ss.fff}\r\n");
#endif
        }
#if DPI_TEST
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(@"e:\xproject\panda-port-manager\tools\dpitest\dpilog.txt",
                $"ERROR   {ex.GetType().Name}: {ex.Message}\r\n{ex.StackTrace}\r\n");
        }
#endif
        catch
        {
            // 忽略重排中的瞬态异常，避免影响消息循环
        }
    }

#if DPI_TEST
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int L; public int T; public int R; public int B; }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
#endif

    private static Padding ScalePadding(Padding p, float dpi) => new Padding(
        (int)Math.Round(p.Left * dpi), (int)Math.Round(p.Top * dpi),
        (int)Math.Round(p.Right * dpi), (int)Math.Round(p.Bottom * dpi));

    private void CaptureToolbarBaseSizes()
    {
        if (_baseCaptured) return;
        _baseFont = Font;
        _baseTopH = _top.Height;
        _baseTopPad = _top.Padding;
        // 刷新/结束进程按钮高度取 AutoSize 等效值（GetPreferredSize，含文字+内边距，
        // 96-DPI + 雅黑 10pt 实测约 29px）——控件默认高度 23px 装不下 17px 的文字会裁掉下半截。
        // 再加 ButtonHeightSlack 余量：捕获时按钮 BorderSize=0，而「结束进程」运行到未选中态
        // 会设成 1px 边框（多占 2px），不预留会被裁掉 2px。该值随启动屏 DPI 缩放，归一化回96-DPI 基准。
        float norm = 96f / DeviceDpi;
        _baseRefreshH = (int)Math.Round(_btnRefresh.GetPreferredSize(Size.Empty).Height * norm) + ButtonHeightSlack;
        _baseKillH = (int)Math.Round(_btnKill.GetPreferredSize(Size.Empty).Height * norm) + ButtonHeightSlack;
        _baseFilterLabelMargin = _filterLabel.Margin;
        _baseAutoRefreshMargin = _autoRefresh.Margin;
        _baseFilterPanelPad = _filterPanel.Padding;
        _baseRefreshMargin = _btnRefresh.Margin;
        _baseKillMargin = _btnKill.Margin;
        _baseIconH = _settingsBtn.Height;
        _baseIconMargin = _settingsBtn.Margin;
        _baseCaptured = true;
    }

    /// <summary>
    /// 按给定 DPI 系数（新DPI/96，绝对值）重算工具栏中手工设定的固定尺寸并覆写。
    /// 幂等：以 96-DPI 基准值计算，重复调用或跨屏多次拖拽都不会累积误差。
    /// </summary>
    private void ScaleToolbarFor(float f)
    {
        if (!_baseCaptured) CaptureToolbarBaseSizes();
        _top.Height = (int)Math.Round(_baseTopH * f);
        _top.Padding = ScalePadding(_baseTopPad, f);
        _filterLabel.Margin = ScalePadding(_baseFilterLabelMargin, f);
        _autoRefresh.Margin = ScalePadding(_baseAutoRefreshMargin, f);
        _filterPanel.Width = (int)Math.Round((FilterDefaultWidth + 2) * f);
        _filterPanel.Padding = ScalePadding(_baseFilterPanelPad, f);
        _filter.Width = Math.Max((int)Math.Round(FilterMinWidth * f), _filterPanel.Width - 2);
        // 刷新/结束进程：显式尺寸绝对重算（字号 pt 恒为基准 10pt，渲染像素随屏幕自动缩放，
        // 17px@96 / 34px@192 恒小于按钮高度，不会截断；且不依赖框架的字体行为，完全确定性）
        _btnRefresh.Size = new Size((int)Math.Round(RefreshWidth * f), (int)Math.Round(_baseRefreshH * f));
        _btnRefresh.Margin = ScalePadding(_baseRefreshMargin, f);
        _btnKill.Size = new Size((int)Math.Round(KillWidth * f), (int)Math.Round(_baseKillH * f));
        _btnKill.Margin = ScalePadding(_baseKillMargin, f);
        foreach (var b in new[] { _settingsBtn, _aboutBtn })
        {
            b.Width = (int)Math.Round(IconBtnWidth * f);
            b.Height = (int)Math.Round(_baseIconH * f);
            b.Margin = ScalePadding(_baseIconMargin, f);
        }
    }

    /// <summary>按 DPI 等比缩放一棵控件树的 Location/Size（AutoSize 控件只缩 Location）。</summary>
    private static void ScaleControlTreeToDpi(Control root, float dpi)
    {
        foreach (Control c in root.Controls)
        {
            c.Location = new Point((int)Math.Round(c.Location.X * dpi), (int)Math.Round(c.Location.Y * dpi));
            if (!c.AutoSize)
            {
                c.Width = (int)Math.Round(c.Width * dpi);
                c.Height = (int)Math.Round(c.Height * dpi);
            }
            ScaleControlTreeToDpi(c, dpi);
        }
    }

    /// <summary>弹窗整体（含控件树）按主窗体当前 DPI 等比缩放（在 ShowDialog 前调用）。</summary>
    private void ScaleDialogToDpi(Form f)
    {
        float dpi = DeviceDpi / 96f;
        if (dpi <= 1.01f) return;
        f.ClientSize = new Size((int)Math.Round(f.ClientSize.Width * dpi),
                                (int)Math.Round(f.ClientSize.Height * dpi));
        ScaleControlTreeToDpi(f, dpi);
    }



    /// <summary>把弹窗按钮（如“关闭”）按当前 DPI 钉到窗体右下角，并锚定右下角。</summary>
    private void PinDialogButtonToBottomRight(Form f, Control btn, int margin)
    {
        int m = (int)Math.Round(margin * DeviceDpi / 96f);
        btn.Location = new Point(f.ClientSize.Width - btn.Width - m,
                                 f.ClientSize.Height - btn.Height - m);
        btn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
    }

    private void AdjustToolbarSpacer()
    {
        int left = 0, right = 0;
        bool pastSpacer = false;
        foreach (Control c in _top.Controls)
        {
            if (c == _spacer)
            {
                pastSpacer = true;
                continue;
            }
            int total = c.Width + c.Margin.Left + c.Margin.Right;
            if (pastSpacer) right += total;
            else left += total;
        }
        int avail = _top.ClientSize.Width - _top.Padding.Left - _top.Padding.Right;
        int w = avail - left - right - _spacer.Margin.Left - _spacer.Margin.Right;

        // 筛选框的默认/最小宽度按 DPI 缩放（控件实际宽度已是设备像素）
        float dpi = DeviceDpi / 96f;
        int defW = (int)Math.Round(FilterDefaultWidth * dpi);
        int minW = (int)Math.Round(FilterMinWidth * dpi);

        if (w < 0)
        {
            // 空间不足：压缩筛选框宽度，保证右侧设置/关于按钮完整可见
            int shrink = Math.Min(-w, Math.Max(0, _filterPanel.Width - minW));
            if (shrink > 0)
            {
                _filterPanel.Width -= shrink;
                left -= shrink;
                w += shrink;
            }
        }
        else if (_filterPanel.Width < defW)
        {
            // 空间富余：筛选框恢复默认宽度
            int grow = Math.Min(w, defW - _filterPanel.Width);
            if (grow > 0)
            {
                _filterPanel.Width += grow;
                left += grow;
                w -= grow;
            }
        }

        if (w < 0) w = 0;
        if (_spacer.Width != w) _spacer.Width = w;
    }

    /// <summary>
    /// 筛选框面板高度与工具栏按钮（刷新）一致，输入框在边框内垂直居中；
    /// 输入框过高时退回「输入框高度 + 内边距」保证不溢出。
    /// </summary>
    private void LayoutFilterPanel()
    {
        int h = Math.Max(_btnRefresh.Height,
                         _filter.Height + _filterPanel.Padding.Top + _filterPanel.Padding.Bottom);
        _filterPanel.Height = h;
        _filter.Location = new Point(_filterPanel.Padding.Left, Math.Max(1, (h - _filter.Height) / 2));
    }

    /// <summary>筛选框边框颜色：跟随主题，聚焦时略亮；未聚焦用柔和的细线。</summary>
    private Color FilterBorderColor => _filter.Focused
        ? (_isDark ? Color.FromArgb(0x6B, 0x6B, 0x74) : Color.FromArgb(0x8A, 0x8A, 0x8A))
        : (_isDark ? Color.FromArgb(0x45, 0x45, 0x4A) : Color.FromArgb(0xAD, 0xAD, 0xAD));

    private void FilterPanel_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(FilterBorderColor);
        var r = e.ClipRectangle;
        e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
    }

    /// <summary>主题/焦点变化时重绘筛选框边框。</summary>
    private void UpdateFilterBorder() => _filterPanel.Invalidate();

    /// <summary>工具栏所有控件按垂直中心线对齐（按钮/复选框/输入框在同一水平线上）。</summary>
    private void CenterToolbarControls()
    {
        int content = _top.ClientSize.Height - _top.Padding.Top - _top.Padding.Bottom;
        int center = _top.Padding.Top + content / 2;
        foreach (Control c in _top.Controls)
        {
            if (c == _spacer) continue;
            int top = center - c.Height / 2 - _top.Padding.Top;
            if (top < 0) top = 0;
            c.Margin = new Padding(c.Margin.Left, top, c.Margin.Right, 0);
        }
    }

    /// <summary>让最后一列（进程名）自动填满列表剩余宽度。</summary>
    private void AdjustLastColumnFill()
    {
        if (_adjustingColumns || _list.Columns.Count == 0) return;
        _adjustingColumns = true;
        try
        {
            int fixedWidth = 0;
            for (int i = 0; i < _list.Columns.Count - 1; i++)
                fixedWidth += _list.Columns[i].Width;

            int remaining = _list.ClientSize.Width - fixedWidth;
            if (remaining > 0 && _list.Columns[^1].Width != remaining)
                _list.Columns[^1].Width = remaining;
        }
        finally
        {
            _adjustingColumns = false;
        }
    }
}
