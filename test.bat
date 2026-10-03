@echo off
rem Chay toan bo unit test (dotnet test). Xem docs/build-tooling.md
setlocal
cd /d "%~dp0"
call scripts\env.bat || exit /b 1
dotnet test PDFEditorApp.sln --nologo %*
exit /b %errorlevel%
