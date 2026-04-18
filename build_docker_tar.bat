@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

set "OUTPUT_DIR=%SCRIPT_DIR%dist\docker"
set "OUTPUT_TAR=%OUTPUT_DIR%\hearthstone-card-search.tar"

if not exist "%OUTPUT_DIR%" (
    mkdir "%OUTPUT_DIR%"
)

echo.
echo [1/2] Building Docker image archive...
dotnet publish ".\webapp\HearthstoneCardSearchTool.Web.csproj" -c Release /t:PublishContainer "-p:ContainerRepository=hearthstone-card-search" "-p:ContainerImageTag=latest" "-p:ContainerArchiveOutputPath=%OUTPUT_TAR%"
if errorlevel 1 (
    echo.
    echo Build failed.
    exit /b 1
)

echo.
echo [2/2] Done.
echo TAR file:
echo %OUTPUT_TAR%
echo.

endlocal
