<#
.SYNOPSIS

This script is used for synchronizing the dotnet/dotnet VMR locally. This means pulling new
code from various repositories into the 'dotnet/dotnet' repository.

.DESCRIPTION

The script is used during CI to ingest new code based on dotnet/sdk but it can also help
for reproducing potential failures during sdk's PRs, namely to fix the Source-Build.
Another usecase is to try manually synchronizing a given commit of some repo into the VMR and
trying to Source-Build the VMR. This can help when fixing the Source-Build but using a commit
from a not-yet merged branch (or fork) to test the fix will help.

The tooling that synchronizes the VMR will need to clone the various repositories into a temporary
folder. These clones can be re-used in future synchronizations so it is advised you dedicate a
folder to this to speed up your re-runs.

.EXAMPLE
  Synchronize current sdk and all dependencies into a local VMR:
    ./vmr-sync.ps1 -vmrDir "$HOME/repos/dotnet" -tmpDir "$HOME/repos/tmp"

  Synchronize the VMR to a specific commit of dotnet/runtime using custom fork:
    ./vmr-sync.ps1 `
       -repository runtime:e7e71da303af8dc97df99b098f21f526398c3943 `
       -remote runtime:https://github.com/yourfork/runtime `
       -tmpDir "$HOME/repos/tmp"

.PARAMETER tmpDir
Required. Path to the temporary folder where repositories will be cloned

.PARAMETER vmrBranch
Optional. Branch of the 'dotnet/dotnet' repo to synchronize. The VMR will be checked out to this branch

.PARAMETER recursive
Optional. Recursively synchronize all the source build dependencies (declared in Version.Details.xml)
This is used when performing the full synchronization during sdk's CI and the final VMR sync.
Defaults to false unless no repository is supplied in which case a recursive sync of sdk is performed.

.PARAMETER remote
Optional. Additional remote to use during the synchronization
This can be used to synchronize to a commit from a fork of the repository
Example: 'runtime:https://github.com/yourfork/runtime'

.PARAMETER repository
Optional. Repository + git ref separated by colon to synchronize to.
This can be a specific commit, branch, tag.
If not supplied, the revision of the parent sdk repository of this script will be used (recursively).
Example: 'runtime:my-branch-name'

.PARAMETER tpnTemplate
Optional. Template for the header of VMRs THIRD-PARTY-NOTICES file.
Defaults to src/VirtualMonoRepo/THIRD-PARTY-NOTICES.template.txt

.PARAMETER azdevPat
Optional. Azure DevOps PAT to use for cloning private repositories.

.PARAMETER vmrDir
Optional. Path to the dotnet/dotnet repository. When null, gets cloned to the temporary folder

.PARAMETER debugOutput
Optional. Enables debug logging in the darc vmr command.

.PARAMETER ci
Optional. Denotes that the script is running in a CI environment.
#>
param (
  [Parameter(Mandatory=$true, HelpMessage="Path to the temporary folder where repositories will be cloned")]
  [string][Alias('t', 'tmp')]$tmpDir,
  [string][Alias('b', 'branch')]$vmrBranch,
  [switch]$recursive,
  [string]$remote,
  [string][Alias('r')]$repository,
  [string]$tpnTemplate = "src/VirtualMonoRepo/THIRD-PARTY-NOTICES.template.txt",
  [string]$azdevPat,
  [string][Alias('v', 'vmr')]$vmrDir,
  [switch]$ci,
  [switch]$debugOutput
)

$scriptRoot = $PSScriptRoot

function Fail {
  Write-Host "> $($args[0])" -ForegroundColor 'Red'
}

function Highlight {
  Write-Host "> $($args[0])" -ForegroundColor 'Cyan'
}

$sdkDir = (Split-Path -Parent $scriptRoot)

# If sdk is a repo, we're in an sdk and not in the dotnet/dotnet repo
if (Test-Path -Path "$sdkDir/.git" -PathType Container) {
  $additionalRemotes = "sdk:$sdkDir"
}

if ($remote) {
  $additionalRemotes = "$additionalRemotes $remote"
}

$verbosity = 'verbose'
if ($debugOutput) {
  $verbosity = 'debug'
}
# Validation

if (-not (Test-Path -Path $sdkDir -PathType Container)) {
  Fail "Directory '$sdkDir' does not exist. Please specify the path to the dotnet/sdk repo"
  exit 1
}

if (-not $tmpDir) {
  Fail "Missing -tmpDir argument. Please specify the path to the temporary folder where the repositories will be cloned"
  exit 1
}

if (-not (Test-Path -Path $tpnTemplate -PathType Leaf)) {
  Fail "File '$tpnTemplate' does not exist. Please specify a valid path to the THIRD-PARTY-NOTICES template"
  exit 1
}

# Sanitize the input

# Default when no repository is provided
if (-not $repository) {
  $repository = "sdk:$(git -C $sdkDir rev-parse HEAD)"
  $recursive = $true
}

if (-not $vmrDir) {
  $vmrDir = Join-Path $tmpDir 'dotnet'
}

if (-not (Test-Path -Path $tmpDir -PathType Container)) {
  New-Item -ItemType Directory -Path $tmpDir | Out-Null
}

# Prepare the VMR

if (-not (Test-Path -Path $vmrDir -PathType Container)) {
  Highlight "Cloning 'dotnet/dotnet' into $vmrDir.."
  git clone https://github.com/dotnet/dotnet $vmrDir

  if ($vmrBranch) {
    git -C $vmrDir switch -c $vmrBranch
  }
}
else {
  if ((git -C $vmrDir diff --quiet) -eq $false) {
    Fail "There are changes in the working tree of $vmrDir. Please commit or stash your changes"
    exit 1
  }

  if ($vmrBranch) {
    Highlight "Preparing $vmrDir"
    git -C $vmrDir checkout $vmrBranch
    git -C $vmrDir pull
  }
}

Set-StrictMode -Version Latest

# Prepare darc

Highlight 'Installing .NET, preparing the tooling..'
. $scriptRoot\common\tools.ps1
$dotnetRoot = InitializeDotNetCli -install:$true
$dotnet = "$dotnetRoot\dotnet.exe"
& "$dotnet" tool restore

Highlight "Starting the synchronization of '$repository'.."

# Synchronize the VMR
$darcArgs = (
    "darc", "vmr", "update",
    "--vmr", $vmrDir,
    "--tmp", $tmpDir,
    "--$verbosity",
    "--tpn-template", $tpnTemplate,
    "--discard-patches",
    "--generate-credscansuppressions",
    $repository
)

if ($recursive) {
  $darcArgs += ("--recursive")
}

if ($ci) {
  $darcArgs += ("--ci")
}

if ($additionalRemotes) {
  $darcArgs += ("--additional-remotes", $additionalRemotes)
}

if ($azdevPat) {
  $darcArgs += ("--azdev-pat", $azdevPat)
}

& "$dotnet" $darcArgs

if ($LASTEXITCODE -eq 0) {
  Highlight "Synchronization succeeded"
}
else {
  Fail "Synchronization of dotnet/dotnet to '$repository' failed!"
  Fail "'$vmrDir' is left in its last state (re-run of this script will reset it)."
  Fail "Please inspect the logs which contain path to the failing patch file (use -debugOutput to get all the details)."
  Fail "Once you make changes to the conflicting VMR patch, commit it locally and re-run this script."
  exit 1
}
