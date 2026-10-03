@echo off
rem ============================================================
rem  Build PRODUCTION: 1 file PDFEditor.exe doc lap (khong can cai .NET)
rem    build_windows.bat          -> Windows x64 (mac dinh)
rem    build_windows.bat win-x86  -> Windows 32-bit
rem    build_windows.bat win-arm64
rem  Ket qua: dist\<runtime>\PDFEditor.exe
rem  Xem docs/build-tooling.md
rem ============================================================
setlocal
cd /d "%~dp0"
call scripts\env.bat || exit /b 1

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"
set "OUT=dist\%RID%"

echo [1/3] Chay test...
dotnet test PDFEditorApp.sln -c Release --nologo -v q
if errorlevel 1 (
    echo [LOI] Test that bai - dung build.
    exit /b 1
)

echo [2/3] Publish %RID% (Release, self-contained, single file)...
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish PDFEditorApp\PDFEditorApp.csproj -c Release -r %RID% --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -o "%OUT%" --nologo
if errorlevel 1 (
    echo [LOI] Publish that bai.
    exit /b 1
)

echo [3/3] Xong: %OUT%\PDFEditor.exe
dir /b "%OUT%"
exit /b 0
