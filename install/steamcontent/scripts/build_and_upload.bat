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

rem Les scripts appeles s'arretent sur un pause quand on les lance a la main. Ici la
rem chaine doit se derouler sans intervention : seul ce script attend, a la fin ou
rem sur la premiere erreur.
set SOI_NOPAUSE=1

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
    echo === Generation de la version %%P ===
    call "%ROOT_DIR%\install\build_desktop_%%P.bat"
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
