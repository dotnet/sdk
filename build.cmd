@echo off
powershell -NoLogo -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\build.ps1""" -restore -build -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
