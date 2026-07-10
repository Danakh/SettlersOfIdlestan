@echo off

echo ====================================
echo Upload Steam Build
echo ====================================

set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%app_build.vdf"
set CONTENT_DIR=%SCRIPT_DIR%..\win64
set REDIST_DIR=%SCRIPT_DIR%..\redist

echo.
echo === Copie des fichiers redistribuables Steam ===
rem NE PAS prendre steam_api64.dll depuis le Steamworks SDK : il doit correspondre
rem exactement a la version de Steamworks.NET utilisee (voir sa FAQ). La DLL vient
rem de la release "Standalone" de Steamworks.NET, deposee dans %REDIST_DIR%.
copy /Y "%REDIST_DIR%\steam_api64.dll" "%CONTENT_DIR%\steam_api64.dll"
if errorlevel 1 (
    echo.
    echo [ERREUR] Copie de steam_api64.dll echouee. Verifiez que le fichier est present dans %REDIST_DIR%.
    pause
    exit /b 1
)

%STEAMCMD% +login TODO +run_app_build %SCRIPT% +quit

echo.
echo Upload termine.
pause