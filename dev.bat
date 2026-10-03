@echo off
rem ============================================================
rem  Chay app o che do DEV (Debug).
rem    dev.bat              -> build Debug va chay app
rem    dev.bat watch        -> chay va tu build lai khi sua code (dotnet watch)
rem    dev.bat file.pdf     -> chay va mo san file.pdf
rem  Xem docs/build-tooling.md
rem ============================================================
setlocal
cd /d "%~dp0"
call scripts\env.bat || exit /b 1

if /i "%~1"=="watch" (
    shift
    dotnet watch --project PDFEditorApp\PDFEditorApp.csproj run -c Debug -- %1 %2 %3
    exit /b %errorlevel%
)

dotnet run --project PDFEditorApp\PDFEditorApp.csproj -c Debug -- %*
exit /b %errorlevel%
