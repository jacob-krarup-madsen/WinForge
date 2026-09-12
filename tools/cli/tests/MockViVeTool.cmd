@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0MockViVeTool.ps1" %*
exit /b %ERRORLEVEL%
