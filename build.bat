@echo off
rem Builds the plugin for Revit 2022, 2024 and 2025 and then the MSI installer.
rem The work is done by build.ps1: it finds MSBuild through vswhere. Needs Visual Studio and WiX Toolset 3.
rem The MSI is written to InstallerWIX\bin\Release.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set RESULT=%ERRORLEVEL%

if %RESULT% neq 0 (
    echo.
    echo Build failed, see the messages above.
)

pause
exit /b %RESULT%
