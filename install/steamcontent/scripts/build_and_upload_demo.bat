@echo off
setlocal

if "%~1"=="" (
    echo [ERREUR] Login Steam manquant.
    echo Usage: %~nx0 ^<login^>
    pause
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set ROOT_DIR=%SCRIPT_DIR%..\..\..
set STEAM_LOGIN=%~1

rem Les scripts appeles s'arretent sur un pause quand on les lance a la main. Ici la chaine
rem doit se derouler sans intervention : seul ce script attend, a la fin ou sur la premiere
rem erreur.
set SOI_NOPAUSE=1

echo =============================================
echo  DEMO Steam - build et upload
echo =============================================
echo.
echo Le mode demo vient de -p:DemoBuild=true, pas de la branche : ce script produit une demo
echo quelle que soit la branche checkoutee, et n'ecrit jamais dans install\steamcontent\win64
echo ni linux64, qui restent le contenu du jeu complet.
echo.

for %%P in (win linux) do (
    echo.
    echo =============================================
    echo  Plateforme : %%P
    echo =============================================

    echo.
    echo === Nettoyage des repertoires temporaires ===
    if exist "%ROOT_DIR%\SettlersOfIdlestanAvalonia.Desktop\bin" rd /s /q "%ROOT_DIR%\SettlersOfIdlestanAvalonia.Desktop\bin"
    if exist "%ROOT_DIR%\SettlersOfIdlestanAvalonia.Desktop\obj" rd /s /q "%ROOT_DIR%\SettlersOfIdlestanAvalonia.Desktop\obj"
    if exist "%SCRIPT_DIR%..\output" rd /s /q "%SCRIPT_DIR%..\output"

    echo.
    echo === Generation de la version demo %%P ===
    call "%ROOT_DIR%\install\build_desktop_demo_%%P.bat"
    if errorlevel 1 (
        echo.
        echo [ERREUR] La generation de la version demo %%P a echoue.
        pause
        exit /b 1
    )

    echo.
    echo === Upload Steam demo %%P ===
    call "%SCRIPT_DIR%upload_demo.bat" %STEAM_LOGIN% %%P
    if errorlevel 1 (
        echo.
        echo [ERREUR] L'upload Steam demo %%P a echoue.
        pause
        exit /b 1
    )
)

echo.
echo [OK] Build et upload Steam de la demo termines pour Windows et Linux.
echo Le build n'est pas encore public : passez-le en live sur la branche voulue depuis
echo Steamworks (Builds de l'application demo).
pause
