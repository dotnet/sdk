@echo off
taskkill /IM "dotnet.exe" /F /T > nul 2> nul
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0eng\common\build.ps1""" -build -restore %*"
exit /b %ErrorLevel%
