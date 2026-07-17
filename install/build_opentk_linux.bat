@echo off
setlocal

cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan OpenTK - Linux
echo =============================================

set PUBLISH_DIR=SettlersOfIdlestanOpenTK\bin\Release\net10.0\linux-x64\publish
set CONTENT_DIR=install\steamcontent\linux64

if exist "%PUBLISH_DIR%" (
    echo Nettoyage du dossier publish...
    rd /s /q "%PUBLISH_DIR%"
)

dotnet publish SettlersOfIdlestanOpenTK -c Release -r linux-x64 --self-contained true
if errorlevel 1 (
    echo.
    echo [ERREUR] Le build a echoue.
    pause
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
    pause
    exit /b 1
)

echo.
echo [OK] Contenu steamcmd pret : %CONTENT_DIR%
echo.
pause
exit /b 0
