@echo off
if "%~1"=="" (
    "%~dp0dotnet.exe" install
) else (
    "%~dp0dotnet.exe" %*
)
