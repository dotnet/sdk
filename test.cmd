@echo off
powershell -NoLogo -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\build.ps1""" -test -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
