@echo off
setlocal

cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan Desktop - Linux
echo =============================================

set PUBLISH_DIR=SettlersOfIdlestanAvalonia.Desktop\bin\Release\net10.0\linux-x64\publish
set CONTENT_DIR=install\steamcontent\linux64
set REDIST_DIR=install\steamcontent\redist

if exist "%PUBLISH_DIR%" (
    echo Nettoyage du dossier publish...
    rd /s /q "%PUBLISH_DIR%"
)

dotnet publish SettlersOfIdlestanAvalonia.Desktop -c Release -r linux-x64 --self-contained true
if errorlevel 1 (
    echo.
    echo [ERREUR] Le build a echoue.
    call :halt
    exit /b 1
)

echo.
echo === Preparation du repertoire steamcmd ===

if exist "%CONTENT_DIR%" rd /s /q "%CONTENT_DIR%"
mkdir "%CONTENT_DIR%"

robocopy "%PUBLISH_DIR%" "%CONTENT_DIR%" /E /NFL /NDL /NJH /NJS
if errorlevel 8 (
    echo.
    echo [ERREUR] La copie a echoue.
    call :halt
    exit /b 1
)

echo.
echo === Copie des redistribuables Steam ===
rem Steamworks.NET ne livre pas la native dans son paquet NuGet : sans cette copie le
rem repertoire de contenu est incomplet et le jeu plante a l'initialisation de Steam.
rem NE PAS prendre libsteam_api.so depuis le Steamworks SDK : il doit correspondre
rem exactement a la version de Steamworks.NET utilisee (voir sa FAQ). La lib vient
rem de la release "Standalone" de Steamworks.NET, deposee dans %REDIST_DIR%.
copy /Y "%REDIST_DIR%\libsteam_api.so" "%CONTENT_DIR%\libsteam_api.so" >nul
if errorlevel 1 (
    echo.
    echo [ERREUR] Copie de libsteam_api.so echouee. Verifiez que le fichier est present dans %REDIST_DIR%.
    call :halt
    exit /b 1
)

echo.
echo [OK] Contenu steamcmd pret : %CONTENT_DIR%
echo.
call :halt
exit /b 0

rem Ne bloque que si le script est lance a la main : build_and_upload.bat enchaine
rem les plateformes et positionne SOI_NOPAUSE pour ne pas attendre une touche.
:halt
if not defined SOI_NOPAUSE pause
exit /b 0
