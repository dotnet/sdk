@echo off
setlocal

powershell -ExecutionPolicy ByPass -NoProfile -Command "& '%~dp0startvs.ps1'" %*