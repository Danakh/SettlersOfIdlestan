@echo off
cd /d "%~dp0"
dotnet run -c Release -- --new-game --endless --objective Data/Objectives/abyss-gate-unlocked.json --strategies Data/Strategies/endless-abyss-gate.json --csv-output run_current.csv --checkpoint-hours 1 --max-cycles 100000
pause
