@echo off
setlocal
chcp 65001 >nul

set "SCRIPT_DIR=%~dp0"
set "PROJECT_FILE=%SCRIPT_DIR%HsCardImageExporter.csproj"
set "OUTPUT_DLL=%SCRIPT_DIR%bin\Release\net481\HsCardImageExporter.dll"

echo Building HsCardImageExporter...
echo Project: "%PROJECT_FILE%"
echo.

dotnet build "%PROJECT_FILE%" -c Release
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

echo.
echo Build succeeded.
echo DLL: "%OUTPUT_DLL%"
pause
exit /b 0
