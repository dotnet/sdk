<#
.SYNOPSIS
    Script to perform git restore for a set of commonly edited paths when building the VMR.

.DESCRIPTION
    This script restores the specified paths using git restore command. It provides options for logging, confirmation, and parameterized restore.

.PARAMETER PathsToRestore
    Specifies the paths to be restored. Default paths are:
    - src/*/eng/common/*
    - src/*global.json

.PARAMETER LogPath
    Specifies the path to save the log file. Default is 'restore.log' in the script directory.

.PARAMETER NoPrompt
    Indicates whether to skip the confirmation prompt. If specified, the script will restore the paths without confirmation.

#>

param (
    [string[]]$PathsToRestore = @(
        "src/*/eng/common/*",
        "src/*global.json"
    ),
    [Alias("y")]
    [switch]$NoPrompt = $false
)

# Confirmation prompt
if (-not $NoPrompt) {
    Write-Host "Will restore changes in the following paths:"
    foreach ($path in $PathsToRestore) {
        Write-Host "  $path"
    }
    $choice = Read-Host "Do you want to proceed with restoring the paths? (Y/N)"
    if (-not $($choice -ieq "Y")) {
        exit 0
    }
}

# Perform git restore for each path
foreach ($path in $PathsToRestore) {
    git -C (Split-Path -Path $PSScriptRoot -Parent) restore $path
}
