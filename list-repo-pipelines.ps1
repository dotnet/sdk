<#
.SYNOPSIS
Lists all AzDO pipelines (public and internal projects) associated with given GitHub repository.
"Associated" means that pipeline's YAML definition is in the given repository.

.PARAMETER GitHubRepository
Optional, name of the GitHub repository for which you want the associated public pipelines listed, e.g. dotnet/runtime

.PARAMETER AzDORepository
Optional, name of the Azure DevOps repository for which you want the associated internal pipelines listed, e.g. dotnet-runtime

.PARAMETER PAT
AzDO personal access token from the Internal project (https://dev.azure.com/dnceng/internal).
Needed when AzDORepository used.
Scopes needed are repository and build/pipeline reads.
How to get a PAT:
https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate

#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $False)]
    [string] $GitHubRepository,
    [Parameter(Mandatory = $False)]
    [string] $AzDORepository,
    [Parameter(Mandatory = $False)]
    [string] $PAT
)

$ErrorActionPreference = 'Stop'

if (![string]::IsNullOrEmpty($AzDORepository) -and [string]::IsNullOrEmpty($PAT)) {
    Write-Error "-PAT parameter is needed when listing internal AzDO pipelines"
    exit 2
}

function Write-Pipeline {
    $pipeline = $args[0]
    Write-Output "  - $($pipeline.name)"
    Write-Output "    $($pipeline._links.web)"
    Write-Output ""
}

$internalApiEndpoint = "https://dev.azure.com/dnceng/internal/_apis/"
$publicApiEndpoint = "https://dev.azure.com/dnceng/public/_apis/"

$B64Pat = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(":$PAT"))

$headers = @{
    "Authorization" = "Basic $B64Pat"
}

if (![string]::IsNullOrEmpty($AzDORepository)) {
    Write-Output "Looking up the $AzDORepository repository in the internal project..."
    
    try {
        $repository = Invoke-RestMethod -Method "GET" -Uri "${internalApiEndpoint}git/repositories/${AzDORepository}?api-version=6.0" -Headers $headers -ContentType "application/json" -MaximumRedirection 0
    }
    catch {
        Write-Error "Fail to get the repository details. Please verify the supplied Personal Access Token"
        exit 1
    }
    
    $repositoryId = $repository.id
    
    Write-Output "Found the repository ($($repository.Id))."
    Write-Output "Looking up build definitions..."
    
    try {
        $internalBuildDefinitions = Invoke-RestMethod -Method "GET" -Uri "${internalApiEndpoint}build/definitions?api-version=6.0&repositoryId=${repositoryId}&repositoryType=TfsGit" -Headers $headers
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) {
            Write-Error "Access denied - the supplied Personal Access Token needs to have following scopes: Code (Read), Build (Read)"
        }
        exit 1
    }
    
    Write-Output "[INTERNAL] Pipelines based on $($AzDORepository):"
    $internalBuildDefinitions.value | ForEach-Object { Write-Pipeline $_ }
}

if (![string]::IsNullOrEmpty($GitHubRepository)) {
    $publicBuildDefinitions = Invoke-RestMethod -Method "GET" -Uri "${publicApiEndpoint}build/definitions?api-version=6.0&repositoryId=${GitHubRepository}&repositoryType=GitHub"
    Write-Output "[PUBLIC] Pipelines based on $($GitHubRepository):"
    $publicBuildDefinitions.value | ForEach-Object { Write-Pipeline $_ }
}