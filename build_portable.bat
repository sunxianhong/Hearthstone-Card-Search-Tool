@echo off
setlocal

cd /d "%~dp0"

echo [1/3] Checking .NET SDK...
where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo Could not find dotnet in PATH.
    echo Please install the .NET SDK and try again.
    exit /b 1
)

if not exist "%~dp0build_exe.ps1" (
    echo.
    echo Missing build_exe.ps1 in the project root.
    exit /b 1
)

echo [2/3] Building portable package...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_exe.ps1"
if errorlevel 1 (
    echo.
    echo Portable build failed.
    exit /b 1
)

echo.
echo [3/3] Done.
echo Output: "%~dp0dist\HearthstoneCardSearchTool\HearthstoneCardSearchTool.exe"
echo.
pause
