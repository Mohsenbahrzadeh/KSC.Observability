@echo off
REM Stop the KSC.Observability demo environment.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0down.ps1" %*
