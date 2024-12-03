@echo off
powershell -NoLogo -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\build.ps1""" -restore -nativeToolsOnMachine -msbuildEngine dotnet %*"
exit /b %ErrorLevel%
