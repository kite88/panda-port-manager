using System.Diagnostics;

namespace PandaPortManager;

/// <summary>
/// 弹窗：设置（主题 / 语言）与关于（版本号、官网 / GitHub、更新检测）。
/// 两者都是纯代码构建，字号用「像素」单位并按目标屏 DPI 绝对重设，
/// 以规避混合 DPI 进程里 pt→px 的 1.5 倍换算。
/// </summary>
public partial class MainForm
{
    // ============ 设置弹窗 ============

    private void ShowSettingsDialog()
    {
        Color back = _isDark ? DarkFormBack : SystemColors.Window;
        Color fore = _isDark ? DarkFore : LightFore;

        var f = new ThemedForm
        {
            DarkTitle = _isDark,
            Text = R.T("Settings"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowIcon = false,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = back,
            ForeColor = fore,
            ClientSize = new Size(360, 346),
        };

        var grpTheme = new GroupBox
        {
            Text = R.T("Theme"),
            Location = new Point(16, 14),
            Size = new Size(328, 116),
            ForeColor = fore,
            BackColor = Color.Transparent,
        };
        var rbLight = new RadioButton { Text = R.T("ThemeLight"), Location = new Point(16, 26), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        var rbDark = new RadioButton { Text = R.T("ThemeDark"), Location = new Point(16, 54), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        var rbSys = new RadioButton { Text = R.T("ThemeSystem"), Location = new Point(16, 82), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        grpTheme.Controls.AddRange(new Control[] { rbLight, rbDark, rbSys });

        var grpLang = new GroupBox
        {
            Text = R.T("Language"),
            Location = new Point(16, 140),
            Size = new Size(328, 146),
            ForeColor = fore,
            BackColor = Color.Transparent,
        };
        var rbZh = new RadioButton { Text = R.T("LangZh"), Location = new Point(16, 26), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        var rbTw = new RadioButton { Text = R.T("LangTw"), Location = new Point(16, 54), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        var rbEn = new RadioButton { Text = R.T("LangEn"), Location = new Point(16, 82), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        var rbSysLang = new RadioButton { Text = R.T("LangSystem"), Location = new Point(16, 110), AutoSize = true, ForeColor = fore, BackColor = Color.Transparent };
        grpLang.Controls.AddRange(new Control[] { rbZh, rbTw, rbEn, rbSysLang });

        // 弹窗字体统一用「像素」单位（12px × 目标屏缩放）：混合 DPI 进程里 pt 会被按 1.5 倍换算导致偏大
        float DlgScale() => (f.IsHandleCreated ? f.DeviceDpi : DeviceDpi) / 96f;
        Font BodyFont() => new Font(Font.FontFamily, 12f * DlgScale(), FontStyle.Regular, GraphicsUnit.Pixel);
        var dlgFont = BodyFont();
        var okFont = dlgFont;
        var ok = new TextButton
        {
            Text = R.T("Close"),
            Font = okFont,
            DialogResult = DialogResult.OK,
            Location = new Point(254, 300),
            Width = 72,
            // 高度用完整 FitHeight（含余量）：此前去 3×余量的负余量在 DPI 缩放取整后会裁字
            Height = TextButton.FitHeight(R.T("Close"), okFont),
        };
        StyleFlatButton(ok, _isDark);

        // 分组框/单选钮统一使用同一像素字号（与按钮一致）
        f.Font = dlgFont;
        foreach (var c in new Control[] { grpTheme, grpLang, rbLight, rbDark, rbSys, rbZh, rbTw, rbEn, rbSysLang })
            c.Font = dlgFont;

        rbLight.Checked = _themeMode == ThemeMode.Light;
        rbDark.Checked = _themeMode == ThemeMode.Dark;
        rbSys.Checked = _themeMode == ThemeMode.System;
        rbZh.Checked = R.Lang == Language.ZhCn;
        rbTw.Checked = R.Lang == Language.ZhTw;
        rbEn.Checked = R.Lang == Language.En;
        rbSysLang.Checked = R.Lang == Language.System;

        void Recolor()
        {
            Color b2 = _isDark ? DarkFormBack : SystemColors.Window;
            Color t2 = _isDark ? DarkFore : LightFore;
            f.BackColor = b2;
            f.ForeColor = t2;
            grpTheme.ForeColor = t2;
            grpLang.ForeColor = t2;
            foreach (var rb in new[] { rbLight, rbDark, rbSys, rbZh, rbTw, rbEn, rbSysLang })
                rb.ForeColor = t2;
            StyleFlatButton(ok, _isDark);
            f.DarkTitle = _isDark;
            f.ApplyDarkTitle();
        }

        // 切换语言时同步刷新弹窗自身文案（标题、分组框、选项、关闭按钮）
        void Retext()
        {
            f.Text = R.T("Settings");
            grpTheme.Text = R.T("Theme");
            grpLang.Text = R.T("Language");
            rbLight.Text = R.T("ThemeLight");
            rbDark.Text = R.T("ThemeDark");
            rbSys.Text = R.T("ThemeSystem");
            rbZh.Text = R.T("LangZh");
            rbTw.Text = R.T("LangTw");
            rbEn.Text = R.T("LangEn");
            rbSysLang.Text = R.T("LangSystem");
            ok.Text = R.T("Close");
        }

        rbLight.CheckedChanged += (_, _) => { if (rbLight.Checked) { SetTheme(ThemeMode.Light); Recolor(); } };
        rbDark.CheckedChanged += (_, _) => { if (rbDark.Checked) { SetTheme(ThemeMode.Dark); Recolor(); } };
        rbSys.CheckedChanged += (_, _) => { if (rbSys.Checked) { SetTheme(ThemeMode.System); Recolor(); } };
        rbZh.CheckedChanged += (_, _) => { if (rbZh.Checked) { SetLanguage(Language.ZhCn); Retext(); } };
        rbTw.CheckedChanged += (_, _) => { if (rbTw.Checked) { SetLanguage(Language.ZhTw); Retext(); } };
        rbEn.CheckedChanged += (_, _) => { if (rbEn.Checked) { SetLanguage(Language.En); Retext(); } };
        rbSysLang.CheckedChanged += (_, _) => { if (rbSysLang.Checked) { SetLanguage(Language.System); Retext(); } };

        f.Controls.AddRange(new Control[] { grpTheme, grpLang, ok });
        // 底部留白收紧：高度贴内容（语言分组底部 + 间距 + 按钮高度 + 下边距），不留大片空带
        f.ClientSize = new Size(360, grpLang.Bottom + 8 + ok.Height + 12);
        ScaleDialogToDpi(f);
        // 两个分组框横向撑满弹窗（左右边距强制对称），并随窗体拉伸，避免右侧出现空带
        int m = (int)Math.Round(16 * DeviceDpi / 96f);
        grpTheme.Left = grpLang.Left = m;
        grpTheme.Width = grpLang.Width = f.ClientSize.Width - 2 * m;
        grpTheme.Anchor = grpLang.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        PinDialogButtonToBottomRight(f, ok, 12);

        // 字号绝对重设：显示后按目标屏 DPI 重设像素字号，覆盖框架在句柄创建/跨屏时的缩放
        void ApplyDialogFonts()
        {
            var body = BodyFont();
            f.Font = body;
            foreach (var c in new Control[] { grpTheme, grpLang, rbLight, rbDark, rbSys, rbZh, rbTw, rbEn, rbSysLang })
                c.Font = body;
            ok.Font = body;
            ok.Height = TextButton.FitHeight(R.T("Close"), body);
            PinDialogButtonToBottomRight(f, ok, (int)Math.Round(12 * DlgScale()));
        }
        f.Shown += (_, _) => BeginInvoke(ApplyDialogFonts);
        f.DpiChanged += (_, _) => BeginInvoke(ApplyDialogFonts);

        f.ShowDialog(this);
    }

    // ============ 关于弹窗 ============

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void ShowAbout()
    {
        Color back = _isDark ? DarkFormBack : SystemColors.Window;
        Color fore = _isDark ? DarkFore : LightFore;

        var f = new ThemedForm
        {
            DarkTitle = _isDark,
            Text = R.T("About"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = back,
            ForeColor = fore,
            ClientSize = new Size(500, 296),
        };

        // 弹窗字体统一用「像素」单位：混合 DPI 进程里 pt 会被按 144/96=1.5 倍换算，导致弹窗文字偏大；
        // 像素单位不参与 pt→px 换算，尺寸完全确定。显示后再按目标屏 DPI 绝对重设一次，覆盖框架的缩放。
        float DlgScale() => (f.IsHandleCreated ? f.DeviceDpi : DeviceDpi) / 96f;
        Font TitleFont() => new Font(Font.FontFamily, 15f * DlgScale(), FontStyle.Bold, GraphicsUnit.Pixel);
        Font BodyFont() => new Font(Font.FontFamily, 12f * DlgScale(), FontStyle.Regular, GraphicsUnit.Pixel);

        var title = new Label
        {
            Text = R.T("AppTitle"),
            Location = new Point(20, 18),
            AutoSize = true,
            Font = TitleFont(),
        };
        var ver = new Label
        {
            Text = R.T("Version") + " v" + VersionText,
            Location = new Point(20, 52),
            AutoSize = true,
        };

        const string webUrl = "https://developer.youbimo.com/kite88/projects/tool/panda-port-manager";
        var webLabel = new Label { Text = R.T("Website"), Location = new Point(20, 84), AutoSize = true };
        var webLink = new LinkLabel
        {
            Text = "developer.youbimo.com/kite88/projects/tool/panda-port-manager",
            Location = new Point(36, 108),
            AutoSize = true,
        };
        webLink.Links.Add(0, webLink.Text.Length, webUrl);
        webLink.LinkClicked += (_, _) => OpenUrl(webUrl);

        const string ghUrl = "https://github.com/kite88/panda-port-manager";
        var ghLabel = new Label { Text = R.T("GitHub"), Location = new Point(20, 138), AutoSize = true };
        var ghLink = new LinkLabel
        {
            Text = "github.com/kite88/panda-port-manager",
            Location = new Point(36, 162),
            AutoSize = true,
        };
        ghLink.Links.Add(0, ghLink.Text.Length, ghUrl);
        ghLink.LinkClicked += (_, _) => OpenUrl(ghUrl);

        var copy = new Label
        {
            Text = R.T("Copyright"),
            Location = new Point(20, 200),
            AutoSize = true,
            ForeColor = _isDark ? SystemColors.GrayText : LightFore,
        };

        // 更新检测状态（发现新版本时整行可点击前往下载）
        var updateLabel = new LinkLabel
        {
            Location = new Point(20, 228),
            AutoSize = true,
            Visible = false,
            LinkColor = _isDark ? Color.FromArgb(0x4DA6FF) : SystemColors.HotTrack,
            ActiveLinkColor = _isDark ? Color.White : SystemColors.HotTrack,
            VisitedLinkColor = _isDark ? Color.FromArgb(0x4DA6FF) : SystemColors.HotTrack,
        };
        updateLabel.LinkClicked += (_, e) => { if (e.Link.LinkData is string u) OpenUrl(u); };

        if (_isDark)
        {
            webLink.LinkColor = Color.FromArgb(0x4DA6FF);
            webLink.ActiveLinkColor = Color.White;
            webLink.VisitedLinkColor = webLink.LinkColor;
            ghLink.LinkColor = webLink.LinkColor;
            ghLink.ActiveLinkColor = Color.White;
            ghLink.VisitedLinkColor = webLink.LinkColor;
        }

        // 正文与按钮统一用同一像素字号（正文 12px），任何 DPI 下与按钮完全一致
        var okFont = BodyFont();
        foreach (var c in new Control[] { ver, webLabel, webLink, ghLabel, ghLink, copy, updateLabel })
            c.Font = okFont;

        var ok = new TextButton
        {
            Text = R.T("Close"),
            Font = okFont,
            DialogResult = DialogResult.OK,
            Location = new Point(400, 256),
            Width = 64,
            // 高度用完整 FitHeight（含余量）：负余量在 DPI 缩放取整后会裁字
            Height = TextButton.FitHeight(R.T("Close"), okFont),
        };
        StyleFlatButton(ok, _isDark);

        var btnCheck = new TextButton
        {
            Text = R.T("CheckUpdate"),
            Font = okFont,
            Location = new Point(300, 256),
            Width = 96,
            Height = TextButton.FitHeight(R.T("CheckUpdate"), okFont),
        };
        StyleFlatButton(btnCheck, _isDark);

        f.Controls.AddRange(new Control[] { title, ver, webLabel, webLink, ghLabel, ghLink, copy, updateLabel, ok, btnCheck });
        ScaleDialogToDpi(f);
        PinDialogButtonToBottomRight(f, ok, 12);
        // 「检查更新」按钮紧贴关闭按钮左侧；与关闭按钮同锚点（右下），
        // 跨屏 DPI 缩放改变客户区时两者同步移动，保持同行对齐
        btnCheck.Location = new Point(ok.Left - btnCheck.Width - (int)Math.Round(12 * DeviceDpi / 96f), ok.Top);
        btnCheck.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        // 字号绝对重设：像素字体不参与 pt 换算，显示后（框架缩放已全部完成）按目标屏 DPI 重设一次，
        // 跨屏拖动时同样重设，保证任何情况下字号都等于「像素设计值 × 目标屏缩放」。
        void ApplyDialogFonts()
        {
            float scale = DlgScale();
            title.Font = TitleFont();
            var body = BodyFont();
            foreach (var c in new Control[] { ver, webLabel, webLink, ghLabel, ghLink, copy, updateLabel })
                c.Font = body;
            ok.Font = body;
            ok.Height = TextButton.FitHeight(R.T("Close"), body);
            btnCheck.Font = body;
            btnCheck.Height = TextButton.FitHeight(R.T("CheckUpdate"), body);
            PinDialogButtonToBottomRight(f, ok, (int)Math.Round(12 * scale));
            btnCheck.Location = new Point(ok.Left - btnCheck.Width - (int)Math.Round(12 * scale), ok.Top);
        }
        f.Shown += (_, _) => BeginInvoke(ApplyDialogFonts);
        f.DpiChanged += (_, _) => BeginInvoke(ApplyDialogFonts);

        // 弹窗打开即查一次更新；也可手动点击「检查更新」按钮
        async void DoCheck()
        {
            btnCheck.Enabled = false;
            updateLabel.Visible = true;
            updateLabel.Links.Clear();
            updateLabel.Text = R.T("UpdateChecking");
            try
            {
                var info = await FetchLatestReleaseAsync();
                _lastUpdateCheck = DateTime.UtcNow;
                SaveSettings();
                if (info.HasUpdate && !string.IsNullOrEmpty(info.Url))
                {
                    updateLabel.Text = R.T("UpdateAvailable", info.NewVersion);
                    updateLabel.Links.Add(0, updateLabel.Text.Length, info.Url);
                }
                else
                {
                    updateLabel.Text = R.T("UpdateNone");
                }
            }
            catch
            {
                _lastUpdateCheck = DateTime.UtcNow;
                SaveSettings();
                updateLabel.Text = R.T("UpdateFailed");
            }
            finally
            {
                btnCheck.Enabled = true;
            }
        }
        btnCheck.Click += (_, _) => DoCheck();
        DoCheck();

        f.ShowDialog(this);
    }
}
