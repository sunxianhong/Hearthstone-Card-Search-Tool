$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$desktopProject = Join-Path $projectRoot "desktop\HearthstoneCardSearchTool.csproj"
$outputName = [string]::Concat([char]0x7089, [char]0x77F3, [char]0x5361, [char]0x724C, [char]0x6D4F, [char]0x89C8, [char]0x5668)
$distRoot = Join-Path $projectRoot "dist"
$portableRoot = Join-Path $distRoot $outputName
$portableImageRoot = Join-Path $portableRoot "cardpng"
$preserveRoot = Join-Path $distRoot ".cardpng-preserve"
$preservedImageRoot = Join-Path $preserveRoot "cardpng"

if (Test-Path -LiteralPath $preserveRoot) {
    Remove-Item -LiteralPath $preserveRoot -Recurse -Force
}

if (Test-Path -LiteralPath $portableImageRoot -PathType Container) {
    New-Item -ItemType Directory -Path $preserveRoot -Force | Out-Null
    Move-Item -LiteralPath $portableImageRoot -Destination $preserveRoot -Force
}

if (Test-Path -LiteralPath $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}

dotnet restore $desktopProject -r win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet publish $desktopProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    --no-restore `
    -o $portableRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $projectRoot "CardDefs.xml") -Destination (Join-Path $portableRoot "CardDefs.xml") -Force
if (Test-Path -LiteralPath $preservedImageRoot -PathType Container) {
    Move-Item -LiteralPath $preservedImageRoot -Destination $portableRoot -Force
}
else {
    New-Item -ItemType Directory -Path $portableImageRoot -Force | Out-Null
}

$legacyHtml = Get-ChildItem -LiteralPath $projectRoot -Filter *.html | Select-Object -First 1
if ($null -ne $legacyHtml) {
    Copy-Item -LiteralPath $legacyHtml.FullName -Destination (Join-Path $portableRoot $legacyHtml.Name) -Force
}

if (Test-Path -LiteralPath $preserveRoot) {
    Remove-Item -LiteralPath $preserveRoot -Recurse -Force
}

Write-Host "Portable build created:" (Join-Path $portableRoot ($outputName + ".exe"))
