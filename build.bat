@echo off
setlocal
if not "%~2"=="" exit /b 2
if "%~1"=="" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if not "%~1"=="" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" "%~1"
exit /b %ERRORLEVEL%
