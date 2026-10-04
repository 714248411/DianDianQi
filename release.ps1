param(
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$distRoot = Join-Path $projectRoot 'dist'
$packageName = "DianDianQi-v$Version-windows-offline"
$stageRoot = Join-Path $distRoot $packageName
$zipPath = Join-Path $distRoot "$packageName.zip"
$checksumPath = "$zipPath.sha256"

function Assert-DistPath([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $allowedRoot = [System.IO.Path]::GetFullPath($distRoot) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside dist: $fullPath"
    }
}

& (Join-Path $projectRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

& (Join-Path $projectRoot 'test.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
Assert-DistPath $stageRoot
Assert-DistPath $zipPath
Assert-DistPath $checksumPath

if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path -LiteralPath $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }
New-Item -ItemType Directory -Path $stageRoot | Out-Null

$runtimeExtensions = @('.exe', '.dll', '.indd', '.ini')
Get-ChildItem -LiteralPath $projectRoot -File | Where-Object {
    $runtimeExtensions -contains $_.Extension.ToLowerInvariant()
} | Copy-Item -Destination $stageRoot

foreach ($fileName in @('README.md', 'CHANGELOG.md', 'PLATFORM_SUPPORT.md', 'default_url.txt', 'ico.ico')) {
    $sourcePath = Join-Path $projectRoot $fileName
    if (Test-Path -LiteralPath $sourcePath) { Copy-Item -LiteralPath $sourcePath -Destination $stageRoot }
}

$assetSource = Join-Path $projectRoot 'assets'
if (Test-Path -LiteralPath $assetSource) {
    Copy-Item -LiteralPath $assetSource -Destination $stageRoot -Recurse
}

Compress-Archive -LiteralPath $stageRoot -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Output "RELEASE OK: $zipPath"
Write-Output "SHA256: $hash"
