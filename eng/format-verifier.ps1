[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$repo,
    [string]$sha,
    [string]$testPath,
    [string]$stage  # Valid values are "prepare", "format-workspace", "format-folder"
)

$currentLocation = Get-Location

if (!(Test-Path $testPath)) {
    New-Item -ItemType Directory -Force -Path $testPath | Out-Null
}

try {
    $repoName = $repo.Substring(19)
    $folderName = $repoName.Split("/")[1]
    $repoPath = Join-Path $testPath $folderName

    if ($stage -eq "prepare") {
        Write-Output "$(Get-Date) - Cloning $repoName."
        git.exe clone $repo $repoPath --single-branch --no-tags
    }

    Set-Location $repoPath

    if ($stage -eq "prepare") {
        git.exe checkout $sha
    }

    # We invoke build.ps1 ourselves because running `restore.cmd` invokes the build.ps1
    # in a child process which means added .NET Core SDKs aren't visible to this process.
    if (Test-Path '.\eng\Build.ps1') {
        Write-Output "$(Get-Date) - Running Build.ps1 -restore"
        .\eng\Build.ps1 -restore
    }
    elseif (Test-Path '.\eng\common\Build.ps1') {
        Write-Output "$(Get-Date) - Running Build.ps1 -restore"
        .\eng\common\Build.ps1 -restore
    }

    if ($stage -eq "prepare" -or $stage -eq "format-workspace") {
        Write-Output "$(Get-Date) - Finding solutions."
        $solutions = Get-ChildItem -Filter *.sln -Recurse -Depth 2 | Select-Object -ExpandProperty FullName | Where-Object { $_ -match '.sln$' }

        foreach ($solution in $solutions) {
            $solutionPath = Split-Path $solution
            $solutionFile = Split-Path $solution -leaf

            Set-Location $solutionPath

            if ($stage -eq "prepare") {
                Write-Output "$(Get-Date) - $solutionFile - Restoring"
                dotnet.exe restore $solution
            }

            if ($stage -eq "format-workspace") {
                Write-Output "$(Get-Date) - $solutionFile - Formatting Workspace"
                $output = dotnet.exe run -p "$currentLocation\src\dotnet-format.csproj" -c Release -- -w $solution -v diag --check | Out-String
                Write-Output $output.TrimEnd()

                # Ignore CheckFailedExitCode since we don't expect these repos to be properly formatted.
                if ($LastExitCode -ne 0 -and $LastExitCode -ne 2) {
                    Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode."
                    exit -1
                }

                if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
                    Write-Output "$(Get-Date) - No files found for solution."
                    exit -1
                }
            }

            Write-Output "$(Get-Date) - $solutionFile - Complete"
        }
    }

    if ($stage -eq "format-folder") {
        Write-Output "$(Get-Date) - $folderName - Formatting Folder"
        $output = dotnet.exe run -p "$currentLocation\src\dotnet-format.csproj" -c Release -- -f $repoPath -v diag --check | Out-String
        Write-Output $output.TrimEnd()

        # Ignore CheckFailedExitCode since we don't expect these repos to be properly formatted.
        if ($LastExitCode -ne 0 -and $LastExitCode -ne 2) {
            Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode."
            exit -1
        }

        if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
            Write-Output "$(Get-Date) - No files found for solution."
            exit -1
        }

        Write-Output "$(Get-Date) - $folderName - Complete"
    }

    exit 0
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation
}
