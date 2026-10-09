using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PandaPortManager;

/// <summary>
/// 端口管理器主窗体骨架：控件字段、构造函数与 Load（DPI 归一、主题/语言初始化、事件绑定）。
/// 其余职责按 partial 拆分到同名文件：
/// <list type="bullet">
/// <item>MainForm.Layout —— DPI 缩放与工具栏/筛选框布局</item>
/// <item>MainForm.Theme —— 主题调色板、暗色标题栏、表头自绘</item>
/// <item>MainForm.Controls —— 自绘按钮/列表等控件</item>
/// <item>MainForm.Ports —— 端口扫描、PID 映射与筛选</item>
/// <item>MainForm.Actions —— 结束进程与保留端口处理</item>
/// <item>MainForm.Dialogs —— 设置 / 关于弹窗</item>
/// <item>MainForm.Updates —— 更新检测</item>
/// <item>MainForm.Settings —— 主题/语言的读写与界面文案应用</item>
/// </list>
/// </summary>
public partial class MainForm : Form
{
    private readonly ListView _list;
    private readonly TextBox _filter;
    private readonly Panel _filterPanel; // 筛选框的 1px 主题色边框容器
    private readonly Label _filterHint;   // 叠加在输入框上的提示语（可跟随主题调色）
    private readonly Label _filterLabel;
    private readonly CheckBox _autoRefresh;
    private readonly Button _btnRefresh;
    private readonly Button _btnKill;
    private readonly Button _settingsBtn;
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Dictionary<int, string> _processNameCache = new();
    private readonly List<ListViewItem> _allItems = new();
    private readonly Dictionary<string, DateTime> _firstSeen = new();
    private ContextMenuStrip _rowMenu = null!;
    private ToolStripMenuItem _endProcessMenuItem = null!;
    private ListViewItem? _menuTarget;
    private int _sortColumn = 7;
    private bool _sortAscending = false;
    private bool _adjustingColumns;

    private readonly FlowLayoutPanel _top;
    private readonly Label _spacer;
    private ThemeMode _themeMode = ThemeMode.System;
    private bool _isDark;

    private readonly Button _aboutBtn;

    public MainForm()
    {
        Text = R.T("AppTitle");
        Size = new Size(1080, 640);
        MinimumSize = new Size(820, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10F);

        // ---- 顶部工具栏 ----
        var top = _top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(8, 4, 8, 2),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _filterLabel = new Label
        {
            Text = R.T("Filter"),
            AutoSize = true,
            Margin = new Padding(3, 16, 4, 0),
        };
        top.Controls.Add(_filterLabel);

        _filter = new TextBox
        {
            Width = 240,
            Margin = new Padding(3, 14, 3, 0),
            BorderStyle = BorderStyle.None,
        };

        // 提示语用叠加 Label 自定义颜色（系统 PlaceholderText 固定灰色，无法跟随主题）
        _filterHint = new Label
        {
            Text = R.T("FilterPlaceholder"),
            ForeColor = _isDark ? DarkHint : LightHint,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        _filterHint.Click += (_, _) => _filter.Focus();

        _filter.TextChanged += (_, _) => { ApplyFilter(); _filterHint.Visible = _filter.Text.Length == 0 && !_filter.Focused; };

        // TextBox 自身不支持画主题色边框，外包一层 Panel：底色与输入框/按钮一致，
        // 边框由 Paint 画 1px 细线（避免面板背景露出成厚框、与工具栏色不一致）
        _filterPanel = new Panel
        {
            Width = FilterDefaultWidth + 2,
            Padding = new Padding(1, 5, 1, 5), // 左右留 1px 边框位，上下留白让输入框垂直居中
            Margin = new Padding(3, 14, 3, 0),
            BackColor = DarkControlBack,
        };
        _filterPanel.Paint += FilterPanel_Paint;
        _filter.Dock = DockStyle.None;
        _filter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _filter.Location = new Point(1, 5);
        _filterPanel.Controls.Add(_filter);
        _filter.GotFocus += (_, _) => { UpdateFilterBorder(); _filterHint.Visible = false; };
        _filter.LostFocus += (_, _) => { UpdateFilterBorder(); _filterHint.Visible = _filter.Text.Length == 0; };
        _filterPanel.Controls.Add(_filterHint);
        top.Controls.Add(_filterPanel);

        _btnRefresh = new TextButton { Text = R.T("Refresh"), Width = RefreshWidth, FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 14, 3, 0) };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += (_, _) => RefreshPorts();
        top.Controls.Add(_btnRefresh);

        _autoRefresh = new CheckBox
        {
            Text = R.T("AutoRefresh"),
            AutoSize = true,
            Margin = new Padding(10, 16, 3, 0),
        };
        top.Controls.Add(_autoRefresh);

        _btnKill = new TextButton
        {
            Text = R.T("Kill"),
            Width = KillWidth,
            Margin = new Padding(10, 14, 3, 0),
            BackColor = Color.FromArgb(192, 57, 43),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _btnKill.FlatAppearance.BorderSize = 0;
        _btnKill.Click += (_, _) => KillSelected();
        top.Controls.Add(_btnKill);

        _spacer = new Label { AutoSize = false, Width = 0 };
        top.Controls.Add(_spacer);

        _settingsBtn = new FocuslessButton
        {
            Text = "",
            Width = IconBtnWidth,
            Height = IconBtnHeight,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(10, 1, 3, 0),
        };
        _settingsBtn.FlatAppearance.BorderSize = 0;
        _settingsBtn.Click += (_, _) => ShowSettingsDialog();
        _settingsBtn.Paint += SettingsBtn_Paint;
        top.Controls.Add(_settingsBtn);

        _aboutBtn = new FocuslessButton
        {
            Text = "",
            Width = IconBtnWidth,
            Height = IconBtnHeight,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3, 1, 8, 0),
        };
        _aboutBtn.FlatAppearance.BorderSize = 0;
        _aboutBtn.Click += (_, _) => ShowAbout();
        _aboutBtn.Paint += AboutBtn_Paint;
        top.Controls.Add(_aboutBtn);

        top.SizeChanged += (_, _) => AdjustToolbarSpacer();

        // ---- 列表 ----
        _list = new DoubleBufferedListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            GridLines = false,
            HideSelection = false,
            OwnerDraw = true,
        };
        _list.DrawColumnHeader += List_DrawColumnHeader;
        _list.DrawItem += (_, e) => e.DrawDefault = true;
        _list.DrawSubItem += (_, e) => e.DrawDefault = true;

        _rowMenu = new ContextMenuStrip();
        _endProcessMenuItem = new ToolStripMenuItem(R.T("Kill"));
        _endProcessMenuItem.Click += EndProcessMenu_Click;
        _rowMenu.Items.Add(_endProcessMenuItem);
        _rowMenu.Opening += RowMenu_Opening;
        _list.ContextMenuStrip = _rowMenu;
        _list.SizeChanged += (_, _) => AdjustLastColumnFill();

        _list.Columns.Add(R.T("ColProto"), ColWidths[0]);
        _list.Columns.Add(R.T("ColLocalAddr"), ColWidths[1]);
        _list.Columns.Add(R.T("ColLocalPort"), ColWidths[2], HorizontalAlignment.Right);
        _list.Columns.Add(R.T("ColRemoteAddr"), ColWidths[3]);
        _list.Columns.Add(R.T("ColRemotePort"), ColWidths[4], HorizontalAlignment.Right);
        _list.Columns.Add(R.T("ColState"), ColWidths[5]);
        _list.Columns.Add(R.T("ColPid"), ColWidths[6], HorizontalAlignment.Right);
        _list.Columns.Add(R.T("ColTime"), ColWidths[7]);
        _list.Columns.Add(R.T("ColProcess"), ColWidths[8]);
        _list.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);

        _list.ColumnClick += List_ColumnClick;
        _list.SelectedIndexChanged += (_, _) => UpdateKillButton();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                KillSelected();
            }
        };

        // ---- 状态栏 ----
        _status = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel(R.T("Ready"));
        _status.Items.Add(_statusLabel);

        Controls.Add(_list);
        Controls.Add(top);
        Controls.Add(_status);

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) => RefreshPorts();
        _autoRefresh.CheckedChanged += (_, _) => _timer.Enabled = _autoRefresh.Checked;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        FormClosed += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        Load += (_, _) =>
        {
            // 高 DPI 适配：PMv2 下字体按显示器 DPI 渲染，但纯代码创建的窗体不会自动
            // 缩放子控件（框架只放大窗口本身），导致文字溢出控件、相互遮挡。
            // 这里按 DeviceDpi 统一手工等比缩放：窗口、工具栏、列宽全部按同一系数。
            _baseFont = Font;   // 96-DPI 基准字号，跨屏 DpiChanged 时绝对重设用
            CaptureToolbarBaseSizes();   // 必须在 Load 立即捕获：此时 DeviceDpi=启动屏 DPI，
                                         // 归一化才正确；推迟到 DpiChanged 时 DeviceDpi 已变，会双重归一
            float dpi = DeviceDpi / 96f;
            if (dpi > 1.01f)
            {
                MinimumSize = new Size((int)Math.Round(MinWindowWidth * dpi),
                                       (int)Math.Round(MinWindowHeight * dpi));
                Width = (int)Math.Round(DefaultWindowWidth * dpi);
                Height = (int)Math.Round(DefaultWindowHeight * dpi);
            }

            // 窗口可能超出屏幕（小屏 + 高 DPI），钳制到工作区并重新居中
            var wa = Screen.FromControl(this).WorkingArea;
            if (Width > wa.Width || Height > wa.Height)
            {
                Width = Math.Min(Width, wa.Width);
                Height = Math.Min(Height, wa.Height);
            }
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2),
                                 wa.Top + Math.Max(0, (wa.Height - Height) / 2));

            // 工具栏尺寸无条件重算：100% DPI（1K 屏）时也必须给按钮显式高度，否则沿用
            // WinForms 默认 23px，装不下 17px 的雅黑文字（实测「刷新」「结束进程」上下都被裁，
            // 后者因未选中态多一圈 1px 边框裁得更狠）。ScaleToolbarFor 以 96-DPI 基准绝对重算，
            // dpi=1.0 时是幂等的。
            ScaleToolbarFor(dpi);

            // 筛选框面板：高度与工具栏按钮一致，输入框在边框内居中
            LayoutFilterPanel();

            // ListView 列宽按 DPI 显式设置（列宽不随窗体缩放，取绝对值避免二次缩放）
            for (int i = 0; i < _list.Columns.Count && i < ColWidths.Length; i++)
                _list.Columns[i].Width = (int)Math.Round(ColWidths[i] * dpi);

            var (theme, lang) = LoadSettings();
            _themeMode = theme;
            R.Lang = lang;
            ApplyLanguage();
            ApplyTheme();
            RefreshPorts();
            AdjustLastColumnFill();
            AdjustToolbarSpacer();
            CenterToolbarControls();

            // 启动后静默检查更新（一天至多一次，仅发现新版本时弹窗）
            if (DateTime.UtcNow - _lastUpdateCheck > UpdateCheckInterval)
                StartupUpdateCheck();

            // 跨屏拖拽（双屏不同 DPI）：按「基准尺寸 × 新DPI/96」绝对重算并覆写，
            // 框架自身的缩放结果会被这里的绝对值覆盖，不会叠加（比例连乘曾导致按钮被截断）。
            // BeginInvoke 推迟到框架自身的 DPI 缩放处理全部完成之后执行。
            DpiChanged += (_, e) =>
            {
                // 事件触发时窗口仍为旧 DPI 的物理尺寸； BeginInvoke 推迟到框架处理完成后执行
                Size physOld = Size;
                BeginInvoke(() => ApplyDpiLayout(e.DeviceDpiNew, e.DeviceDpiOld, physOld));
            };

            // 字号钳制兜底：实测框架在 WM_DPICHANGED 处理中（甚至在我们的 DpiChanged
            // 回调之后）会按错误的比率反复缩放字号 pt 值（10→5→2.5），导致文字忽大忽小。
            // pt 是绝对单位，任何 DPI 下都应恒为基准值，这里监听 FontChanged 强制钳回。
            // 我们自己的钳回会再次触发 FontChanged，但此时字号已等于基准，条件不成立，自然收敛。
            FontChanged += (_, _) =>
            {
                if (_baseFont != null && Math.Abs(Font.Size - _baseFont.Size) > 0.01f)
                    Font = new Font(_baseFont.FontFamily, _baseFont.Size, _baseFont.Style);
            };

#if DPI_TEST
            // 仅供 DPI 模拟测试（正式构建不含此代码）：
            // F9/F10 = 进程内发 WM_DPICHANGED（框架会忽略合成消息，仅作对照）；
            // F11 = 跳到副屏（96dpi），F12 = 跳回主屏（192dpi）——真实跨屏移动，
            // 系统会发真实 WM_DPICHANGED，与拖拽完全等价。
            KeyPreview = true;
            KeyDown += (_, ke) =>
            {
                Text = "KEY:" + ke.KeyCode;   // 测试反馈：确认按键到达
                if (ke.KeyCode == Keys.F11) Location = new Point(3500, 600);   // 副屏
                else if (ke.KeyCode == Keys.F12) Location = new Point(100, 100); // 主屏
                else if (ke.KeyCode is Keys.F9 or Keys.F10)
                {
                    int nd = ke.KeyCode == Keys.F9 ? 96 : 192;
                    if (nd == DeviceDpi) return;
                    float s = nd / (float)DeviceDpi;
                    var rect = new NativeRect
                    {
                        L = Left, T = Top,
                        R = Left + (int)Math.Round(Width * s),
                        B = Top + (int)Math.Round(Height * s),
                    };
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
                    Marshal.StructureToPtr(rect, ptr, false);
                    SendMessage(Handle, 0x02E1, (IntPtr)(nd | (nd << 16)), ptr);
                    Marshal.FreeHGlobal(ptr);
                }
            };
#endif
        };
    }
}
