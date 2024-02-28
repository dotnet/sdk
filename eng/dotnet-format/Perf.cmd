@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Perf.ps1""" %*"
exit /b %ErrorLevel%
