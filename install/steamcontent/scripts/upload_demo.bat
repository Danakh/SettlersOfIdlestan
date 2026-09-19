@echo off
setlocal

if "%~1"=="" (
    echo [ERREUR] Login Steam manquant.
    echo Usage: %~nx0 ^<login^> ^<win^|linux^>
    call :halt
    exit /b 1
)

if /I "%~2"=="linux" (
    set PLATFORM_DIR=demo_linux64
    set APP_BUILD_SCRIPT=app_build_demo_linux.vdf
    set STEAM_NATIVE=libsteam_api.so
    set GAME_BINARY=SettlersOfIdlestan
) else if /I "%~2"=="win" (
    set PLATFORM_DIR=demo_win64
    set APP_BUILD_SCRIPT=app_build_demo_win.vdf
    set STEAM_NATIVE=steam_api64.dll
    set GAME_BINARY=SettlersOfIdlestan.exe
) else (
    echo [ERREUR] Plateforme invalide : "%~2". Valeurs attendues : win, linux.
    call :halt
    exit /b 1
)

echo ====================================
echo Upload Steam Build DEMO - %~2
echo ====================================

rem Surchargeable depuis l'environnement si steamcmd est installe ailleurs.
if not defined STEAMCMD set STEAMCMD=C:\DEV\steamcmd\steamcmd.exe
set STEAM_LOGIN=%~1
set SCRIPT_DIR=%~dp0
set SCRIPT="%SCRIPT_DIR%%APP_BUILD_SCRIPT%"
set CONTENT_DIR=%SCRIPT_DIR%..\%PLATFORM_DIR%

echo.
echo === Verification du contenu ===
rem Le contenu est produit par install\build_desktop_demo_%~2.bat, redistribuables Steam
rem compris. On refuse d'uploader un repertoire incomplet : une build sans la native Steam
rem passe la validation Steamworks mais plante au lancement chez les joueurs.
if not exist "%CONTENT_DIR%\%GAME_BINARY%" (
    echo [ERREUR] %GAME_BINARY% introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_demo_%~2.bat.
    call :halt
    exit /b 1
)
if not exist "%CONTENT_DIR%\%STEAM_NATIVE%" (
    echo [ERREUR] %STEAM_NATIVE% introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_demo_%~2.bat.
    call :halt
    exit /b 1
)
if not exist "%CONTENT_DIR%\Steamworks.NET.dll" (
    echo [ERREUR] Steamworks.NET.dll introuvable dans %CONTENT_DIR%.
    echo Lancez d'abord install\build_desktop_demo_%~2.bat.
    call :halt
    exit /b 1
)
rem Garde-fou propre a la demo : seul build_desktop_demo_%~2.bat depose ce marqueur, et il ne le
rem depose qu'apres une publication -p:DemoBuild=true. Sans lui, rien ne distingue a l'oeil un
rem binaire complet d'un binaire demo, et on enverrait le jeu entier sur l'App ID de la demo.
if not exist "%CONTENT_DIR%\demo_build.marker" (
    echo [ERREUR] demo_build.marker introuvable dans %CONTENT_DIR%.
    echo Ce repertoire n'a pas ete produit par install\build_desktop_demo_%~2.bat :
    echo il contient probablement une build complete. Regenerez-le avant d'uploader.
    call :halt
    exit /b 1
)
echo [OK] %GAME_BINARY%, %STEAM_NATIVE%, Steamworks.NET.dll et marqueur demo presents.

if not exist "%STEAMCMD%" (
    echo.
    echo [ERREUR] steamcmd introuvable : %STEAMCMD%
    echo Definissez la variable d'environnement STEAMCMD pour pointer vers steamcmd.exe.
    call :halt
    exit /b 1
)

if not exist %SCRIPT% (
    echo.
    echo [ERREUR] %APP_BUILD_SCRIPT% introuvable dans %SCRIPT_DIR%.
    echo Ce fichier porte l'App ID et le Depot ID de la demo : il n'est pas versionne.
    echo Creez-le a partir de app_build_demo_template.vdf.
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

rem Ne bloque que si le script est lance a la main : build_and_upload_demo.bat enchaine les
rem plateformes et positionne SOI_NOPAUSE pour ne pas attendre une touche.
:halt
if not defined SOI_NOPAUSE pause
exit /b 0
