# 枚举每台显示器的物理分辨率与 DPI（C#5 兼容）
$ErrorActionPreference = 'Stop'
$src = @"
using System;
using System.Runtime.InteropServices;
public class M {
  public delegate bool MonitorEnumProc(IntPtr hmon, IntPtr hdc, ref RECT rect, IntPtr data);
  [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX mi);
  [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dx, out uint dy);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
  public struct MONITORINFOEX {
    public int cbSize;
    public RECT rcMonitor; public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string szDevice;
  }
  public static bool Cb(IntPtr hmon, IntPtr hdc, ref RECT rect, IntPtr data) {
    MONITORINFOEX mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
    GetMonitorInfo(hmon, ref mi);
    uint dx, dy;
    GetDpiForMonitor(hmon, 0, out dx, out dy);
    Console.WriteLine(mi.szDevice + " rect=" + rect.L + "," + rect.T + "," + rect.R + "," + rect.B + " dpi=" + dx);
    return true;
  }
  public static void Dump() {
    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, new MonitorEnumProc(Cb), IntPtr.Zero);
  }
}
"@
Add-Type -TypeDefinition $src
[M]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
[M]::Dump()
