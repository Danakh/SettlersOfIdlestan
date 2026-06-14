@echo off
setlocal

cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan OpenTK - Windows
echo =============================================

set PUBLISH_DIR=SettlersOfIdlestanOpenTK\bin\Release\net10.0\win-x64\publish
set ZIP_PATH=install\SettlersOfIdlestan_OpenTK_Win.zip

if exist "%PUBLISH_DIR%" (
    echo Nettoyage du dossier publish...
    rd /s /q "%PUBLISH_DIR%"
)

dotnet publish SettlersOfIdlestanOpenTK -c Release -r win-x64 --self-contained true
if errorlevel 1 (
    echo.
    echo [ERREUR] Le build a echoue.
    pause
    exit /b 1
)

echo.
echo === Compression du build ===

if exist "%ZIP_PATH%" del "%ZIP_PATH%"

powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 (
    echo.
    echo [ERREUR] La compression a echoue.
    pause
    exit /b 1
)

echo.
echo [OK] Zip genere : %ZIP_PATH%
echo.
pause
