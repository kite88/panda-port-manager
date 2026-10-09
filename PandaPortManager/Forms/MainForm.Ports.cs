using System.Diagnostics;
using System.Net.NetworkInformation;

namespace PandaPortManager;

/// <summary>
/// 端口数据扫描：连接/监听列表来自 IPGlobalProperties，只有 PID 需要解析一次 netstat -ano；
/// 另含双栈监听合并、首次发现时间维护与列表筛选。
/// </summary>
public partial class MainForm
{
    private sealed record PortEntry(string Protocol, string LocalAddr, int LocalPort,
        string RemoteAddr, int RemotePort, string State, int Pid)
    {
        public string Key => $"{Protocol}|{LocalAddr}|{LocalPort}|{RemoteAddr}|{RemotePort}|{State}";
    }

    private void RefreshPorts()
    {
        _btnRefresh.Enabled = false;
        _processNameCache.Clear();

        var entries = new List<PortEntry>();
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();

            foreach (var ep in props.GetActiveTcpListeners())
                entries.Add(new PortEntry("TCP", ep.Address.ToString(), ep.Port, "-", 0, "LISTENING", 0));

            foreach (var c in props.GetActiveTcpConnections())
                entries.Add(new PortEntry("TCP", c.LocalEndPoint.Address.ToString(), c.LocalEndPoint.Port,
                    c.RemoteEndPoint.Address.ToString(), c.RemoteEndPoint.Port,
                    c.State.ToString().ToUpperInvariant(), 0));

            foreach (var ep in props.GetActiveUdpListeners())
                entries.Add(new PortEntry("UDP", ep.Address.ToString(), ep.Port, "-", 0, "LISTENING", 0));

            // netstat -ano 拿 PID（IPGlobalProperties 不返回 PID）
            MapPidsFromNetstat(entries);

            // 去重（同一连接可能在 listener + connection 中出现）
            entries = entries
                .GroupBy(e => e.Key)
                .Select(g => g.First())
                .OrderBy(e => e.Protocol)
                .ThenBy(e => e.LocalPort)
                .ToList();

            // 合并双栈监听：同一协议/端口/PID 的 IPv4 与 IPv6 监听合并为一行
            entries = MergeDualStackListeners(entries);

            // 记录/更新首次发现时间，并清理已消失的连接
            var seen = new HashSet<string>();
            var now = DateTime.Now;
            foreach (var e in entries)
            {
                seen.Add(e.Key);
                if (!_firstSeen.ContainsKey(e.Key))
                    _firstSeen[e.Key] = now;
            }
            foreach (var key in _firstSeen.Keys.ToList())
                if (!seen.Contains(key))
                    _firstSeen.Remove(key);
        }
        catch (Exception ex)
        {
            MessageBox.Show(R.T("ScanFailed", ex.Message), R.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // 记住选中项
        string? selectedKey = _list.SelectedItems.Count > 0
            ? string.Join("|", _list.SelectedItems[0].SubCells())
            : null;

        _list.BeginUpdate();
        _list.Items.Clear();
        _allItems.Clear();
        foreach (var e in entries)
        {
            var procName = e.Pid > 0 ? GetProcessName(e.Pid) : R.T("SystemReserved");
            var item = new ListViewItem(new[]
            {
                e.Protocol,
                e.LocalAddr,
                e.LocalPort.ToString(),
                e.RemoteAddr,
                e.RemotePort == 0 ? "-" : e.RemotePort.ToString(),
                e.State,
                e.Pid.ToString(),
                _firstSeen.TryGetValue(e.Key, out var seenAt) ? seenAt.ToString("MM-dd HH:mm:ss") : "",
                procName,
            });
            item.Tag = e.Pid;
            _allItems.Add(item);
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        ApplyFilter();
        _list.Sort();

        // 恢复选中
        if (selectedKey != null)
        {
            foreach (ListViewItem it in _list.Items)
            {
                if (string.Join("|", it.SubCells()) == selectedKey)
                {
                    it.Selected = true;
                    it.EnsureVisible();
                    break;
                }
            }
        }

        _statusLabel.Text = R.T("StatusSummary", _list.Items.Count,
            entries.Count(e => e.Protocol == "TCP"), entries.Count(e => e.Protocol == "UDP"),
            DateTime.Now.ToString("HH:mm:ss"));
        _btnRefresh.Enabled = true;
        UpdateKillButton();
    }

    /// <summary>合并同一进程在相同端口上的 IPv4/IPv6 双栈监听，减少重复行。</summary>
    private List<PortEntry> MergeDualStackListeners(List<PortEntry> entries)
    {
        var groups = entries
            .Where(e => e.State == "LISTENING" && e.RemotePort == 0)
            .GroupBy(e => (e.Protocol, e.LocalPort, e.Pid))
            .Where(g => g.Count() > 1)
            .ToList();
        if (groups.Count == 0) return entries;

        var mergedByOrigin = new Dictionary<PortEntry, PortEntry>();
        foreach (var g in groups)
        {
            var rep = g.First();
            var addrs = string.Join(" / ", g.Select(x => x.LocalAddr).Distinct());
            var merged = rep with { LocalAddr = addrs };

            // 继承组内最早的首次发现时间，避免刷新后时间被重置
            DateTime? earliest = null;
            foreach (var x in g)
                if (_firstSeen.TryGetValue(x.Key, out var t) && (earliest == null || t < earliest))
                    earliest = t;
            if (earliest != null) _firstSeen[merged.Key] = earliest.Value;

            foreach (var x in g) mergedByOrigin[x] = merged;
        }

        var result = new List<PortEntry>();
        foreach (var e in entries)
        {
            if (mergedByOrigin.TryGetValue(e, out var m))
            {
                if (!result.Contains(m)) result.Add(m);
            }
            else result.Add(e);
        }
        return result;
    }

    /// <summary>解析 netstat -ano 输出，将 PID 映射回端口条目。</summary>
    private void MapPidsFromNetstat(List<PortEntry> entries)
    {
        var psi = new ProcessStartInfo("netstat", "-ano")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        if (p == null) return;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);

        // key: "proto|localAddr|localPort|remotePort|state" -> pid
        var map = new Dictionary<string, int>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            if (!TryParseProto(parts[0], out var proto)) continue;
            if (!TryParseEndPoint(parts[1], out var lAddr, out var lPort)) continue;
            if (!TryParseEndPoint(parts[2], out _, out var rPort)) continue;

            string state;
            int pid;
            if (proto == "UDP")
            {
                state = "LISTENING";
                if (!int.TryParse(parts[3], out pid)) continue;
            }
            else
            {
                state = parts[3].ToUpperInvariant();
                if (state == "TIME") state = "TIMEWAIT"; // 旧版 netstat 输出 "TIME WAIT" 带空格
                if (!int.TryParse(parts[^1], out pid)) continue;
            }

            map[$"{proto}|{lAddr}|{lPort}|{rPort}|{state}"] = pid;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (map.TryGetValue($"{e.Protocol}|{e.LocalAddr}|{e.LocalPort}|{e.RemotePort}|{e.State}", out var pid))
            {
                entries[i] = e with { Pid = pid };
            }
        }
    }

    /// <summary>解析 "1.2.3.4:80" 或 "[::]:135" 形式的端点，返回规范化的地址（去方括号）与端口。</summary>
    private static bool TryParseEndPoint(string s, out string addr, out int port)
    {
        addr = ""; port = 0;

        if (string.IsNullOrWhiteSpace(s))
            return false;

        if (s == "*" || s == "*:*")
        {
            addr = "*";
            port = 0;
            return true;
        }

        if (s.StartsWith("["))
        {
            int close = s.IndexOf(']');
            if (close < 0) return false;
            addr = s[1..close];

            if (close + 1 >= s.Length)
            {
                port = 0;
                return true;
            }

            if (s[close + 1] != ':') return false;
            if (close + 2 == s.Length)
            {
                port = 0;
                return true;
            }

            if (s[(close + 2)..] == "*")
            {
                port = 0;
                return true;
            }

            return int.TryParse(s[(close + 2)..], out port);
        }

        int idx = s.LastIndexOf(':');
        if (idx < 0) return false;

        addr = s[..idx];
        if (addr == "*" || s[(idx + 1)..] == "*")
        {
            port = 0;
            return true;
        }

        return int.TryParse(s[(idx + 1)..], out port);
    }

    private static bool TryParseProto(string s, out string proto)
    {
        switch (s.ToUpperInvariant())
        {
            case "TCP": proto = "TCP"; return true;
            case "UDP": proto = "UDP"; return true;
            default: proto = ""; return false;
        }
    }

    private string GetProcessName(int pid)
    {
        if (_processNameCache.TryGetValue(pid, out var name)) return name;
        try
        {
            using var proc = Process.GetProcessById(pid);
            name = proc.ProcessName + ".exe";
        }
        catch
        {
            name = R.T("Exited");
        }
        _processNameCache[pid] = name;
        return name;
    }

    /// <summary>按筛选框内容过滤 _allItems（ListViewItem 无法隐藏，只能重建列表项）。</summary>
    private void ApplyFilter()
    {
        string q = _filter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var it in _allItems)
        {
            if (q.Length == 0 ||
                string.Join("|", it.SubCells()).Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                _list.Items.Add(it);
            }
        }
        _list.EndUpdate();
    }
}
