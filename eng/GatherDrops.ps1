[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)]
  [String]$filePath,
  [Parameter(Mandatory=$true)]
  [String]$outputPath,
  [Parameter(Mandatory=$true)]
  [String]$darcPath,
  [Parameter(Mandatory=$true)]
  [String]$githubPat,
  [Parameter(Mandatory=$true)]
  [String]$azdevPat
)
$jsonContent = Get-Content -Path $filePath -Raw | ConvertFrom-Json
foreach ($repo in $jsonContent.repositories) {
    $remoteUri = $repo.remoteUri
    $commitSha = $repo.commitSha
    $path = "$outputPath$($repo.path)"
    $darcCommand = "$darcPath gather-drop -c $commitSha -r $remoteUri --non-shipping --skip-existing --continue-on-error --use-azure-credential-for-blobs -o $path --github-pat $githubPat --azdev-pat $azdevPat --verbose --ci"
    Write-Output "Gathering drop for $remoteUri"
    Invoke-Expression $darcCommand
}
exit 0