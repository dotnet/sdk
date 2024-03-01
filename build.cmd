@echo off
powershell -NoLogo -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -restore -build -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
