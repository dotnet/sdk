@echo off
setlocal

:: This command launches a Visual Studio solution with environment variables required to use a local version of the .NET Core SDK.

set "SOLUTION=%~1"
if not defined SOLUTION set "SOLUTION=%~dp0sdk.slnx"

:: This tells .NET Core to use the same dotnet.exe that build scripts use
set DOTNET_ROOT=%~dp0.dotnet
set DOTNET_ROOT(x86)=%~dp0.dotnet\x86

:: Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
set PATH=%DOTNET_ROOT%;%PATH%

call restore.cmd

if not exist "%DOTNET_ROOT%\dotnet.exe" (
    echo [ERROR] .NET Core has not yet been installed. Run `%~dp0restore.cmd` to install tools
    exit /b 1
)

:: Prefer the VS in the developer command prompt if we're in one, followed by whatever shows up in the current search path.
set "DEVENV=%DevEnvDir%devenv.exe"

if exist "%DEVENV%" (
    :: Fully qualified works
    set "COMMAND=start "" /B "%ComSpec%" /S /C ""%DEVENV%" "%SOLUTION%"""
) else (
    where devenv.exe /Q
    if not errorlevel 1 (
        :: On the PATH, use that.
        set "COMMAND=start "" /B "%ComSpec%" /S /C "devenv.exe "%SOLUTION%"""
    ) else (
        :: Can't find devenv.exe, let file associations take care of it
        set "COMMAND=start "" /B "%SOLUTION%""
    )
)

%COMMAND%
