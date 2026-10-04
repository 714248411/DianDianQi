$ErrorActionPreference = 'Continue'
# OCR 冒烟测试：调用 Umi-OCR 离线识别（首次启动较慢）
Add-Type -AssemblyName System.Windows.Forms
$p = Start-Process -FilePath 'C:\Users\Administrator\Desktop\布灵点点离线版\言逸点点器.exe' -ArgumentList '--selftest-ocr' -PassThru -WorkingDirectory 'C:\Users\Administrator\Desktop\布灵点点离线版'
if (-not $p.WaitForExit(180000)) { Write-Output 'TIMEOUT 3min'; Stop-Process -Id $p.Id -Force; exit 1 }
Write-Output ('exit=' + $p.ExitCode)
Get-Content 'C:\Users\Administrator\Desktop\布灵点点离线版\ocrtest.log' -ErrorAction SilentlyContinue
