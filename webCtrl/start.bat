@echo off
setlocal
cd /d "%~dp0"

if not exist "node_modules\" (
    echo [start] node_modules not found, running npm install...
    call npm install
    if errorlevel 1 (
        echo [start] npm install failed.
        pause
        exit /b 1
    )
)

title YFramework webCtrl
echo [start] starting node server.js ...
echo [start] open browser: http://localhost:7777
echo.

node server.js

echo.
echo [start] server exited.
pause