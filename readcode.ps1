$ErrorActionPreference = 'Continue'
Remove-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -Force -ErrorAction SilentlyContinue
New-Item 'C:\Users\Administrator\Desktop\布灵点点离线版\shot.req' -ItemType File -Force | Out-Null
Start-Sleep -Milliseconds 1800
Write-Output '--- bl_code.txt raw bytes:'
$b = [System.IO.File]::ReadAllBytes('C:\Users\Administrator\Desktop\bl_code.txt')
Write-Output (($b | ForEach-Object { $_.ToString('X2') }) -join ' ')
Write-Output '--- as UTF8:'
Write-Output [System.Text.Encoding]::UTF8.GetString($b)
