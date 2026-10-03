@echo off
rem ============================================================
rem  Lint / format code (giong ESLint):
rem    lint.bat           -> tu sua format theo .editorconfig
rem    lint.bat --check   -> chi kiem tra (dung cho CI): format + build -warnaserror
rem  Phai 0 loi truoc khi xong task. Xem docs/build-tooling.md
rem ============================================================
setlocal
cd /d "%~dp0"
call scripts\env.bat || exit /b 1

if /i "%~1"=="--check" (
    echo [1/2] dotnet format --verify-no-changes
    dotnet format PDFEditorApp.sln --verify-no-changes --no-restore
    if errorlevel 1 (
        echo [LOI] Code chua dung format. Chay "lint.bat" de tu sua.
        exit /b 1
    )
    echo [2/2] dotnet build -warnaserror
    dotnet build PDFEditorApp.sln -warnaserror --nologo -v q
    if errorlevel 1 exit /b 1
    echo Lint OK - 0 loi.
    exit /b 0
)

dotnet format PDFEditorApp.sln
exit /b %errorlevel%
