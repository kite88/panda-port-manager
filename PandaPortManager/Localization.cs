using System.Globalization;

namespace PandaPortManager;

internal enum Language { ZhCn, ZhTw, En, System }

/// <summary>界面文案表（简体中文 / 繁體中文 / English），按 key 取词并支持 {0} 占位。</summary>
internal static class R
{
    private static Language _lang = Language.ZhCn;

    public static Language Lang
    {
        get => _lang;
        set => _lang = value;
    }

    private static readonly Dictionary<string, (string zh, string tw, string en)> _map = new()
    {
        ["AppTitle"] = ("Panda Port Manager", "Panda Port Manager", "Panda Port Manager"),
        ["Filter"] = ("筛选:", "篩選:", "Filter:"),
        ["FilterPlaceholder"] = ("端口 / 进程名 / IP / 状态", "連接埠 / 處理程序 / IP / 狀態", "Port / Process / IP / Status"),
        ["Refresh"] = ("刷新", "重新整理", "Refresh"),
        ["Kill"] = ("结束进程", "結束處理程序", "End Process"),
        ["AutoRefresh"] = ("自动刷新 (3秒)", "自動重新整理 (3秒)", "Auto Refresh (3s)"),
        ["Settings"] = ("设置", "設定", "Settings"),
        ["Theme"] = ("主题", "主題", "Theme"),
        ["ThemeLight"] = ("亮色", "亮色", "Light"),
        ["ThemeDark"] = ("暗色", "暗色", "Dark"),
        ["ThemeSystem"] = ("跟随系统", "跟隨系統", "System"),
        ["Language"] = ("语言", "語言", "Language"),
        ["LangZh"] = ("简体中文", "简体中文", "简体中文"),
        ["LangTw"] = ("繁體中文", "繁體中文", "繁體中文"),
        ["LangEn"] = ("English", "English", "English"),
        ["LangSystem"] = ("跟随系统", "跟隨系統", "System"),
        ["About"] = ("关于", "關於", "About"),
        ["Version"] = ("版本", "版本", "Version"),
        ["Website"] = ("网址", "網址", "Website"),
        ["GitHub"] = ("GitHub", "GitHub", "GitHub"),
        ["Copyright"] = ("Copyright © 2026 kite88", "Copyright © 2026 kite88", "Copyright © 2026 kite88"),
        ["CheckUpdate"] = ("检查更新", "檢查更新", "Check for Updates"),
        ["UpdateChecking"] = ("正在检查更新…", "正在檢查更新…", "Checking for updates…"),
        ["UpdateAvailable"] = ("发现新版本 {0}", "發現新版本 {0}", "A new version {0} is available"),
        ["UpdateNone"] = ("已是最新版本。", "已是最新版本。", "You are on the latest version."),
        ["UpdateFailed"] = ("检查更新失败，请稍后重试。", "檢查更新失敗，請稍後重試。", "Failed to check for updates. Please try later."),
        ["UpdateGo"] = ("前往下载", "前往下載", "Go to download"),
        ["Ready"] = ("就绪", "就緒", "Ready"),
        ["ScanFailed"] = ("扫描端口失败: {0}", "掃描連接埠失敗: {0}", "Failed to scan ports: {0}"),
        ["SelectFirst"] = ("请先在列表中选择一条端口记录。", "請先在清單中選取一筆連接埠記錄。", "Please select a port record first."),
        ["NoProcess"] = ("该记录没有关联的进程（系统保留端口），无法结束。", "此記錄沒有關聯的處理程序（系統保留連接埠），無法結束。", "This record has no associated process (system reserved port) and cannot be ended."),
        ["ReservedInRange"] = ("端口 {0} 位于系统保留范围（winnat，通常由 Hyper-V / WSL / Docker 保留），因此没有可结束的进程。", "連接埠 {0} 位於系統保留範圍（winnat，通常由 Hyper-V / WSL / Docker 保留），因此沒有可結束的處理程序。", "Port {0} is inside a system reserved range (winnat, usually reserved by Hyper-V / WSL / Docker), so there is no process to end."),
        ["ReservedConfirm"] = ("是否重启 winnat 服务以尝试释放保留端口？\n\n注意：期间 WSL / Docker / NAT 网络可能短暂中断。", "是否重新啟動 winnat 服務以嘗試釋放保留連接埠？\n\n注意：期間 WSL / Docker / NAT 網路可能短暫中斷。", "Restart the winnat service to try releasing the reserved port?\n\nNote: WSL / Docker / NAT networking may be briefly interrupted."),
        ["ReservedTitle"] = ("释放保留端口", "釋放保留連接埠", "Release Reserved Port"),
        ["ReservedDone"] = ("已重启 winnat 服务，请刷新查看端口 {0} 是否已释放。", "已重新啟動 winnat 服務，請重新整理以確認連接埠 {0} 是否已釋放。", "winnat restarted; refresh to check whether port {0} is released."),
        ["ConfirmKill"] = ("确定要结束进程 {0} (PID: {1}) 吗？\n\n端口 {2} 将随之释放。", "確定要結束處理程序 {0} (PID: {1}) 嗎？\n\n連接埠 {2} 將隨之釋放。", "End process {0} (PID: {1})?\n\nPort {2} will be released."),
        ["Killed"] = ("已结束进程 {0} (PID: {1})，端口 {2} 已释放。", "已結束處理程序 {0} (PID: {1})，連接埠 {2} 已釋放。", "Ended process {0} (PID: {1}); port {2} released."),
        ["ConfirmKillMulti"] = ("确定要结束选中的 {0} 个进程吗？\n\n相关端口将随之释放。", "確定要結束選取的 {0} 個處理程序嗎？\n\n相關連接埠將隨之釋放。", "End the {0} selected processes?\n\nTheir ports will be released."),
        ["KilledMulti"] = ("已结束 {0} 个进程（{1} 个失败）。", "已結束 {0} 個處理程序（{1} 個失敗）。", "Ended {0} processes ({1} failed)."),
        ["KillFailed"] = ("结束进程失败：{0}\n\n（系统关键进程受保护，无法结束）", "結束處理程序失敗：{0}\n\n（系統關鍵處理程序受保護，無法結束）", "Failed to end process: {0}\n\n(Critical system processes are protected and cannot be ended.)"),
        ["Error"] = ("错误", "錯誤", "Error"),
        ["Info"] = ("提示", "提示", "Info"),
        ["ConfirmTitle"] = ("确认结束进程", "確認結束處理程序", "Confirm End Process"),
        ["Failed"] = ("失败", "失敗", "Failed"),
        ["Close"] = ("关闭", "關閉", "Close"),
        ["SystemReserved"] = ("系统保留", "系統保留", "System Reserved"),
        ["Exited"] = ("(已退出)", "(已退出)", "(Exited)"),
        ["ColProto"] = ("协议", "通訊協定", "Proto"),
        ["ColLocalAddr"] = ("本地地址", "本地位址", "Local Address"),
        ["ColLocalPort"] = ("本地端口", "本地連接埠", "Local Port"),
        ["ColRemoteAddr"] = ("远程地址", "遠端位址", "Remote Address"),
        ["ColRemotePort"] = ("远程端口", "遠端連接埠", "Remote Port"),
        ["ColState"] = ("状态", "狀態", "State"),
        ["ColPid"] = ("PID", "PID", "PID"),
        ["ColProcess"] = ("进程名", "處理程序名稱", "Process Name"),
        ["ColTime"] = ("时间", "時間", "Time"),
        ["StatusSummary"] = ("共 {0} 条记录  |  TCP {1}  UDP {2}  |  更新于 {3}", "共 {0} 筆記錄  |  TCP {1}  UDP {2}  |  更新於 {3}", "Total {0}  |  TCP {1}  UDP {2}  |  Updated {3}"),
    };

    // 跟随系统时的语言判定：繁体地区（台湾/香港/澳门/繁体）→ 繁體中文，其余中文 → 简体，非中文 → 英文
    private static Language SystemLanguage()
    {
        string name = CultureInfo.CurrentUICulture.Name;
        if (!name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Language.En;
        bool traditional = name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase);
        return traditional ? Language.ZhTw : Language.ZhCn;
    }

    private static Language EffectiveLang => _lang == Language.System ? SystemLanguage() : _lang;

    public static string T(string key, params object[] args)
    {
        if (!_map.TryGetValue(key, out var pair)) return key;
        string s = EffectiveLang switch
        {
            Language.ZhCn => pair.zh,
            Language.ZhTw => pair.tw,
            _ => pair.en,
        };
        return args.Length > 0 ? string.Format(s, args) : s;
    }
}
