@echo off
chcp 65001 >nul
echo ========================================
echo   Publish All
echo ========================================

python publish_config.py

echo.
echo Done!
pause
