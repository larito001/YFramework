@echo off
setlocal
rem ============================================================
rem  Generate C# from network .proto sources via repo-bundled protoc.
rem  Sources : %~dp0Net\*.proto
rem  Output  : %~dp0..\Assets\Scripts\GamePlay\Network\Messages\*.cs
rem  protoc  : %~dp0..\..\tools\3rdparty\protobuf\protoc.exe (libprotoc 3.6.1)
rem  Runtime : Assets\Plugins\Google.Protobuf\Google.Protobuf.dll (3.21.12)
rem ============================================================
set SCRIPT_DIR=%~dp0
set PROTOC=%SCRIPT_DIR%..\..\tools\3rdparty\protobuf\protoc.exe
set PROTO_DIR=%SCRIPT_DIR%Net
set OUT_DIR=%SCRIPT_DIR%..\Assets\Scripts\GamePlay\Network\Messages

if not exist "%PROTOC%" (
    echo [ERROR] protoc not found at: %PROTOC%
    exit /b 1
)
if not exist "%PROTO_DIR%" (
    echo [ERROR] proto source dir not found: %PROTO_DIR%
    exit /b 1
)
if not exist "%OUT_DIR%" (
    mkdir "%OUT_DIR%"
)

echo Using protoc: %PROTOC%
for %%F in ("%PROTO_DIR%\*.proto") do (
    echo Compiling %%~nxF
    "%PROTOC%" -I="%PROTO_DIR%" --csharp_out="%OUT_DIR%" "%%F"
    if errorlevel 1 (
        echo [ERROR] protoc failed on %%~nxF
        exit /b 1
    )
)
echo Done.
endlocal