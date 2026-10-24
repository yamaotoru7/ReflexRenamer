# インストーラーを作る：移植版を .NET 同梱（win-x64、self-contained）で発行し、Inno Setup の ISCC でまとめる。
# 出力は .work/installer/FlexibleRenamer-Setup-<版>.exe。
# 使い方（リポジトリ直下）：pwsh tools/build-installer.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/FlexibleRenamer/FlexibleRenamer.csproj'
$publish = Join-Path $root '.work/publish'

# 版は csproj の <Version> から取る
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'csproj に <Version> がない' }

# ISCC は ユーザー単位（winget --scope user）とマシン全体のどちらの場所でも探す
$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 が見つからない（winget install --id JRSoftware.InnoSetup -e --scope user）' }

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained -p:DebugType=none -o $publish -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish に失敗した' }

& $iscc "/DAppVersion=$version" "/DPublishDir=$publish" (Join-Path $root 'installer/FlexibleRenamer.iss')
if ($LASTEXITCODE -ne 0) { throw 'ISCC に失敗した' }
Get-ChildItem -LiteralPath (Join-Path $root '.work/installer') -Filter "FlexibleRenamer-Setup-$version.exe"
