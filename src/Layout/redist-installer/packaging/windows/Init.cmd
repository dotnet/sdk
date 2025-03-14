@echo off

set DOTNET_MULTILEVEL_LOOKUP=0
set PATH=%~dp0;%PATH%

if not "%PROCESSOR_ARCHITECTURE%"=="x86" goto :SetDotnetRoot_Wow
if not "%PROCESSOR_ARCHITEW6432%"== "" goto :SetDotnetRoot_Wow

:SetDotnetRoot
set DOTNET_ROOT=%~dp0
goto :eof

:SetDotnetRoot_Wow
set DOTNET_ROOT(x86)=%~dp0
goto :eof
