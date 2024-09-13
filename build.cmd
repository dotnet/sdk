@echo off

echo %* | findstr /C:"-pack" >nul
if %errorlevel%==0 (
    set PackInstaller=
) else (
    set PackInstaller=/p:PackInstaller=false
)
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -command "& """%~dp0eng\common\build.ps1""" -restore -build -nativeToolsOnMachine -msbuildEngine dotnet %PackInstaller% %*"
exit /b %ErrorLevel%
