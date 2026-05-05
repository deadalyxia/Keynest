@echo off
echo ============================================================
echo  Keynest Password Manager - Build Script
echo ============================================================
echo.

REM Check .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK not found.
    echo Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/3] Restoring packages...
dotnet restore VaultApp.csproj
if %errorlevel% neq 0 ( echo [ERROR] Restore failed. & pause & exit /b 1 )

echo.
echo [2/3] Building release...
dotnet publish VaultApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\
if %errorlevel% neq 0 ( echo [ERROR] Build failed. & pause & exit /b 1 )

echo.
echo [3/3] Done!
echo.
echo Output: publish\VaultApp.exe
echo.
echo Your vault data will be stored in:
echo %%APPDATA%%\VaultApp\
echo.
pause
