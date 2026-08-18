@echo off
cd /d "%~dp0"
dotnet run -c Release -- --pandemonium-ascended --seed 1 --max-island-hours 4 --checkpoint-hours 1 --pandemonium-output pandemonium-ascended
pause
