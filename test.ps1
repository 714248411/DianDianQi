$ErrorActionPreference = 'Continue'
$dir = 'C:\Users\Administrator\Desktop\言逸点点离线版'
Stop-Process -Name '言逸点点器' -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
& "$dir\build.ps1"
if ($LASTEXITCODE -ne 0) { Write-Output 'BUILD FAILED - ABORT'; exit 1 }
Remove-Item "$dir\selftest.log" -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "$dir\言逸点点器.exe" -ArgumentList '--selftest' -PassThru -Wait
Write-Output ('selftest exit code: ' + $p.ExitCode)
Get-Content "$dir\selftest.log" -ErrorAction SilentlyContinue
