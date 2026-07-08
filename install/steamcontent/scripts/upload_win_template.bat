@echo off

echo ====================================
echo Upload Steam Build
echo ====================================

set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%app_build.vdf"

%STEAMCMD% +login TODO +run_app_build %SCRIPT% +quit

echo.
echo Upload termine.
pause