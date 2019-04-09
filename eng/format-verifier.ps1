[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$repo,
    [string]$sha,
    [string]$testPath
)

function Clone-Repo([string]$repo, [string]$sha, [string]$repoPath) {
    $currentLocation = Get-Location

    git.exe clone $repo $repoPath

    Set-Location $repoPath

    git.exe checkout $sha

    Set-Location $currentLocation
}

if (!(Test-Path $testPath)) {
    New-Item -ItemType Directory -Force -Path $testPath | Out-Null
}

try {
    $repoName = $repo.Substring(19)
    $folderName = $repoName.Split("/")[1]
    $repoPath = Join-Path $testPath $folderName

    Write-Output "$(Get-Date) - Cloning $repoName."
    Clone-Repo $repo $sha $repoPath

    Write-Output "$(Get-Date) - Finding solutions."
    $solutions = Get-ChildItem -Path $repoPath -Filter *.sln -Recurse -Depth 2 | Select-Object -ExpandProperty FullName | Where-Object { $_ -match '.sln$' }

    foreach ($solution in $solutions) {
        $solutionFile = Split-Path $solution -leaf

        Write-Output "$(Get-Date) - $solutionFile - Restoring"
        dotnet.exe restore $solution

        Write-Output "$(Get-Date) - $solutionFile - Formatting"
        $output = dotnet.exe run -p .\src\dotnet-format.csproj -c Release -- -w $solution -v d --dry-run | Out-String
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
    Remove-Item $repoPath -Force -Recurse
    Write-Output "$(Get-Date) - Deleted $repoName."
}
