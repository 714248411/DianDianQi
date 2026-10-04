$ErrorActionPreference = 'Continue'
Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
public struct RECT { public int Left, Top, Right, Bottom; }
'@ -Name W -Namespace N

function Click-At([int]$x, [int]$y, [int]$delayMs = 600)
{
    $p = Get-Process | Where-Object { $_.ProcessName -like '*言逸*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($p -eq $null) { Write-Output 'NO PROCESS'; return }
    $r = New-Object N.W+RECT
    [N.W]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
    if ($r.Left -ne 40 -or $r.Top -ne 40 -or ($r.Right - $r.Left) -ne 1060 -or ($r.Bottom - $r.Top) -ne 660)
    {
        [N.W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 300
        [N.W]::MoveWindow($p.MainWindowHandle, 40, 40, 1060, 660, $true) | Out-Null
        Start-Sleep -Milliseconds 500
    }
    [N.W]::SetCursorPos(40 + $x, 40 + $y) | Out-Null
    Start-Sleep -Milliseconds 200
    [N.W]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [N.W]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $delayMs
    Write-Output ('click {0},{1}' -f $x, $y)
}

function Shot([string]$path)
{
    Remove-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -Force -ErrorAction SilentlyContinue
    New-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -ItemType File -Force | Out-Null
    Start-Sleep -Milliseconds 1500
    Copy-Item 'C:\Users\Administrator\Desktop\bl_appself.png' $path -Force
    Write-Output ('shot -> ' + $path)
}

$running = Get-Process | Where-Object { $_.ProcessName -like '*言逸*' }
if (-not $running)
{
    Start-Process -FilePath 'C:\Users\Administrator\Desktop\布灵点点离线版\言逸点点器.exe' -WorkingDirectory 'C:\Users\Administrator\Desktop\布灵点点离线版'
    Start-Sleep -Seconds 3
}
Click-At 455 350 500     # 同意

Shot 'C:\Users\Administrator\Desktop\bl_beg5.png'
