using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaPortManager;

/// <summary>
/// 更新检测（GitHub Releases API）：按当前系统架构挑下载包，一天至多静默检查一次。
/// </summary>
public partial class MainForm
{
    private const string UpdateApiUrl = "https://api.github.com/repos/kite88/panda-port-manager/releases/latest";
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    private static DateTime _lastUpdateCheck = DateTime.MinValue;

    private static readonly HttpClient _http = CreateUpdateHttpClient();
    private static HttpClient CreateUpdateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PandaPortManager");
        c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    private sealed record UpdateInfo(bool HasUpdate, string? NewVersion, string? Url);

    private static async Task<UpdateInfo> FetchLatestReleaseAsync()
    {
        using var resp = await _http.GetAsync(UpdateApiUrl);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        string? tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        string? url = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

        // 优先挑出与当前系统架构匹配的下载包（win-x64 / win-x86 / win-arm64）
        bool matched = false;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64",
            };
            foreach (var a in assets.EnumerateArray())
            {
                if (a.TryGetProperty("name", out var n) && a.TryGetProperty("browser_download_url", out var d)
                    && (n.GetString() ?? "").Contains("win-" + arch, StringComparison.OrdinalIgnoreCase))
                {
                    url = d.GetString();
                    matched = true;
                    break;
                }
            }
        }

        bool has = !string.IsNullOrEmpty(tag);
        if (has)
        {
            var nv = tag!;
            if (nv.StartsWith("v", StringComparison.OrdinalIgnoreCase)) nv = nv.Substring(1);
            has = IsNewer(nv, VersionText);
        }
        // 没有匹配当前架构的包时不提示更新（例如只发了 x64，x86/arm64 用户不会看到更新）
        if (has && !matched)
            has = false;
        return new UpdateInfo(has, tag, url);
    }

    private static bool IsNewer(string candidate, string current)
    {
        int[] c = ParseVer(candidate), cur = ParseVer(current);
        for (int i = 0; i < 3; i++)
            if (c[i] != cur[i]) return c[i] > cur[i];
        return false;
    }

    private static int[] ParseVer(string s)
    {
        var parts = (s ?? "").Split('.');
        var v = new int[3];
        for (int i = 0; i < 3; i++)
        {
            var m = Regex.Match(parts.Length > i ? parts[i] : "", @"\d+");
            int.TryParse(m.Success ? m.Value : "0", out v[i]);
        }
        return v;
    }

    // 启动后静默检查：一天至多一次，仅在发现新版本时弹窗询问
    private async void StartupUpdateCheck()
    {
        try
        {
            var info = await FetchLatestReleaseAsync();
            _lastUpdateCheck = DateTime.UtcNow;
            SaveSettings();
            if (info.HasUpdate && !string.IsNullOrEmpty(info.Url)
                && MessageBox.Show(this, R.T("UpdateAvailable", info.NewVersion) + "\n\n" + R.T("UpdateGo") + "？",
                    R.T("Info"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                OpenUrl(info.Url);
        }
        catch
        {
            _lastUpdateCheck = DateTime.UtcNow;
            SaveSettings();
        }
    }
}
