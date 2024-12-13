@echo off

echo %* | findstr /C:"-pack" >nul
if %errorlevel%==0 (
    set PackInstaller=
) else (
    REM disable crossgen for inner-loop builds to save a ton of time
    set PackInstaller=/p:PackInstaller=false
    set DISABLE_CROSSGEN=true
)
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -msbuildEngine dotnet %PackInstaller% %*"
exit /b %ErrorLevel%
