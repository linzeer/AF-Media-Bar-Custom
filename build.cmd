@echo off
echo ============================================================
echo   AF Media Bar  -  build single-file exe
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] dotnet not found. Please install .NET 8 SDK first.
  echo         https://dotnet.microsoft.com/download/dotnet/8.0
  echo.
  pause
  exit /b 1
)

echo [1/3] .NET SDK versions installed:
dotnet --list-sdks

echo.
echo [2/3] Publishing  Release / win-x64 / self-contained single-file ...
echo.
dotnet publish "%~dp0AFMediaBar.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo [ERROR] Build failed. Make sure .NET 8 SDK is installed and NuGet source is reachable.
  pause
  exit /b 1
)

echo.
echo [3/3] Done!
echo.
echo Output exe:
echo   %~dp0bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\AFMediaBar.exe
echo.
echo Close any running AFMediaBar process before launching the new exe.
echo.
pause
