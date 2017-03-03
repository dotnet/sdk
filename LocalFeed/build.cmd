@echo off
SETLOCAL

IF EXIST "%ProgramFiles(x86)%\MSBuild\12.0\Bin\MSBuild.exe" (
    SET MSBUILDEXE="%ProgramFiles(x86)%\MSBuild\12.0\Bin\MSBuild.exe"
) ELSE (
    set MSBUILDEXE=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe
)

%MSBUILDEXE% Setup.msbuild %*

ENDLOCAL