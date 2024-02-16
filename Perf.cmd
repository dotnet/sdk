@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\Perf.ps1""" %*"
exit /b %ErrorLevel%
