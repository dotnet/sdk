@echo off

setlocal

echo %* | findstr /C:"-pack" >nul
if %errorlevel%==0 (
    set skipFlags="/p:SkipUsingCrossgen=false /p:SkipBuildingInstallers=false"
) else (
    REM skip crossgen for inner-loop builds to save a ton of time
    set skipFlags="/p:SkipUsingCrossgen=true /p:SkipBuildingInstallers=true"
)
set DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true
set DOTNET_CLI_USE_MSBUILD_SERVER=1
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -msbuildEngine dotnet %skipFlags% /tlp:summary %*"
exit /b %ErrorLevel%
