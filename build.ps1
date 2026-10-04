$ErrorActionPreference = 'Continue'
$dir = 'C:\Users\Administrator\Desktop\言逸点点离线版'
Stop-Process -Name '言逸点点器' -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
$src = Get-ChildItem "$dir\src\*.cs" | ForEach-Object { $_.FullName }
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = "$dir\言逸点点器.exe"
Remove-Item $exe -Force -ErrorAction SilentlyContinue
$output = & $csc /nologo /target:winexe /platform:anycpu /optimize+ /langversion:5 `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll `
  /out:$exe $src 2>&1
$errors = $output | ForEach-Object { "$_" } | Where-Object { $_ -match ': error ' }
if ($errors) { $errors | Select-Object -First 30; Write-Output 'BUILD FAILED'; exit 1 }
if (-not (Test-Path $exe)) { Write-Output 'BUILD FAILED'; exit 1 }

# DLL 桥接器：让 x86 / x64 DLL 都能被脚本调用
Remove-Item "$dir\DllBridge32.exe","$dir\DllBridge64.exe" -Force -ErrorAction SilentlyContinue
& $csc /nologo /target:exe /platform:x86 /optimize+ /out:"$dir\DllBridge32.exe" "$dir\bridge\DllBridge.cs" 2>&1 |
  Where-Object { "$_" -match ': error ' }
& $csc /nologo /target:exe /platform:x64 /optimize+ /out:"$dir\DllBridge64.exe" "$dir\bridge\DllBridge.cs" 2>&1 |
  Where-Object { "$_" -match ': error ' }
$bridgeOk = (Test-Path "$dir\DllBridge32.exe") -and (Test-Path "$dir\DllBridge64.exe")

Write-Output 'BUILD OK: 言逸点点器.exe'
if ($bridgeOk) { Write-Output 'BUILD OK: DllBridge32.exe / DllBridge64.exe' } else { Write-Output 'WARN: DLL 桥接器编译失败' }
