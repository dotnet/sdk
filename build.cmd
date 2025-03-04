@echo off

echo %* | findstr /C:"-pack" >nul
if %errorlevel%==0 (
    set SkipBuildingInstallers=
) else (
    REM disable crossgen for inner-loop builds to save a ton of time
    set SkipBuildingInstallers=/p:SkipBuildingInstallers=true
    set DISABLE_CROSSGEN=true
)
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -msbuildEngine dotnet %SkipBuildingInstallers% %*"
exit /b %ErrorLevel%
