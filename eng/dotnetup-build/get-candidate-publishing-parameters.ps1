# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('daily', 'preview')]
    [string] $Quality
)

# Construct this at runtime, outside Azure DevOps task inputs and pipeline variables.
# The publishing agent must resolve its own staging-directory macro.
'/p:BuildQuality={0} /p:BlobBasePath="$(Build.ArtifactStagingDirectory)/BlobArtifacts/{0}/"' -f $Quality
