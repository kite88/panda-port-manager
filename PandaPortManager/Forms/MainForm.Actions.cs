using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PandaPortManager;

/// <summary>
/// 用户操作：右键菜单、结束进程（单个/批量）、系统保留端口（winnat）释放与列排序。
/// </summary>
public partial class MainForm
{
    private void RowMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var hit = _list.HitTest(_list.PointToClient(Cursor.Position));
        ListViewItem? target = hit.Item;
        if (target == null && _list.SelectedItems.Count > 0)
            target = _list.SelectedItems[0];
        if (target == null)
        {
            e.Cancel = true;
            return;
        }

        // 右键项已在当前多选集合内则保留整组选择；否则仅选中这一行
        if (!_list.SelectedItems.Contains(target))
        {
            _list.SelectedItems.Clear();
            target.Selected = true;
        }

        _menuTarget = target;
        _endProcessMenuItem.Enabled = int.TryParse(target.SubItems[6].Text, out int pid) && pid > 0;
    }

    private void EndProcessMenu_Click(object? sender, EventArgs e)
    {
        if (_menuTarget == null) return;
        if (_list.SelectedItems.Count > 1 && _list.SelectedItems.Contains(_menuTarget))
            KillSelected();
        else
            KillSingle(_menuTarget);
    }

    private void KillSingle(ListViewItem item)
    {
        if (!int.TryParse(item.SubItems[6].Text, out int pid) || pid <= 0)
        {
            HandleReservedPort(item.SubItems[0].Text, int.Parse(item.SubItems[2].Text));
            return;
        }
        string procName = item.SubItems[8].Text;
        int port = int.Parse(item.SubItems[2].Text);

        var confirm = MessageBox.Show(R.T("ConfirmKill", procName, pid, port), R.T("ConfirmTitle"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        try
        {
            using var proc = Process.GetProcessById(pid);
            proc.Kill(entireProcessTree: true);
            _statusLabel.Text = R.T("Killed", procName, pid, port);
        }
        catch (Exception ex)
        {
            MessageBox.Show(R.T("KillFailed", ex.Message), R.T("Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        ScheduleRefreshAfterKill();
    }

    /// <summary>「结束进程」按钮的两种状态：有选中=醒目红底白字；无选中=灰底红字勾勒提示。</summary>
    private void UpdateKillButton()
    {
        bool hasSel = _list.SelectedItems.Count > 0;
        if (hasSel)
        {
            // 有选中：醒目的红色按钮
            _btnKill.Enabled = true;
            _btnKill.FlatAppearance.BorderSize = 0;
            _btnKill.BackColor = Color.FromArgb(192, 57, 43);
            _btnKill.ForeColor = Color.White;
            _btnKill.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xB63A2B);
            _btnKill.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x9E2E22);
        }
        else
        {
            // 常规状态：始终显示。灰底 + 主题灰边框；暗色下文字提亮（更白），亮色下用红色提示
            _btnKill.Enabled = true;
            _btnKill.FlatAppearance.BorderSize = 1;
            _btnKill.FlatAppearance.BorderColor = _isDark
                ? Color.FromArgb(0x55, 0x55, 0x5A)
                : Color.FromArgb(0xAD, 0xAD, 0xAD);
            _btnKill.BackColor = _isDark
                ? Color.FromArgb(0x3A, 0x3A, 0x3D)
                : Color.FromArgb(0xE2, 0xE2, 0xE2);
            // 灰态文字统一用红色（亮/暗色相同），仅背景区分灰/红两种状态
            _btnKill.ForeColor = Color.FromArgb(192, 57, 43);
            _btnKill.FlatAppearance.MouseOverBackColor = _isDark
                ? Color.FromArgb(0x45, 0x45, 0x49)
                : Color.FromArgb(0xD8, 0xD8, 0xD8);
            _btnKill.FlatAppearance.MouseDownBackColor = _isDark
                ? Color.FromArgb(0x40, 0x40, 0x44)
                : Color.FromArgb(0xCF, 0xCF, 0xCF);
        }
    }

    private void KillSelected()
    {
        if (_list.SelectedItems.Count == 0)
        {
            MessageBox.Show(R.T("SelectFirst"), R.T("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var targets = new List<(int pid, string name, int port)>();
        foreach (ListViewItem it in _list.SelectedItems)
        {
            if (int.TryParse(it.SubItems[6].Text, out int pid) && pid > 0)
                targets.Add((pid, it.SubItems[8].Text, int.Parse(it.SubItems[2].Text)));
        }

        if (targets.Count == 0)
        {
            var first = _list.SelectedItems[0];
            HandleReservedPort(first.SubItems[0].Text, int.Parse(first.SubItems[2].Text));
            return;
        }

        DialogResult confirm;
        if (targets.Count == 1)
        {
            var (pid, name, port) = targets[0];
            confirm = MessageBox.Show(R.T("ConfirmKill", name, pid, port), R.T("ConfirmTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        }
        else
        {
            confirm = MessageBox.Show(R.T("ConfirmKillMulti", targets.Count), R.T("ConfirmTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        }

        if (confirm != DialogResult.Yes) return;

        int ok = 0, fail = 0;
        string? lastErr = null;
        foreach (var (pid, name, port) in targets)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: true);
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                lastErr = ex.Message;
            }
        }

        if (targets.Count == 1)
            _statusLabel.Text = R.T("Killed", targets[0].name, targets[0].pid, targets[0].port);
        else
            _statusLabel.Text = R.T("KilledMulti", ok, fail);

        if (fail > 0 && lastErr != null)
            MessageBox.Show(R.T("KillFailed", lastErr), R.T("Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);

        ScheduleRefreshAfterKill();
    }

    /// <summary>强杀后稍等片刻再刷新（等系统释放端口）。</summary>
    private void ScheduleRefreshAfterKill()
    {
        var t = new System.Windows.Forms.Timer { Interval = 800 };
        t.Tick += (_, _) => { t.Stop(); t.Dispose(); RefreshPorts(); };
        t.Start();
    }

    /// <summary>处理无进程的保留端口：若位于 winnat 排除范围内，提供重启 winnat 释放的操作。</summary>
    private void HandleReservedPort(string protocol, int port)
    {
        if (!IsExcludedPort(protocol, port))
        {
            MessageBox.Show(R.T("NoProcess"), R.T("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(R.T("ReservedInRange", port) + "\n\n" + R.T("ReservedConfirm"),
            R.T("ReservedTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        try
        {
            RestartWinnat();
            _statusLabel.Text = R.T("ReservedDone", port);
        }
        catch (Exception ex)
        {
            MessageBox.Show(R.T("KillFailed", ex.Message), R.T("Failed"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        ScheduleRefreshAfterKill();
    }

    /// <summary>判断端口是否在 netsh 报告的系统排除（保留）端口范围内。</summary>
    private static bool IsExcludedPort(string protocol, int port)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh",
                $"interface ipv4 show excludedportrange protocol={protocol.ToLowerInvariant()}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            foreach (var raw in output.Split('\n'))
            {
                var m = Regex.Match(raw, @"^\s*(\d+)\s+(\d+)\s*$");
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, out int start)
                    && int.TryParse(m.Groups[2].Value, out int end)
                    && port >= start && port <= end)
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>重启 winnat 服务以刷新保留端口范围（需管理员权限）。</summary>
    private static void RestartWinnat()
    {
        foreach (var args in new[] { "stop winnat", "start winnat" })
        {
            var psi = new ProcessStartInfo("net.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"net {args}: {err.Trim()}");
        }
    }

    private void List_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortColumn == e.Column) _sortAscending = !_sortAscending;
        else { _sortColumn = e.Column; _sortAscending = true; }

        _list.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);
        _list.Sort();
    }
}
