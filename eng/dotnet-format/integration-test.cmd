@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0format-verifier.ps1""" %*"
exit /b %ErrorLevel%