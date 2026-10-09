using System.Globalization;
using System.Text.Json;

namespace PandaPortManager;

/// <summary>
/// 偏好设置：主题/语言/上次更新时间的读写（%LOCALAPPDATA%\PandaPortManager\settings.json），
/// 以及主题、语言切换后对界面文案的应用。
/// </summary>
public partial class MainForm
{
    /// <summary>产品版本号（去掉 SDK 自动附加的 git 提交哈希，如 1.0.0+a1b2c3...）。</summary>
    private static string VersionText
    {
        get
        {
            string v = Application.ProductVersion;
            int plus = v.IndexOf('+');
            return plus > 0 ? v[..plus] : v;
        }
    }

    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PandaPortManager", "settings.json");

    private static (ThemeMode theme, Language lang) LoadSettings()
    {
        var theme = ThemeMode.System;
        var lang = Language.System;
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("theme", out var t) && Enum.TryParse<ThemeMode>(t.GetString(), out var tm))
                    theme = tm;
                if (root.TryGetProperty("language", out var l) && Enum.TryParse<Language>(l.GetString(), out var lm))
                    lang = lm;
                if (root.TryGetProperty("lastUpdateCheck", out var lu)
                    && DateTime.TryParse(lu.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var luDt))
                    _lastUpdateCheck = luDt;
            }
        }
        catch { }
        return (theme, lang);
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = "{\"theme\":\"" + _themeMode + "\",\"language\":\"" + R.Lang + "\"";
            if (_lastUpdateCheck != DateTime.MinValue)
                json += ",\"lastUpdateCheck\":\"" + _lastUpdateCheck.ToString("o") + "\"";
            json += "}";
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private void SetTheme(ThemeMode mode)
    {
        _themeMode = mode;
        SaveSettings();
        ApplyTheme();
    }

    private void SetLanguage(Language lang)
    {
        if (R.Lang == lang) return;
        R.Lang = lang;
        SaveSettings();
        ApplyLanguage();
        RefreshPorts();
    }

    // ============ 多语言 ============

    private void ApplyLanguage()
    {
        Text = R.T("AppTitle");
        _filterLabel.Text = R.T("Filter");
        _filter.PlaceholderText = R.T("FilterPlaceholder");
        _btnRefresh.Text = R.T("Refresh");
        _btnKill.Text = R.T("Kill");
        _autoRefresh.Text = R.T("AutoRefresh");

        _list.Columns[0].Text = R.T("ColProto");
        _list.Columns[1].Text = R.T("ColLocalAddr");
        _list.Columns[2].Text = R.T("ColLocalPort");
        _list.Columns[3].Text = R.T("ColRemoteAddr");
        _list.Columns[4].Text = R.T("ColRemotePort");
        _list.Columns[5].Text = R.T("ColState");
        _list.Columns[6].Text = R.T("ColPid");
        _list.Columns[7].Text = R.T("ColTime");
        _list.Columns[8].Text = R.T("ColProcess");
        _endProcessMenuItem.Text = R.T("Kill");

        // 图标按钮的文案在 Paint 里取词，语言切换后需重绘
        _settingsBtn.Invalidate();
        _aboutBtn.Invalidate();

        _statusLabel.Text = R.T("Ready");
        _filterHint.Text = R.T("FilterPlaceholder");

        // 文字变化可能改变控件尺寸，重新按垂直中心线对齐工具栏
        CenterToolbarControls();
    }
}
