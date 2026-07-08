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
echo [OK] Repertoires steamcmd generes dans install\steamcontent\
echo.
pause
exit /b 0

:build
set ARCH=%1
set PUBLISH_DIR=SettlersOfIdlestanOpenTK\bin\Release\net10.0\osx-%ARCH%\publish
set CONTENT_DIR=install\steamcontent\osx_%ARCH%

echo.
echo --- osx-%ARCH% ---

if exist "%PUBLISH_DIR%" (
    echo Nettoyage du dossier publish...
    rd /s /q "%PUBLISH_DIR%"
)

dotnet publish SettlersOfIdlestanOpenTK -c Release -r osx-%ARCH% --self-contained true
if errorlevel 1 exit /b 1

if exist "%CONTENT_DIR%" rd /s /q "%CONTENT_DIR%"
mkdir "%CONTENT_DIR%"

robocopy "%PUBLISH_DIR%" "%CONTENT_DIR%" /E /NFL /NDL /NJH /NJS
if errorlevel 8 exit /b 1

echo [OK] %CONTENT_DIR%
exit /b 0

:error
echo.
echo [ERREUR] Le build a echoue.
pause
exit /b 1
