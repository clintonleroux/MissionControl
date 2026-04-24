@echo off
REM Double-click-friendly wrapper around run.ps1 on Windows.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
