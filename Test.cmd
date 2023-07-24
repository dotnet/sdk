@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -test -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
