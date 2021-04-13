<#
.SYNOPSIS
Prepares data for migration, disables subscriptions, migrates and verifies
DARC subcriptions and default channels.

.DESCRIPTION
This script runs in 3 modes:
    1. GenerateDataFile - creates json file which describes DARC migration.
    2. DisableSubscriptions - disables targeting subscriptions for your repository and old branch.
    3. Migration - using json file generated during Initializatin, this script removes default channels
        and targeting subscriptions for your repository and branch. Then it recreates them under
        a new branch.
    4. Verification - Compares default channels and targeting subscriptions from json file against current
        state in DARC.

.PARAMETER Repository
Mandatory short name of GitHub repository (e.g. dotnet/runtime or dotnet/wpf). This short name is transformed to
public and internal repository (e.g. for dotnet/runtime https://github.com/dotnet/runtime and
https://dev.azure.com/dnceng/internal/_git/dotnet-runtime).

.PARAMETER NewBranch
Optional new name of branch, defaults to 'main'.

.PARAMETER OldBranch
Optional old name of branch, defaults to 'master'.

.PARAMETER GenerateDataFile
Switch to run in generate data file mode. Repository parameter is required and NewBranch, OldBranch are optional.

.PARAMETER DisableSubscriptions
Switch to run in disable subscriptions on old branch mode. DataFile parameter is required.

.PARAMETER Migrate
Switch to run in migration mode. DataFile parameter is required.

.PARAMETER Verify
Switch to run in verification mode. DataFile parameter is required.

.PARAMETER DataFile
json file path used for DARC validation.

.PARAMETER DryRun
When specified then no DARC updates are executed, but only logged.

.EXAMPLE
1. For initilization execute:
./m2m-dotnet.ps1 -GenerateDataFile -Repository dotnet/m2m-renaming-test-1
or you can additionaly specify branch names:
./m2m-dotnet.ps1 -GenerateDataFile -Repository dotnet/m2m-renaming-test-1  -OldBranch master -NewBranch main

This generates data file m2m-dotnet_[timestamp].json and disables all targeting subscriptions.

2. To disable targeting subscriptions for your repository and old branch execute:
.\m2m-dotnet.ps1  -DisableSubscriptions -DataFile m2m-dotnet_[timestamp].json

3. For migration execute:
.\m2m-dotnet.ps1  -Migrate -DataFile m2m-dotnet_[timestamp].json

4. For verification execute:
.\m2m-dotnet.ps1  -Verify -DataFile m2m-dotnet_[timestamp].json

#>

[CmdletBinding()]
param (

    [Parameter(ParameterSetName = 'GenerateDataFile', Mandatory = $true)]
    [switch]$GenerateDataFile,

    [Parameter(ParameterSetName = 'DisableSubscriptions', Mandatory = $true)]
    [switch]$DisableSubscriptions,

    [Parameter(ParameterSetName = 'Migrate', Mandatory = $true)]
    [switch]$Migrate,

    [Parameter(ParameterSetName = 'Verify', Mandatory = $true)]
    [switch]$Verify,

    [Parameter(ParameterSetName = 'GenerateDataFile', Mandatory = $true)]
    [string]$Repository,
    [Parameter(ParameterSetName = 'GenerateDataFile')]
    [string]$NewBranch = "main",
    [Parameter(ParameterSetName = 'GenerateDataFile')]
    [string]$OldBranch = "master",

    [Parameter(ParameterSetName = 'Verify', Mandatory = $true)]
    [Parameter(ParameterSetName = 'Migrate', Mandatory = $true)]
    [Parameter(ParameterSetName = 'DisableSubscriptions', Mandatory = $true)]
    [string]$DataFile,

    [switch]$DryRun = $false
)


Class DarcExecutor {
    [bool]$DryRun = $false

    [string[]] ParseIgnoreChecks([string] $line) {
        $ignoreChecks = @()
        # Matches fragment like : ignoreChecks = [ "WIP",  "license/cla" ]
        if ($line -match "ignoreChecks\s*=\s*\[\s*([^\]]+)\s*\]") {
            $ignoreChecksValuesMatches = [regex]::matches($matches[1], "`"([^`"]+)`"")
            ForEach ($check in $ignoreChecksValuesMatches) {
                $ignoreChecks += $check.Groups[1].Value
            }
        }

        return $ignoreChecks
    }

    [object[]] ParseMergePolicies([string] $line) {
        $line = $line -replace "ignoreChecks\s*=\s*\[\s*[^\]]*\s*\]", ""
        $policies = $line -split "\s+" | Where-Object { $_ }
        return $policies
    }

    [object[]] ParseSubscriptions([string] $output) {
        $darcOutputLines = $output.Split([Environment]::NewLine)
        $list = @()
        $processingMergePolicies = $false
        $batchable = $fromRepo = $fromChannel = $updateFrequency = $enabled = $mergePolicies = $null
        For ($i = 0; $i -le $darcOutputLines.Length; $i++) {
            $line = $darcOutputLines[$i]
            # Matches header like: https://github.com/dotnet/arcade (.NET Eng - Latest) ==> 'https://github.com/dotnet/m2m-renaming-test-1' ('main')
            if ($line -match "([^\s]+)\s+\(([^\)]+)\)\s+==>\s+'([^']+)'\s+\('([^\)]+)'\)") {
                if ($i -ne 0) {
                    $list += @{fromRepo = $fromRepo; fromChannel = $fromChannel; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; ignoreChecks = @($this.ParseIgnoreChecks($mergePolicies)); mergePolicies = @($this.ParseMergePolicies($mergePolicies)) };
                }

                $updateFrequency = $enabled = $batchable = $mergePolicies = ""

                $fromRepo = $matches[1]
                $fromChannel = $matches[2]
                continue
            }
            # Matches field like: - Update Frequency: EveryWeek
            if ($line -match "^\s+\-\s+([^:]+):\s*(.*)") {
                $processingMergePolicies = $false
                if ($matches[1] -eq "Update Frequency") {
                    $updateFrequency = $matches[2]
                    continue
                }
                if ($matches[1] -eq "Enabled") {
                    $enabled = $matches[2]
                    continue
                }
                if ($matches[1] -eq "Batchable") {
                    $batchable = $matches[2]
                    continue
                }
                if ($matches[1] -eq "Merge Policies") {
                    $mergePolicies = $matches[2]
                    $processingMergePolicies = $true
                    continue
                }
            }
            if ($processingMergePolicies) {
                $mergePolicies += $line
                continue
            }
        }

        if ($null -ne $fromRepo) {
            $list += @{fromRepo = $fromRepo; fromChannel = $fromChannel; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; ignoreChecks = @($this.ParseIgnoreChecks($mergePolicies)); mergePolicies = @($this.ParseMergePolicies($mergePolicies)) };
        }

        return $list
    }

    [object[]] GetSubscriptions([string]$repo, [string]$branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $output = $this.Execute($arguments, $false)
        $subscriptions = @($this.ParseSubscriptions($output))
        return $subscriptions
    }


    [void]AddSubscription($repo, $branch, $item) {
        $arguments = @("add-subscription", "--channel", $item.fromChannel, "--source-repo", $item.fromRepo, "--target-repo", $repo, "--update-frequency", $item.updateFrequency, "--target-branch", $branch, "--no-trigger", "-q")
        $policiesArguments = @("set-repository-policies", "--repo", $repo, "--branch", $branch, "-q")
        $targetArgumentsRef = [ref]$arguments
        if ($item.batchable -eq "True") {
            $arguments += "--batchable"
            $targetArgumentsRef = [ref]$policiesArguments
        }
        if ($item.mergePolicies -contains "Standard") {
            $targetArgumentsRef.value += "--standard-automerge"
        }
        if ($item.mergePolicies -like "NoRequestedChanges") {
            $targetArgumentsRef.value += "--no-requested-changes"
        }
        if ($item.mergePolicies -like "NoExtraCommits") {
            $targetArgumentsRef.value += "--no-extra-commits"
        }
        if ($item.mergePolicies -like "AllChecksSuccessful") {
            $targetArgumentsRef.value += "--all-checks-passed"
        }
        if ($item.ignoreChecks.length -gt 0) {
            $targetArgumentsRef.value += "--ignore-checks"
            $targetArgumentsRef.value += $item.ignoreChecks -join ","
        }

        if ($item.batchable -eq "True") {
            $this.Execute($policiesArguments, $true)
        }

        $output = $this.Execute($arguments, $true)

        if ($output -match "Successfully created new subscription with id '([^']+)'.") {
            $id = $matches[1]
            if ($item.enabled -eq [bool]::FalseString) {
                $this.DisableSubscription($id)
            }
        }
        else {
            Write-Error("    WARNING: {0}" -f $output)
        }
    }

    [void]DeleteSubscriptions($repo, $branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $output = $this.Execute($arguments, $false)
        if (-not ($output -match "^No subscriptions found matching the specified criteria.")) {
            Write-Host ("Deleting subscriptions for {0} {1}" -f $repo, $branch)
            $arguments = @("delete-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch, "-q")
            $this.Execute($arguments, $true)
        }
    }

    [void]CreateDefaultChannel($repo, $branch, $channel) {
        Write-Host ("Creating default channel {2} for branch {0} {1}" -f $repo, $branch, $channel)
        $arguments = @("add-default-channel", "--repo", $repo, "--branch", $branch, "--channel", $channel, "-q")
        $this.Execute($arguments, $true)
    }

    [void]DisableSubscription ([string] $id) {
        Write-Host ("Disabling subscription {0}" -f $id)
        $arguments = @("subscription-status", "--id", $id, "-d", "-q")
        $this.Execute($arguments, $true)
    }

    [string[]]GetTargetSubscriptionIds ([string] $repo, [string] $branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $ids = $this.Execute($arguments, $false) | Select-String -AllMatches -Pattern "\s+-\s+Id:\s+([^\s]+)" |  ForEach-Object { $_.Matches } | Foreach-Object { $_.Groups[1].Value }
        return $ids
    }

    [void]DisableTargetSubscriptions ([string] $repo, [string] $branch) {
        Write-Host "Disabling targeting subscriptions for $repo ($branch)"

        $ids = $this.GetTargetSubscriptionIds($repo, $branch)
        ForEach ($id in $ids) {
            $this.DisableSubscription($id)
        }
    }

    [Hashtable[]]GetDefaultChannels ([string] $repo, [string] $branch) {
        $arguments = @("get-default-channels", "--source-repo", $repo, "--branch", $branch)
        $output = $this.Execute($arguments, $true)
        $records = @($output | Select-String -AllMatches -Pattern "\((\d+)\)\s+$repo\s+@\s+$branch\s+->\s+(.*)\b" |  ForEach-Object { $_.Matches } |  ForEach-Object { @{id = $_.Groups[1].Value; channel = $_.Groups[2].Value } })
        return $records
    }

    [void]DeleteDefaultChannel([string] $id) {
        Write-Host ("Deleting default channel {0}" -f $id)
        $arguments = @("delete-default-channel", "--id", $id)
        $this.Execute($arguments, $true)
    }

    [void]DeleteDefaultChannels([string] $repo, [string] $branch) {
        $channels = @($this.GetDefaultChannels($repo, $branch))
        ForEach ($item in $channels) {
            $this.DeleteDefaultChannel($item.id)
        }
    }

    [Hashtable]GetRepoConfig([string] $repo, [string] $newBranch, [string] $oldBranch) {
        $defaultChannels = @($this.GetDefaultChannels($repo, $oldBranch) | ForEach-Object { $_.channel })
        $subscriptions = @($this.GetSubscriptions($repo, $oldBranch))
        $config = @{repo = $repo; newBranch = $newBranch; oldBranch = $oldBranch; defaultChannels = $defaultChannels; subscriptions = $subscriptions; }
        return $config
    }

    [void]MigrateRepo([PSCustomObject]$config) {
        Write-Host (">>>Migrating repository {0} {1} ==> {2}..." -f $config.repo, $config.oldBranch, $config.newBranch)

        $this.DeleteDefaultChannels($config.repo, $config.oldBranch)
        ForEach ($channel in $config.defaultChannels) {
            $this.CreateDefaultChannel($config.repo, $config.newBranch, $channel)
        }

        $this.DeleteSubscriptions($config.repo, $config.oldBranch)
        $this.DeleteSubscriptions($config.repo, $config.newBranch)

        Write-Host ("Adding subscriptions")
        ForEach ($item in $config.subscriptions) {
            $this.AddSubscription($config.repo, $config.newBranch, $item)
        }
    }

    [void]VerifyRepo([PSCustomObject]$config) {
        Write-Host (">>>Verifying repository {0} {1} ==> {2}..." -f $config.repo, $config.oldBranch, $config.newBranch)
        if ($this.GetDefaultChannels($config.repo, $config.oldBranch).length -ne 0) {
            throw("Default channels for old branch haven't been removed.")
        }
        if ($this.GetTargetSubscriptionIds($config.repo, $config.oldBranch).length -ne 0) {
            throw("Subscriptions for old branch haven't been removed.")
        }

        $actualConfig = $this.GetRepoConfig($config.repo, $config.oldBranch, $config.newBranch)
        if ($actualConfig.defaultChannels.length -ne $config.defaultChannels.length) {
            throw("Subscriptions for old branch haven't been removed.")
        }

        $expectedDefaultChannels = ConvertTo-Json($actualConfig.defaultChannels | Sort-Object)
        $actualDefaultChannels = ConvertTo-Json($config.defaultChannels | Sort-Object)
        if ($expectedDefaultChannels -ne $actualDefaultChannels) {
            throw("Expected default channels {0} don't match actual {1}." -f $actualDefaultChannels, $actualDefaultChannels)
        }

        $actualSubscriptions = ConvertTo-Json($actualConfig.subscriptions | Sort-Object { $_.fromRepo })
        $expectedSubscriptions = ConvertTo-Json($config.subscriptions | Sort-Object { $_.fromRepo })

        if ($expectedSubscriptions -ne $actualSubscriptions) {
            throw("Expected subscriptions {0} don't match actual {1}." -f $expectedSubscriptions, $actualSubscriptions)
        }

        Write-Host ("Validation of {0} passed" -f $config.repo)
    }

    [string]Execute ([string[]] $arguments, [bool]$exitCodeCheck) {
        if ($this.DryRun -and ($arguments[0] -ne "get-default-channels") -and ($arguments[0] -ne "get-subscriptions")) {
            Write-Host (">>> darc {0}" -f ($arguments -join " "))
            return "Successfully created new subscription with id 'TEST_ID'."
        }
        else {
            $output = (&"darc"  $arguments | Out-String)
            if ($exitCodeCheck -and $LASTEXITCODE -ne 0) {
                throw ("    Error executing command ""darc {0}"" with status code {1}: {2}" -f ($arguments -join " "), $LASTEXITCODE, $output)
            }
            return $output
        }
    }
}
function  InitializeDarc {
    param (
        [DarcExecutor] $darc
    )
    $configFile = "m2m-dotnet_{0:yyyyMMdd_HHmmss}.json" -f (get-date)
    $internalRepo = "https://dev.azure.com/dnceng/internal/_git/{0}" -f ($Repository -replace "/", "-")
    $publicRepo = "https://github.com/{0}" -f $Repository

    Write-Host ("Creating configuration for repository {0} {1} ==> {2}..." -f $publicRepo, $OldBranch, $NewBranch)
    $configPublic = $darc.GetRepoConfig($publicRepo, $NewBranch, $OldBranch)

    Write-Host ("Creating configuration for repository {0} {1} ==> {2}..." -f $internalRepo, $OldBranch, $NewBranch)
    $configInternal = $darc.GetRepoConfig($internalRepo, $NewBranch, $OldBranch)

    $configs = @($configPublic, $configInternal)
    ConvertTo-Json $configs -Depth 4 | Out-File -FilePath $configFile
    Write-Host ("Configuration has been saved as {0}" -f $configFile)
}

function  DisableDarcSubscriptions {
    param (
        [DarcExecutor] $darc
    )

    $configs = Get-Content -Raw -Path $DataFile | ConvertFrom-Json
    ForEach ($config in $configs) {
        $darc.DisableTargetSubscriptions($config.repo, $config.oldBranch)
    }
}
function MigrateDarc {
    param (
        [DarcExecutor]$darc
    )

    $configs = Get-Content -Raw -Path $DataFile | ConvertFrom-Json
    ForEach ($config in $configs) {
        $darc.MigrateRepo($config)
    }
}
function VerifyDarc {
    param (
        [DarcExecutor]$darc
    )

    $configs = Get-Content -Raw -Path $DataFile | ConvertFrom-Json
    ForEach ($config in $configs) {
        $darc.VerifyRepo($config)
    }
}

$ErrorActionPreference = 'Stop'
$darc = [DarcExecutor]::new()
$darc.DryRun = $DryRun

switch ($PSCmdlet.ParameterSetName) {
    "GenerateDataFile" { InitializeDarc -darc $darc }
    "DisableSubscriptions" { DisableDarcSubscriptions -darc $darc }
    "Migrate" { MigrateDarc -darc $darc }
    "Verify" { VerifyDarc -darc $darc }
}