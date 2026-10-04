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
    $p = Get-Process | Where-Object { $_.ProcessName -like '*离线版*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
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

$running = Get-Process | Where-Object { $_.ProcessName -like '*离线版*' }
if (-not $running)
{
    Start-Process -FilePath 'C:\Users\Administrator\Desktop\布灵点点离线版\布灵点点离线版.exe' -WorkingDirectory 'C:\Users\Administrator\Desktop\布灵点点离线版'
    Start-Sleep -Seconds 3
}
Click-At 541 17 700      # 多功能
Click-At 80 67 500       # 选择 常用操作 下的“延迟”
Click-At 160 287 500     # 生成代码
Remove-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -Force -ErrorAction SilentlyContinue
New-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -ItemType File -Force | Out-Null
Start-Sleep -Milliseconds 1800
Write-Output '--- 编辑器内容(字节数与内容):'
$bytes = [System.IO.File]::ReadAllBytes('C:\Users\Administrator\Desktop\bl_code.txt')
Write-Output ('bytes=' + $bytes.Length)
$t = [System.IO.File]::ReadAllText('C:\Users\Administrator\Desktop\bl_code.txt', [System.Text.Encoding]::UTF8)
Write-Output ($t | Format-Hex | Out-String)
