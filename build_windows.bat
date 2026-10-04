@echo off
rem ============================================================
rem  Build PRODUCTION: bo cai dat PDFEditor-Setup-<version>-<runtime>.exe
rem  (cai nhu phan mem: Start menu, Desktop, go cai dat, mo file .pdf; khong can cai .NET)
rem    build_windows.bat          -> Windows x64 (mac dinh)
rem    build_windows.bat win-x86  -> Windows 32-bit
rem    build_windows.bat win-arm64
rem  Ket qua: dist\PDFEditor-Setup-<version>-<runtime>.exe
rem  Can Inno Setup 6.5+ (winget install JRSoftware.InnoSetup).
rem  Xem docs/build-tooling.md, docs/installer.md
rem ============================================================
setlocal
cd /d "%~dp0"
call scripts\env.bat || exit /b 1

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"
set "PUBLISH=%CD%\dist\publish\%RID%"

rem --- Tim trinh dong goi Inno Setup (ISCC.exe) ---
set "ISCC="
for /f "delims=" %%i in ('where iscc 2^>nul') do if not defined ISCC set "ISCC=%%i"
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    echo [LOI] Khong tim thay Inno Setup 6. Cai dat: winget install JRSoftware.InnoSetup
    exit /b 1
)

rem --- Phien ban lay tu <Version> trong PDFEditorApp.csproj ---
set "VER="
for /f "delims=" %%v in ('dotnet msbuild PDFEditorApp\PDFEditorApp.csproj -getProperty:Version -nologo') do set "VER=%%v"
if not defined VER (
    echo [LOI] Khong doc duoc Version trong PDFEditorApp.csproj.
    exit /b 1
)

echo [1/3] Chay test...
dotnet test PDFEditorApp.sln -c Release --nologo -v q
if errorlevel 1 (
    echo [LOI] Test that bai - dung build.
    exit /b 1
)

echo [2/3] Publish %RID% %VER% (Release, self-contained)...
if exist "%PUBLISH%" rmdir /s /q "%PUBLISH%"
dotnet publish PDFEditorApp\PDFEditorApp.csproj -c Release -r %RID% --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:DebugType=none ^
    -o "%PUBLISH%" --nologo
if errorlevel 1 (
    echo [LOI] Publish that bai.
    exit /b 1
)

echo [3/3] Dong goi bo cai dat...
"%ISCC%" /Q "/DAppVersion=%VER%" "/DRid=%RID%" "/DSourceDir=%PUBLISH%" installer\PDFEditor.iss
if errorlevel 1 (
    echo [LOI] Dong goi bo cai dat that bai.
    exit /b 1
)

echo Xong: dist\PDFEditor-Setup-%VER%-%RID%.exe
exit /b 0
