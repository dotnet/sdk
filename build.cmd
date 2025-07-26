@echo off

echo %* | findstr /C:"-pack" >nul
if %errorlevel%==0 (
    set SkipBuildingInstallers=
) else (
    REM skip crossgen for inner-loop builds to save a ton of time
    set skipFlags="/p:SkipUsingCrossgen=true /p:SkipBuildingInstallers=true"
)
set DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -msbuildEngine dotnet %skipFlags% %*"
exit /b %ErrorLevel%
