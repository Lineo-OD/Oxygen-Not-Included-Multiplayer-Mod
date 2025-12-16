@echo off
echo.
echo ============================================
echo    ONI Multiplayer - Build and Install
echo ============================================
echo.

:: Set mod install path
set "MOD_PATH=%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Dev\OniMultiplayer"

echo [1/2] Building mod...
echo.
dotnet build --configuration Debug
if errorlevel 1 (
    echo.
    echo ============================================
    echo    BUILD FAILED! Check errors above.
    echo ============================================
    exit /b 1
)

echo.
echo [2/2] Installing to: %MOD_PATH%
echo.

:: Create folder if needed
if not exist "%MOD_PATH%" mkdir "%MOD_PATH%"

:: Copy files
if exist "bin\OniMultiplayer.dll" (
    copy /Y "bin\OniMultiplayer.dll" "%MOD_PATH%\" >nul
    echo   [OK] OniMultiplayer.dll
) else (
    echo   [!!] OniMultiplayer.dll NOT FOUND!
    exit /b 1
)

if exist "bin\LiteNetLib.dll" (
    copy /Y "bin\LiteNetLib.dll" "%MOD_PATH%\" >nul
    echo   [OK] LiteNetLib.dll
)

copy /Y "mod_info.yaml" "%MOD_PATH%\" >nul
echo   [OK] mod_info.yaml

echo.
echo ============================================
echo    DONE! Launch ONI and enable the mod.
echo    Location: Options - Mods - Dev
echo ============================================
echo.
