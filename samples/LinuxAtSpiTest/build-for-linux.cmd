@echo off
echo Building portable Linux binary...

dotnet publish -c Release -r linux-x64 --self-contained false -o publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo.
    echo Portable files created in: publish\
    echo.
    echo To test on Linux:
    echo 1. Copy the 'publish' folder to your Linux machine
    echo 2. Run: dotnet LinuxAtSpiTest.dll
    echo 3. Test with: atspi-spy
) else (
    echo Build failed!
)