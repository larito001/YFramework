@echo off
chcp 65001 >nul

if "%~1"=="" (
    echo Usage: publish_single.bat [excel_file_path]
    pause
    exit /b 1
)

python publish_config.py %*
pause
