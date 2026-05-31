@echo off
REM One-click: publish ALL config tables under tools/excel/3xlsx
REM -> proto / C# config classes / .bytes into the client project.
chcp 65001 >nul
cd /d %~dp0
python publish_config.py
echo.
echo ============================================
echo  Done. Switch to Unity and wait for compile.
echo ============================================
pause
