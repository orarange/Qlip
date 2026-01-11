[CmdletBinding()]
param(
  [Parameter(Position=0)]
  [ValidateSet('installer','app')]
  [string]$Target = 'installer',

  [Parameter(Position=1)]
  [string]$Tag = 'v1.0.0',

  [switch]$Upload,
  [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Get-IsccPath {
  $candidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
  )
  foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
  return $null
}

function Assert-Tool($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Required tool not found in PATH: $name"
  }
}

function Bundle-Ffmpeg([string]$publishDir) {
  $work = Join-Path $PSScriptRoot 'installer\work'
  if (Test-Path $work) { Remove-Item $work -Recurse -Force }
  New-Item -ItemType Directory -Path $work | Out-Null

  $zipUrl = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip'
  $zipPath = Join-Path $work 'ffmpeg.zip'
  Write-Host "Downloading FFmpeg..." -ForegroundColor Cyan
  Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath

  $extract = Join-Path $work 'ffmpeg'
  Expand-Archive -Force -LiteralPath $zipPath -DestinationPath $extract
  $ffmpegExe = Get-ChildItem -Path $extract -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
  if (-not $ffmpegExe) { throw 'ffmpeg.exe not found in archive' }

  $ffmpegDir = Join-Path $publishDir 'ffmpeg'
  $licDir = Join-Path $ffmpegDir 'licenses'
  New-Item -ItemType Directory -Path $licDir -Force | Out-Null
  Copy-Item $ffmpegExe.FullName (Join-Path $ffmpegDir 'ffmpeg.exe') -Force

  Get-ChildItem -Path $extract -Recurse -Include 'LICENSE*','COPYING*','README*' | ForEach-Object {
    try { Copy-Item $_.FullName (Join-Path $licDir $_.Name) -Force } catch {}
  }
}

function Publish-Qlip([string]$srcRoot, [string]$publishDir) {
  if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
  New-Item -ItemType Directory -Path $publishDir | Out-Null

  Write-Host "Publishing Qlip from: $srcRoot" -ForegroundColor Cyan
  dotnet publish (Join-Path $srcRoot 'Qlip.csproj') -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o $publishDir

  if (-not (Test-Path (Join-Path $publishDir 'Qlip.exe'))) {
    $exes = Get-ChildItem -Path $publishDir -Filter *.exe -File | Select-Object -ExpandProperty Name
    throw ("Qlip.exe not found in publish output. Exes: " + ($exes -join ', '))
  }
}

function Make-CleanSrcCopy([string]$srcRoot) {
  $tmp = Join-Path $PSScriptRoot '_tmp_src_clean'
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  New-Item -ItemType Directory -Path $tmp | Out-Null

  $excludeDirs = @('_installer_repo','InstallerBuilder','installer','bin','obj','.git','.github','.vs','.vscode','_tmp_unpack','_tmp_src_clean')
  $excludeArgs = @()
  foreach ($d in $excludeDirs) { $excludeArgs += @('/XD', (Join-Path $srcRoot $d)) }

  # robocopy returns non-zero for "copied"; treat >= 8 as failure
  $null = robocopy $srcRoot $tmp /E /NFL /NDL /NJH /NJS /NC /NS /NP @excludeArgs
  if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
  }

  return $tmp
}

function Upload-ReleaseAsset([string]$tag, [string[]]$paths) {
  $env:GH_PAGER = 'cat'
  $gh = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'
  if (-not (Test-Path $gh)) { throw 'gh.exe not found' }

  Write-Host "Uploading assets to release $tag ..." -ForegroundColor Cyan
  & $gh release upload $tag @paths --repo orarange/Qlip --clobber
}

# --- main ---
Assert-Tool dotnet
Assert-Tool robocopy

$version = $Tag.TrimStart('v')
$publish = Join-Path $PSScriptRoot 'installer\publish'
$dist = Join-Path $PSScriptRoot 'installer\dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null

if (-not $SkipBuild) {
  $srcRoot = Split-Path $PSScriptRoot -Parent
  $cleanSrc = Make-CleanSrcCopy $srcRoot
  Publish-Qlip -srcRoot $cleanSrc -publishDir $publish
  Bundle-Ffmpeg -publishDir $publish
}

if ($Target -eq 'installer') {
  $iscc = Get-IsccPath
  if (-not $iscc) { throw 'ISCC.exe not found. Install Inno Setup 6.' }

  $env:QLIP_VERSION = $version
  Push-Location (Join-Path $PSScriptRoot 'installer')
  try {
    Write-Host "Building offline installer..." -ForegroundColor Cyan
    & $iscc .\QlipOffline.iss
  } finally {
    Pop-Location
  }

  $exe = Join-Path $dist "QlipSetup_${version}_win-x64.exe"
  if (-not (Test-Path $exe)) { throw "Installer EXE not found: $exe" }

  if ($Upload) {
    Upload-ReleaseAsset -tag $Tag -paths @($exe)
  } else {
    Write-Host "Built: $exe" -ForegroundColor Green
  }
}

if ($Target -eq 'app') {
  $zip = Join-Path $dist "Qlip_${version}_win-x64_portable.zip"
  if (Test-Path $zip) { Remove-Item $zip -Force }

  Write-Host "Creating portable zip..." -ForegroundColor Cyan
  Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip

  if ($Upload) {
    Upload-ReleaseAsset -tag $Tag -paths @($zip)
  } else {
    Write-Host "Built: $zip" -ForegroundColor Green
  }
}
