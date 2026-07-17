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

for %%P in (win linux) do (
    echo.
    echo =============================================
    echo  Plateforme : %%P
    echo =============================================

    echo.
    echo === Nettoyage des repertoires temporaires ===
    if exist "%ROOT_DIR%\SettlersOfIdlestanOpenTK\bin" rd /s /q "%ROOT_DIR%\SettlersOfIdlestanOpenTK\bin"
    if exist "%ROOT_DIR%\SettlersOfIdlestanOpenTK\obj" rd /s /q "%ROOT_DIR%\SettlersOfIdlestanOpenTK\obj"
    if exist "%SCRIPT_DIR%..\output" rd /s /q "%SCRIPT_DIR%..\output"

    echo.
    echo === Generation de la version %%P ===
    call "%ROOT_DIR%\install\build_opentk_%%P.bat"
    if errorlevel 1 (
        echo.
        echo [ERREUR] La generation de la version %%P a echoue.
        pause
        exit /b 1
    )

    echo.
    echo === Upload Steam %%P ===
    call "%SCRIPT_DIR%upload.bat" %STEAM_LOGIN% %%P
    if errorlevel 1 (
        echo.
        echo [ERREUR] L'upload Steam %%P a echoue.
        pause
        exit /b 1
    )
)

echo.
echo [OK] Build et upload Steam termines pour Windows et Linux.
pause
