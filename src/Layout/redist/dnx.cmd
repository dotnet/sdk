:: Licensed to the .NET Foundation under one or more agreements.
:: The .NET Foundation licenses this file to you under the MIT license.

@echo off
setlocal

set "DOTNET=%~dp0dotnet.exe"

for /f "tokens=1" %%i in ('"%DOTNET%" --list-sdks') do (
    set "SDK_VERSION=%%i"
)

set "SDK_PATH=%~dp0sdk\%SDK_VERSION%\dotnet.dll"

"%DOTNET%" exec "%SDK_PATH%" dnx %*

endlocal
