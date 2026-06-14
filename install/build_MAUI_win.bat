@echo off
setlocal

:: Se placer à la racine du projet (répertoire parent de install)
cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan Desktop (Release)
echo =============================================

dotnet publish SettlersOfIdlestanDesktop -f net10.0-windows10.0.19041.0 -c Release
if errorlevel 1 (
    echo.
    echo [ERREUR] Le build a echoue.
    pause
    exit /b 1
)

echo.
echo === Compression du build ===

set PUBLISH_DIR=SettlersOfIdlestanDesktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish
set ZIP_PATH=install\SettlersOfIdlestan_Desktop.zip

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
