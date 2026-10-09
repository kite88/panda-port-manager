using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PandaPortManager;

/// <summary>
/// 主题：亮/暗调色板、DWM 暗色标题栏、自绘列表表头与右键菜单配色。
/// </summary>
public partial class MainForm
{
    private enum ThemeMode { Light = 0, Dark = 1, System = 2 }

    // 暗色调色板
    private static readonly Color DarkFormBack = Color.FromArgb(0x1E, 0x1E, 0x1E);
    private static readonly Color DarkToolbarBack = Color.FromArgb(0x25, 0x25, 0x28);
    private static readonly Color DarkControlBack = Color.FromArgb(0x2D, 0x2D, 0x30);
    private static readonly Color DarkHeaderBack = Color.FromArgb(0x33, 0x33, 0x37);
    private static readonly Color DarkGrid = Color.FromArgb(0x3A, 0x3A, 0x3D);
    // 暗色主题文字更白
    private static readonly Color DarkFore = Color.FromArgb(0xF2, 0xF2, 0xF2);
    // 亮色主题文字更深（纯黑，比系统默认 ControlText/WindowText 更黑）
    private static readonly Color LightFore = Color.FromArgb(0x00, 0x00, 0x00);
    // 提示语（次要文字）颜色：亮色更黑、暗色更白
    private static readonly Color LightHint = Color.FromArgb(0x6E, 0x6E, 0x6E);
    private static readonly Color DarkHint = Color.FromArgb(0xB5, 0xB5, 0xB5);
    private static readonly Color DarkStatusBack = Color.FromArgb(0x2D, 0x2D, 0x30);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_ALT = 19; // 早期 Win10 构建用 19
    private const int DWMWA_CAPTION_COLOR = 35;               // Win11：精确设置标题栏颜色
    private const int DWMWA_TEXT_COLOR = 36;                  // Win11：标题文字颜色

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>标题栏跟随主题的弹窗窗体：暗色时用 DWM 暗色模式 + Win11 精确标题栏配色。</summary>
    private sealed class ThemedForm : Form
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool DarkTitle { get; set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitle();
        }

        // 句柄刚创建时设置会被 DWM 初始化覆盖，显示后再应用一次才能生效
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyDarkTitle();
        }

        public void ApplyDarkTitle()
        {
            if (!IsHandleCreated) return;
            int on = DarkTitle ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_ALT, ref on, sizeof(int));
            if (DarkTitle)
            {
                // Win11 直接指定标题栏底色/文字色，与窗体内容色一致
                int caption = ColorTranslator.ToWin32(Color.FromArgb(0x1E, 0x1E, 0x1E));
                int text = ColorTranslator.ToWin32(Color.FromArgb(0xE0, 0xE0, 0xE0));
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
            else
            {
                int none = unchecked((int)0xFFFFFFFF); // DWMWA_COLOR_NONE：恢复系统默认
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref none, sizeof(int));
            }
        }
    }

    private void ApplyTheme()
    {
        _isDark = _themeMode == ThemeMode.Dark || (_themeMode == ThemeMode.System && IsSystemDark());
        ApplyPalette(_isDark);
        if (IsHandleCreated)
            SetImmersiveDarkMode(_isDark);
    }

    private void ApplyPalette(bool dark)
    {
        Color formBack = dark ? DarkFormBack : SystemColors.Control;
        Color ctrlBack = dark ? DarkControlBack : SystemColors.Window;
        Color fore = dark ? DarkFore : LightFore;
        Color statusBack = dark ? DarkStatusBack : SystemColors.Control;

        BackColor = formBack;
        ForeColor = fore;
        _top.BackColor = dark ? DarkToolbarBack : SystemColors.Control;
        _top.ForeColor = fore;

        _list.BackColor = ctrlBack;
        _list.ForeColor = fore;

        // 筛选框：暗色下内部与工具栏同色（完全融入，只靠 1px 描边勾轮廓），避免突兀的灰底
        _filter.BackColor = dark ? DarkToolbarBack : SystemColors.Window;
        _filter.ForeColor = fore;
        _filterPanel.BackColor = _filter.BackColor;
        _filterHint.ForeColor = dark ? DarkHint : LightHint;
        UpdateFilterBorder();

        _status.BackColor = statusBack;
        _statusLabel.ForeColor = fore;

        foreach (Control c in _top.Controls)
        {
            switch (c)
            {
                case Button b:
                    if (b == _btnKill)
                    {
                        UpdateKillButton();
                    }
                    else if (b == _settingsBtn || b == _aboutBtn)
                    {
                        b.BackColor = Color.Transparent;
                        b.ForeColor = fore;
                        b.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(0x3A, 0x3A, 0x3D) : SystemColors.ControlLight;
                        b.FlatAppearance.MouseDownBackColor = dark ? Color.FromArgb(0x45, 0x45, 0x4A) : SystemColors.ControlDark;
                    }
                    else
                    {
                        b.BackColor = dark ? DarkControlBack : SystemColors.Control;
                        b.ForeColor = fore;
                    }
                    break;
                case Label l:
                    l.ForeColor = fore;
                    l.BackColor = Color.Transparent;
                    break;
                case CheckBox cb:
                    cb.ForeColor = fore;
                    cb.BackColor = Color.Transparent;
                    break;
            }
        }

        // 列表右键菜单同样跟随主题
        if (dark)
        {
            _rowMenu.RenderMode = ToolStripRenderMode.Professional;
            _rowMenu.Renderer = new DarkMenuRenderer(new DarkColorTable());
            _rowMenu.BackColor = DarkControlBack;
            SetMenuForeColor(_rowMenu.Items, Color.White);
        }
        else
        {
            _rowMenu.RenderMode = ToolStripRenderMode.System;
            _rowMenu.BackColor = SystemColors.Control;
            SetMenuForeColor(_rowMenu.Items, Color.Black);
        }

        _settingsBtn.Invalidate();
        _aboutBtn.Invalidate();
        UpdateKillButton();
    }

    private static void SetMenuForeColor(ToolStripItemCollection items, Color fore)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = fore;
            if (item is ToolStripMenuItem mi && mi.DropDownItems.Count > 0)
                SetMenuForeColor(mi.DropDownItems, fore);
        }
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v)
                return v == 0;
        }
        catch { }
        return false;
    }

    private void SetImmersiveDarkMode(bool enabled)
    {
        int value = enabled ? 1 : 0;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, Marshal.SizeOf(typeof(int)));
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _themeMode == ThemeMode.System && IsHandleCreated)
            ApplyTheme();
    }

    private void List_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        if (!_isDark)
        {
            e.DrawDefault = true;
            return;
        }

        using var brush = new SolidBrush(DarkHeaderBack);
        e.Graphics.FillRectangle(brush, e.Bounds);

        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? string.Empty, e.Font,
            new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
            DarkFore, DarkHeaderBack,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        using var pen = new Pen(DarkGrid);
        e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => DarkControlBack;
        public override Color MenuBorder => DarkGrid;
        public override Color MenuItemBorder => DarkGrid;
        public override Color MenuItemSelected => DarkHeaderBack;
        public override Color MenuItemSelectedGradientBegin => DarkHeaderBack;
        public override Color MenuItemSelectedGradientEnd => DarkHeaderBack;
        public override Color ButtonSelectedGradientBegin => DarkHeaderBack;
        public override Color ButtonSelectedGradientEnd => DarkHeaderBack;
        public override Color ButtonCheckedGradientBegin => DarkToolbarBack;
        public override Color ButtonCheckedGradientEnd => DarkToolbarBack;
        public override Color ImageMarginGradientBegin => DarkControlBack;
        public override Color ImageMarginGradientMiddle => DarkControlBack;
        public override Color ImageMarginGradientEnd => DarkControlBack;
        public override Color SeparatorLight => DarkGrid;
        public override Color SeparatorDark => DarkToolbarBack;
    }

    // 强制下拉菜单项使用 item.ForeColor（Professional 渲染器默认会忽略它）
    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer(ProfessionalColorTable table) : base(table) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.ForeColor != Color.Empty)
                e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }
}
