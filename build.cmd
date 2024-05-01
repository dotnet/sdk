@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -nativeToolsOnMachine -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
