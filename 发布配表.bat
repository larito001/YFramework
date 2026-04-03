@echo off
chcp 65001 >nul
echo ========================================
echo   Publish All
echo ========================================

python tools\publish_config.py

echo.
echo Done!
pause
