@echo off

if "%~1"=="" (
    echo [ERREUR] Login Steam manquant.
    echo Usage: %~nx0 ^<login^> ^<win^|linux^>
    pause
    exit /b 1
)

if /I "%~2"=="linux" (
    set PLATFORM_DIR=linux64
    set APP_BUILD_SCRIPT=app_build_linux.vdf
) else if /I "%~2"=="win" (
    set PLATFORM_DIR=win64
    set APP_BUILD_SCRIPT=app_build_win.vdf
) else (
    echo [ERREUR] Plateforme invalide : "%~2". Valeurs attendues : win, linux.
    pause
    exit /b 1
)

echo ====================================
echo Upload Steam Build - %~2
echo ====================================

set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set STEAM_LOGIN=%~1
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%%APP_BUILD_SCRIPT%"
set CONTENT_DIR=%SCRIPT_DIR%..\%PLATFORM_DIR%
set REDIST_DIR=%SCRIPT_DIR%..\redist

if /I "%~2"=="win" (
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
) else if /I "%~2"=="linux" (
    echo.
    echo === Copie des fichiers redistribuables Steam ===
    rem NE PAS prendre libsteam_api.so depuis le Steamworks SDK : il doit correspondre
    rem exactement a la version de Steamworks.NET utilisee (voir sa FAQ). La lib vient
    rem de la release "Standalone" de Steamworks.NET, deposee dans %REDIST_DIR%.
    copy /Y "%REDIST_DIR%\libsteam_api.so" "%CONTENT_DIR%\libsteam_api.so"
    if errorlevel 1 (
        echo.
        echo [ERREUR] Copie de libsteam_api.so echouee. Verifiez que le fichier est present dans %REDIST_DIR%.
        pause
        exit /b 1
    )
)

%STEAMCMD% +login %STEAM_LOGIN% +run_app_build %SCRIPT% +quit

echo.
echo Upload termine.
pause
