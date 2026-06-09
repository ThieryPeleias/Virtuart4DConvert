@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "STAGE=%SCRIPT_DIR%zip"

echo === Virtuart4DConvert build + pack ===
echo.

:: Clean any previous staging folder
if exist "%STAGE%" rmdir /s /q "%STAGE%"

:: Build into staging folder
echo [1/4] dotnet publish...
dotnet publish "%SCRIPT_DIR%Virtuart4DConvert.csproj" -c Release -r win-x64 --self-contained -o "%STAGE%"
:: IKVM may emit IKVM-0001 "Failed to attach child process" on Windows (benign parallel-build
:: noise). Check exe existence instead of trusting the exit code.
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

:: Create zip
echo [4/4] Creating Virtuart4DConvert.zip...
if exist "%SCRIPT_DIR%Virtuart4DConvert.zip" del /q "%SCRIPT_DIR%Virtuart4DConvert.zip"
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%SCRIPT_DIR%Virtuart4DConvert.zip' -Force"
if errorlevel 1 ( echo ERROR: zip failed. & pause & exit /b 1 )

:: Delete staging folder — source files are never inside zip/
echo Cleaning up staging folder...
rmdir /s /q "%STAGE%"

echo.
echo Done.
echo   ZIP: %SCRIPT_DIR%Virtuart4DConvert.zip
echo.
pause
