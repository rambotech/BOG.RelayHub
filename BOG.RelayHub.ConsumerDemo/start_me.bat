@echo off

REM 02/07/2024 -- Start the demo / tester

PUSHD %~d0%~p0

BOG.RelayHub.ConsumerDemo.exe %1
echo Exit code: %ERRORLEVEL%

POPD

