@echo off
cd /d "%~dp0"
dotnet run -c Release -- --race-gauntlet --seed 1 --islands 5 --final-island-points 100 --gauntlet-output race-gauntlet-endgame
pause
