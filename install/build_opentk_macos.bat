@echo off
setlocal

cd /d "%~dp0.."

echo =============================================
echo  Build SettlersOfIdlestan OpenTK - macOS
echo  (arm64 = Apple Silicon, x64 = Intel)
echo =============================================

call :build arm64
if errorlevel 1 goto error

call :build x64
if errorlevel 1 goto error

echo.
echo [OK] Builds macOS generes dans install\
echo.
pause
exit /b 0

:build
set ARCH=%1
set PUBLISH_DIR=SettlersOfIdlestanOpenTK\bin\Release\net10.0\osx-%ARCH%\publish
set ZIP_PATH=install\SettlersOfIdlestan_OpenTK_macOS_%ARCH%.zip

echo.
echo --- osx-%ARCH% ---

if exist "%PUBLISH_DIR%" (
    echo Nettoyage du dossier publish...
    rd /s /q "%PUBLISH_DIR%"
)

dotnet publish SettlersOfIdlestanOpenTK -c Release -r osx-%ARCH% --self-contained true
if errorlevel 1 exit /b 1

if exist "%ZIP_PATH%" del "%ZIP_PATH%"

powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 exit /b 1

echo [OK] %ZIP_PATH%
exit /b 0

:error
echo.
echo [ERREUR] Le build a echoue.
pause
exit /b 1
