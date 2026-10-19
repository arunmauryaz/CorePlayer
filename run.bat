@echo off
title CORE PLAYER - Modern Media Player
echo =======================================================
echo          CORE PLAYER - Launching Application          
echo =======================================================
echo.

cd /d "%~dp0"
dotnet run --project src\CorePlayer\CorePlayer.csproj

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Application exited with error code %ERRORLEVEL%.
    pause
)
