@echo off
echo ================================
echo Tea Cup SolidWorks API Installer
echo ================================

:: Check for .NET installation
echo Checking for .NET SDK...
dotnet --version >nul 2>&1
IF ERRORLEVEL 1 (
    echo .NET SDK is not installed. Please install it from https://dotnet.microsoft.com/download.
    exit /b
)

:: Create the libs directory if it doesn't exist
IF NOT EXIST libs (
    echo Creating libs directory...
    mkdir libs
)

:: Copy SolidWorks DLLs to libs
echo Copying SolidWorks Interop DLLs to libs directory...
copy "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll" libs >nul
copy "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll" libs >nul

IF ERRORLEVEL 1 (
    echo Failed to copy SolidWorks DLLs. Please ensure SolidWorks is installed and the DLL paths are correct.
    exit /b
)

:: Restore packages
echo Restoring NuGet packages...
dotnet restore >nul
IF ERRORLEVEL 1 (
    echo Failed to restore NuGet packages. Please check your internet connection or .csproj file.
    exit /b
)

:: Build the project
echo Building the project...
dotnet build >nul
IF ERRORLEVEL 1 (
    echo Build failed. Please check the project files for errors.
    exit /b
)

echo ================================
echo Installation complete!
echo ================================
pause
