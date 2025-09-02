# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

# PowerShell script to launch dotnet.exe with 'dnx' and all passed arguments
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dotnet = Join-Path $scriptDir 'dotnet.exe'
& $dotnet dnx @Args
exit $LASTEXITCODE
