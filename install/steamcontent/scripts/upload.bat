@echo off
setlocal

if "%~1"=="" (
    echo [ERREUR] Login Steam manquant.
    echo Usage: %~nx0 ^<login^> ^<win^|linux^>
    call :halt
    exit /b 1
)

if /I "%~2"=="linux" (
    set PLATFORM_DIR=linux64
    set APP_BUILD_SCRIPT=app_build_linux.vdf
    set STEAM_NATIVE=libsteam_api.so
    set GAME_BINARY=SettlersOfIdlestan
) else if /I "%~2"=="win" (
    set PLATFORM_DIR=win64
    set APP_BUILD_SCRIPT=app_build_win.vdf
    set STEAM_NATIVE=steam_api64.dll
    set GAME_BINARY=SettlersOfIdlestan.exe
) else (
    echo [ERREUR] Plateforme invalide : "%~2". Valeurs attendues : win, linux.
    call :halt
    exit /b 1
)

echo ====================================
echo Upload Steam Build - %~2
echo ====================================

rem Surchargeable depuis l'environnement si steamcmd est installe ailleurs.
if not defined STEAMCMD set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set STEAM_LOGIN=%~1
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%%APP_BUILD_SCRIPT%"
set CONTENT_DIR=%SCRIPT_DIR%..\%PLATFORM_DIR%

echo.
echo === Verification du contenu ===
rem Le contenu est produit par install\build_desktop_%~2.bat, redistribuables Steam
rem compris. On refuse d'uploader un repertoire incomplet : une build sans la native
rem Steam passe la validation Steamworks mais plante au lancement chez les joueurs.
if not exist "%CONTENT_DIR%\%GAME_BINARY%" (
    echo [ERREUR] %GAME_BINARY% introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_%~2.bat.
    call :halt
    exit /b 1
)
if not exist "%CONTENT_DIR%\%STEAM_NATIVE%" (
    echo [ERREUR] %STEAM_NATIVE% introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_%~2.bat.
    call :halt
    exit /b 1
)
if not exist "%CONTENT_DIR%\Steamworks.NET.dll" (
    echo [ERREUR] Steamworks.NET.dll introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_%~2.bat.
    call :halt
    exit /b 1
)
echo [OK] %GAME_BINARY%, %STEAM_NATIVE% et Steamworks.NET.dll presents.

if not exist "%STEAMCMD%" (
    echo.
    echo [ERREUR] steamcmd introuvable : %STEAMCMD%
    echo Definissez la variable d'environnement STEAMCMD pour pointer vers steamcmd.exe.
    call :halt
    exit /b 1
)

"%STEAMCMD%" +login %STEAM_LOGIN% +run_app_build %SCRIPT% +quit
if errorlevel 1 (
    echo.
    echo [ERREUR] steamcmd a retourne une erreur. Consultez install\steamcontent\output.
    call :halt
    exit /b 1
)

echo.
echo Upload termine.
call :halt
exit /b 0

rem Ne bloque que si le script est lance a la main : build_and_upload.bat enchaine
rem les plateformes et positionne SOI_NOPAUSE pour ne pas attendre une touche.
:halt
if not defined SOI_NOPAUSE pause
exit /b 0
