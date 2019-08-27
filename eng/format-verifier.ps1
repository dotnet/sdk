[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$repo,
    [string]$sha,
    [string]$testPath
)

$currentLocation = Get-Location

if (!(Test-Path $testPath)) {
    New-Item -ItemType Directory -Force -Path $testPath | Out-Null
}

try {
    $repoName = $repo.Substring(19)
    $folderName = $repoName.Split("/")[1]
    $repoPath = Join-Path $testPath $folderName

    Write-Output "$(Get-Date) - Cloning $repoName."
    git.exe clone $repo $repoPath

    Set-Location $repoPath

    git.exe checkout $sha

    Write-Output "$(Get-Date) - Finding solutions."
    $solutions = Get-ChildItem -Filter *.sln -Recurse -Depth 2 | Select-Object -ExpandProperty FullName | Where-Object { $_ -match '.sln$' }

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

    foreach ($solution in $solutions) {
        $solutionPath = Split-Path $solution
        $solutionFile = Split-Path $solution -leaf

        Set-Location $solutionPath

        Write-Output "$(Get-Date) - $solutionFile - Restoring"
        dotnet.exe restore $solution

        Write-Output "$(Get-Date) - $solutionFile - Formatting"
        $output = dotnet.exe run -p "$currentLocation\src\dotnet-format.csproj" -c Release -- -w $solution -v d --dry-run | Out-String
        Write-Output $output.TrimEnd()
        
        if ($LastExitCode -ne 0) {
            Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode."
            exit -1
        }
        
        if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
            Write-Output "$(Get-Date) - No files found for project."
            exit -1
        }

        Write-Output "$(Get-Date) - $solutionFile - Complete"
    }
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation

    Remove-Item $repoPath -Force -Recurse
    Write-Output "$(Get-Date) - Deleted $repoName."
}
