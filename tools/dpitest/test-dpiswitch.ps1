# 用 F12(跳主屏192dpi)/F11(跳副屏96dpi) 热键触发真实跨屏 WM_DPICHANGED，每阶段截图
$ErrorActionPreference = 'Stop'
$out = $PSScriptRoot
Add-Type -AssemblyName System.Drawing

$src = @"
using System;
using System.Runtime.InteropServices;
public static class U {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
}
"@
Add-Type -TypeDefinition $src

$p = Start-Process -FilePath (Join-Path $out 'PandaPortManager.exe') -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { Write-Host 'NO WINDOW'; Stop-Process -Id $p.Id -Force; exit 1 }
Start-Sleep -Milliseconds 500

function Title([IntPtr]$h) {
  $sb = New-Object System.Text.StringBuilder 256
  [U]::GetWindowText($h, $sb, 256) | Out-Null
  return $sb.ToString()
}

function Snap([string]$path) {
  $r = New-Object 'U+RECT'
  [U]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $ht = $r.B - $r.T
  if ($ht -gt 400) { $ht = 400 }
  if ($w -lt 10 -or $ht -lt 10) { Write-Host "snap skip (rect $w x $ht)"; return }
  $bmp = New-Object System.Drawing.Bitmap($w, $ht)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [U]::PrintWindow($hwnd, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "saved $path ($w x $ht)"
}

function Send-Key([int]$vk) {
  [U]::PostMessage($hwnd, 0x100, [IntPtr]$vk, [IntPtr]0x000F0001) | Out-Null
  [U]::PostMessage($hwnd, 0x101, [IntPtr]$vk, [IntPtr]0xC00F0001) | Out-Null
}

Write-Host ("start dpi = " + [U]::GetDpiForWindow($hwnd))

Send-Key 0x7A   # F12: 跳主屏(192dpi)
Start-Sleep -Seconds 3
$r1 = New-Object 'U+RECT'; [U]::GetWindowRect($hwnd, [ref]$r1) | Out-Null
Write-Host ("after F12: title=" + (Title $hwnd) + " rect=" + $r1.L + "," + $r1.T + " dpi=" + [U]::GetDpiForWindow($hwnd))
Snap (Join-Path $out 'snap_1_192.png')

Send-Key 0x7B   # F11: 跳副屏(96dpi)
Start-Sleep -Seconds 3
$r2 = New-Object 'U+RECT'; [U]::GetWindowRect($hwnd, [ref]$r2) | Out-Null
Write-Host ("after F11: title=" + (Title $hwnd) + " rect=" + $r2.L + "," + $r2.T + " dpi=" + [U]::GetDpiForWindow($hwnd))
Snap (Join-Path $out 'snap_2_96.png')

Send-Key 0x7A   # F12: 回主屏(192dpi)
Start-Sleep -Seconds 3
Write-Host ("after F12: title=" + (Title $hwnd) + " dpi=" + [U]::GetDpiForWindow($hwnd))
Snap (Join-Path $out 'snap_3_192back.png')

Stop-Process -Id $p.Id -Force
Write-Host 'DONE'
