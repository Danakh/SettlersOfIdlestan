@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "TRAILER_ROOT=%SCRIPT_DIR:~0,-1%"
set "REPO_ROOT=%TRAILER_ROOT%\..\.."
set "PROJECT=%REPO_ROOT%\SOITrailerGenerator\SOITrailerGenerator.csproj"

echo === Compilation de SOITrailerGenerator (Release) ===
dotnet build "%PROJECT%" -c Release
if errorlevel 1 (
    echo Echec de la compilation.
    exit /b 1
)

echo.
echo === Generation des frames ===
dotnet run --no-build --project "%PROJECT%" -c Release -- "%TRAILER_ROOT%"
if errorlevel 1 (
    echo Echec de la generation des frames.
    exit /b 1
)

echo.
echo === Assemblage de la video avec ffmpeg ===
set "FFMPEG_FALLBACK_DIR=C:\Program Files\ffmpeg-8.1.1-essentials_build\bin"
where ffmpeg >nul 2>nul
if errorlevel 1 (
    if exist "%FFMPEG_FALLBACK_DIR%\ffmpeg.exe" (
        set "PATH=%FFMPEG_FALLBACK_DIR%;%PATH%"
    ) else (
        echo ffmpeg est introuvable dans le PATH ni dans %FFMPEG_FALLBACK_DIR%.
        echo Installez-le ou ajoutez-le au PATH puis relancez ce script.
        exit /b 1
    )
)

set "FFMPEG_CMD_FILE=%TRAILER_ROOT%\ffmpeg_command.txt"
if not exist "%FFMPEG_CMD_FILE%" (
    echo Fichier ffmpeg_command.txt introuvable dans %TRAILER_ROOT%.
    exit /b 1
)

set /p FFMPEG_CMD=<"%FFMPEG_CMD_FILE%"
%FFMPEG_CMD%
if errorlevel 1 (
    echo Echec de l'assemblage ffmpeg.
    exit /b 1
)

echo.
echo === Trailer genere : %TRAILER_ROOT%\trailer.mp4 ===
endlocal
