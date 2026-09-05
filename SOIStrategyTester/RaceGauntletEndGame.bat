@echo off
cd /d "%~dp0"
dotnet run -c Release -- --race-gauntlet --seed 1 --last-island 5 --final-island-points 100 --gauntlet-output race-gauntlet-endgame
pause
