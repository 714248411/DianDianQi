$ErrorActionPreference = 'Continue'
Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
public struct RECT { public int Left, Top, Right, Bottom; }
'@ -Name W -Namespace N

$p = Get-Process | Where-Object { $_.ProcessName -like '*离线版*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p -eq $null) { Write-Output 'NO PROCESS'; exit 1 }
$r = New-Object N.W+RECT
[N.W]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
if ($r.Left -ne 40 -or $r.Top -ne 40)
{
    [N.W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
    [N.W]::MoveWindow($p.MainWindowHandle, 40, 40, 1060, 660, $true) | Out-Null
    Start-Sleep -Milliseconds 500
}
[N.W]::SetCursorPos(40 + 468, 40 + 17) | Out-Null
Start-Sleep -Milliseconds 250
[N.W]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[N.W]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
Remove-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -Force -ErrorAction SilentlyContinue
New-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -ItemType File -Force | Out-Null
Start-Sleep -Milliseconds 1500
Copy-Item 'C:\Users\Administrator\Desktop\bl_appself.png' 'C:\Users\Administrator\Desktop\bl_edit2.png' -Force
Write-Output 'ok'
