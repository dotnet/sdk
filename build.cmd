@echo off

setlocal

set DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\build.ps1""" %*"
exit /b %ErrorLevel%
