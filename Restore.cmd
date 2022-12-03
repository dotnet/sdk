@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -restore -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
