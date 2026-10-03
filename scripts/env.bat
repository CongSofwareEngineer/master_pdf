@echo off
rem Tim dotnet: uu tien dotnet trong PATH, neu khong co thi dung ban cai rieng cho user
rem (%LOCALAPPDATA%\Microsoft\dotnet, cai bang dotnet-install.ps1). Xem docs/build-tooling.md.
where dotnet >nul 2>nul
if %errorlevel%==0 goto :eof

if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
    goto :eof
)

echo [LOI] Khong tim thay .NET SDK 8. Cai dat tai: https://dotnet.microsoft.com/download/dotnet/8.0
exit /b 1
