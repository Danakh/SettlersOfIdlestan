@echo off

echo ====================================
echo Upload Steam Build
echo ====================================

set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set STEAM_SDK_DIR=C:\DEV\steamcmd\sdk
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%app_build.vdf"
set CONTENT_DIR=%SCRIPT_DIR%..\win64

echo.
echo === Copie des fichiers redistribuables Steam ===
copy /Y "%STEAM_SDK_DIR%\redistributable_bin\win64\steam_api64.dll" "%CONTENT_DIR%\steam_api64.dll"
if errorlevel 1 (
    echo.
    echo [ERREUR] Copie de steam_api64.dll echouee. Verifiez que le SDK Steamworks est present dans %STEAM_SDK_DIR%.
    pause
    exit /b 1
)

%STEAMCMD% +login TODO +run_app_build %SCRIPT% +quit

echo.
echo Upload termine.
pause