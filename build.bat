@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "ZIP_DIR=%SCRIPT_DIR%zip"
set "STAGE=%ZIP_DIR%\stage"

echo === Virtuart4DConvert build + pack ===
echo.

:: Check .NET SDK installed
where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet SDK not found.
    echo   Install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause & exit /b 1
)
for /f "tokens=1" %%V in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%V"
for /f "delims=." %%M in ("%DOTNET_VER%") do set "DOTNET_MAJOR=%%M"
echo   .NET SDK: %DOTNET_VER%
if %DOTNET_MAJOR% LSS 8 (
    echo ERROR: .NET 8+ SDK required. Found: %DOTNET_VER%
    echo   Install from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause & exit /b 1
)
echo.

:: Check for MPXJ.Net updates and auto-upgrade
echo [0/4] Checking for MPXJ.Net updates...
for /f "tokens=4" %%V in ('dotnet list "%SCRIPT_DIR%Virtuart4DConvert.csproj" package --outdated 2^>nul ^| findstr /i "mpxj"') do set "LATEST_VER=%%V"
if defined LATEST_VER (
    echo   MPXJ.Net update found: %LATEST_VER% — upgrading...
    dotnet add "%SCRIPT_DIR%Virtuart4DConvert.csproj" package MPXJ.Net --version %LATEST_VER%
    if errorlevel 1 ( echo ERROR: dotnet add package failed. & pause & exit /b 1 )
    echo   MPXJ.Net upgraded to %LATEST_VER%.
) else (
    echo   MPXJ.Net: up to date.
)
echo.

:: Create zip\ if needed; clean staging subfolder
if not exist "%ZIP_DIR%" mkdir "%ZIP_DIR%"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%"

:: Publish into staging
echo [1/4] dotnet publish...
dotnet publish "%SCRIPT_DIR%Virtuart4DConvert.csproj" -c Release -r win-x64 --self-contained -o "%STAGE%"
:: IKVM may emit IKVM-0001 "Failed to attach child process" (benign parallel-build noise).
:: Check exe existence instead of trusting the exit code.
if not exist "%STAGE%\Virtuart4DConvert.exe" (
    echo ERROR: dotnet publish did not produce Virtuart4DConvert.exe
    pause & exit /b 1
)

:: Remove debug symbols
echo [2/4] Removing .pdb files...
del /q "%STAGE%\*.pdb" 2>nul

:: Copy licenses into staging
echo [3/4] Copying licenses...
mkdir "%STAGE%\licenses" 2>nul
xcopy /e /y /q "%SCRIPT_DIR%licenses\*" "%STAGE%\licenses\"

:: Create zip inside zip\ folder
echo [4/4] Creating Virtuart4DConvert.zip...
if exist "%ZIP_DIR%\Virtuart4DConvert.zip" del /q "%ZIP_DIR%\Virtuart4DConvert.zip"
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%ZIP_DIR%\Virtuart4DConvert.zip' -Force"
if errorlevel 1 ( echo ERROR: zip failed. & pause & exit /b 1 )

:: Delete staging — only zip remains
echo Cleaning staging...
rmdir /s /q "%STAGE%"

echo.
echo Done.
echo   zip\Virtuart4DConvert.zip
echo.
pause
