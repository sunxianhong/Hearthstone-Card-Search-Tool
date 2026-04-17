$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$desktopProject = Join-Path $projectRoot "desktop\HearthstoneCardSearchTool.csproj"
$portableRoot = Join-Path $projectRoot "dist\HearthstoneCardSearchTool"

if (Test-Path -LiteralPath $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}

dotnet publish $desktopProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -o $portableRoot

Copy-Item -LiteralPath (Join-Path $projectRoot "CardDefs.xml") -Destination (Join-Path $portableRoot "CardDefs.xml") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "cardpng") -Destination (Join-Path $portableRoot "cardpng") -Recurse -Force
$legacyHtml = Get-ChildItem -LiteralPath $projectRoot -Filter *.html | Select-Object -First 1
if ($null -ne $legacyHtml) {
    Copy-Item -LiteralPath $legacyHtml.FullName -Destination (Join-Path $portableRoot $legacyHtml.Name) -Force
}

Write-Host "Portable build created:" (Join-Path $portableRoot "HearthstoneCardSearchTool.exe")
