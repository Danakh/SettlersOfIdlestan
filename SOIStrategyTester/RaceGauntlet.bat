@echo off
cd /d "%~dp0"
dotnet run -c Release -- --race-gauntlet --seed 1 --islands 4 --gauntlet-output race-gauntlet
pause
