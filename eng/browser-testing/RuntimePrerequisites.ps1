# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
function Copy-BrowserRuntimePrerequisites {
    param(
        [Parameter(Mandatory)][string] $BootstrapRoot,
        [Parameter(Mandatory)][string] $SdkLayout,
        [Parameter(Mandatory)][string[]] $Versions
    )

    $ErrorActionPreference = 'Stop'
    if ($Versions.Count -eq 0) {
        throw 'The TestFX source must declare its test runtime prerequisites.'
    }

    foreach ($version in ($Versions | Select-Object -Unique)) {
        if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
            throw "Invalid TestFX runtime prerequisite: '$version'."
        }
        $relativeRoot = "shared\Microsoft.NETCore.App\$version"
        $source = Join-Path $BootstrapRoot $relativeRoot
        $destination = Join-Path $SdkLayout $relativeRoot
        if (-not (Test-Path -LiteralPath $source -PathType Container)) {
            throw "Restore the declared TestFX runtime before composing the SDK: $version."
        }
        $sourceFiles = @(Get-ChildItem -LiteralPath $source -File -Recurse)
        if ($sourceFiles.Count -eq 0) {
            throw "The restored TestFX runtime is empty: $version."
        }

        $files = foreach ($file in $sourceFiles) {
            $relativePath = [IO.Path]::GetRelativePath($source, $file.FullName)
            $target = Join-Path $destination $relativePath
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) {
                throw "The composed runtime does not match the restored input: $version/$relativePath."
            }
            @{ Path = $relativePath; Length = $file.Length; SHA256 = $hash }
        }
        @{
            Framework = 'Microsoft.NETCore.App'; Version = $version
            Source = $source; Files = @($files)
        }
    }
}
