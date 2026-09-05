@echo off
cd /d "%~dp0"
dotnet run -c Release -- --race-gauntlet --seed 1 --last-island 5 --gauntlet-output race-gauntlet
pause
