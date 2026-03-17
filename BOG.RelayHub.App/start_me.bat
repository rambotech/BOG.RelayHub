REM 1/24/2024

@echo off

PUSHD %~d0%~p0

if /I "%1" == "F" goto FRESH

BOG.RelayHub.App.exe ^
--MaxCountQueuedFilenames 20 ^
--Listeners http://*:5050 ^
--SecurityDelaySecondsFactor 15 ^
--SecurityMaxInvalidTokenAttempts 3

goto END

:FRESH

BOG.RelayHub.App.exe ^
--MaxCountQueuedFilenames 20 ^
--Listeners http://*:5050 ^
--SecurityDelaySecondsFactor 15 ^
--SecurityMaxInvalidTokenAttempts 3

:END

POPD
