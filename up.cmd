@echo off
REM One-command launcher for the KSC.Observability demo environment.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0up.ps1" %*
