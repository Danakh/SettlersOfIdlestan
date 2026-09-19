@echo off
setlocal

cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan Desktop - Windows
echo  MODE DEMO (-p:DemoBuild=true)
echo =============================================

set PUBLISH_DIR=SettlersOfIdlestanAvalonia.Desktop\bin\Release\net10.0\win-x64\publish
set CONTENT_DIR=install\steamcontent\demo_win64
set REDIST_DIR=install\steamcontent\redist

rem Le mode demo est une constante de compilation du head Desktop. On repart d'un obj vide :
rem c'est le seul moyen d'etre certain que MainWindow a bien ete recompile avec DEMO, et de ne
rem jamais empaqueter un binaire complet dans le contenu de la demo (ni l'inverse).
if exist "SettlersOfIdlestanAvalonia.Desktop\bin" rd /s /q "SettlersOfIdlestanAvalonia.Desktop\bin"
if exist "SettlersOfIdlestanAvalonia.Desktop\obj" rd /s /q "SettlersOfIdlestanAvalonia.Desktop\obj"

dotnet publish SettlersOfIdlestanAvalonia.Desktop -c Release -r win-x64 --self-contained true -p:DemoBuild=true
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
rem Meme regle que pour le jeu complet : la native vient de la release "Standalone" de
rem Steamworks.NET, pas du Steamworks SDK. L'App ID, lui, n'est pas dans le binaire - c'est le
rem client Steam qui le fournit au lancement, le meme executable sert donc les deux apps.
copy /Y "%REDIST_DIR%\steam_api64.dll" "%CONTENT_DIR%\steam_api64.dll" >nul
if errorlevel 1 (
    echo.
    echo [ERREUR] Copie de steam_api64.dll echouee. Verifiez que le fichier est present dans %REDIST_DIR%.
    call :halt
    exit /b 1
)

rem Marqueur lu par upload_demo.bat : il refuse d'envoyer un repertoire qui n'a pas ete produit
rem ici, donc compile sans la constante DEMO. Exclu du depot par depot_build_demo_win.vdf.
echo demo build> "%CONTENT_DIR%\demo_build.marker"

echo.
echo [OK] Contenu steamcmd demo pret : %CONTENT_DIR%
echo.
call :halt
exit /b 0

rem Ne bloque que si le script est lance a la main : build_and_upload_demo.bat enchaine les
rem plateformes et positionne SOI_NOPAUSE pour ne pas attendre une touche.
:halt
if not defined SOI_NOPAUSE pause
exit /b 0
