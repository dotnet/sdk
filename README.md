# Arcade Minimal CI Sample

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/dotnet/arcade-minimalci-sample/arcade-minimalci-sample-ci?branchName=master)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=209?branchName=master)

This repository serves as an example of how to link GitHub repositories to Azure DevOps for CI and PR builds.

## Before You Start

You'll want to start by following the [Azure DevOps Onboarding](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/AzureDevOpsOnboarding.md) instructions, which provide a thorough, step-by-step list of instructions for creating Azure DevOps CI builds for GitHub repos. From there, you'll find the [Azure DevOps YAML documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/get-started-yaml?view=vsts), which details the creation of Azure DevOps CI YAML files.

The purpose of this repository is to provide a jumping off point with an example YAML CI file that already has the basic architecture you'll want for your builds. All examples below are taken from this repository's [azure-pipelines.yml](azure-pipelines.yml).

## Set build triggers in your YAML

Documentation on setting CI triggers in YAML can be found [here](https://docs.microsoft.com/en-us/azure/devops/pipelines/build/ci-build-git?view=vsts&tabs=yaml#set-up-a-ci-trigger-for-a-topic-branch). The syntax for pull request triggers is identical, and will trigger whenever a PR is created merging into your specified branches.

**Note: YAML-based PR triggers are a feature currently being rolled out by Azure DevOps. Until they are completed, you must override the YAML PR trigger settings from the build definition GUI on Azure DevOps.**

```yaml
trigger:
- master

# Commenting out until Azure DevOps supports YAML PR triggers
# pr:
# - master
```

## Base your builds on Arcade for ease of use

Arcade is designed to make many of the more complex tasks (such as sending telemetry) simple to do out of the box. It is therefore recommended that all builds base themselves on Arcade's `base.yml` template. Today, this can be done by copying the `eng/common` folder from Arcade into a local `eng/common` folder.  In the near future, Engineering services will provide the capability to auto-update this folder via Maestro so that you don't need to manually take updates to common Arcade scripts.

```yaml
jobs:
- template: /eng/common/templates/job/job.yml
  parameters:
  ...
```

## Use the Arcade SDK for an easier build process

To quickstart your builds, you can use the Arcade SDK's build scripts. Clone the `eng/*` folder from this repository and copy [`Directory.Build.props`](Directory.Build.props), [`Directory.Build.targets`](Directory.Build.targets), [`global.json`](global.json), and [`NuGet.Config`](NuGet.Config) into your root directory. To use the build scripts, simply use a `script` task to run `eng\common\cibuild.cmd` on Windows or `eng/common/cibuild.sh` on a Unix-based OS.

```yaml
# for Windows
steps:
- script: eng\common\cibuild.cmd
    -configuration $(_BuildConfig)
    -prepareMachine

# for Unix-based
steps:
- script: eng/common/cibuild.sh
    --configuration $(_BuildConfig)
    --prepareMachine
```

Note: for the Unix-based scripts to work, make sure you clone rather than copy/paste while on Windows&mdash;copying and pasting will remove the `x` chmod parameter from the Unix scripts, which will build breaks when attempting to run them.

## Use matrices to quickly create phases for different build configurations

Azure DevOps supports using a **matrix** in a phase definition to quickly create several different phases on the same queue with slightly different build configurations. This is the recommended way to quickly add debug and release configuration builds.

```yaml
- phase: Windows
  queue:
    name: Helix
    parallel: 99
    matrix:
      debug_configuration:
        _BuildConfig: Debug
      release_configuration:
        _BuildConfig: Release
```

The variable defined in this matrix (in this case, `_BuildConfig`) can later be referenced in your build steps:

```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'build'
    projects: '**/*.csproj'
    arguments: '--configuration $(_BuildConfig)'
```

## Run both CI and PR builds out of the same file

While this sample repository has no need to do so, there are many scenarios in which you may want to differentiate between different build triggers. The current recommendation is that all repositories have a single `azure-pipelines.yml` file which defines all of their builds (CI, PR, and internal). To do this, use YAML `{{ if }}` directives and the Azure DevOps built-in `Build.Reason` variable.

```yaml
- ${{ if notIn(variables['Build.Reason'], 'PullRequest') }}:
  - task: DotNetCoreCLI@2
    inputs:
      command: 'publish'
      projects: 'HelloWorld/HelloWorld.csproj'
      publishWebProjects: false
      arguments: '--configuration $(_BuildConfig) --output $(build.ArtifactStagingDirectory) --framework $(targetFramework)'
    displayName: dotnet publish
```

## Enabling telemetry

[Arcade](#base-your-builds-on-arcade-for-ease-of-use) provides the ability to send telemetry.  To enable telemetry you must...

1. Set `enableTelemetry` to `true`

2. Define the `_HelixType`, `_HelixSource`, and `_HelixBuildConfig` variables

    - `_HelixType` - This is a string that defines the type of run you are currently performing. Note that a trailing slash is required. e.g. test/functional/cli/, build/product/
    - `_HelixSource` - This defines information about the run in a specific format Type/repo/branch/. Note that a trailing slash is required. e.g. pr/corefx/master/, official/coreclr/master/
    - `_HelixBuildConfig` - The build configuration for your current build ie, Release, Debug, etc

3. For official builds, add an "AzureKeyVault" task reference to `HelixProdKV`

```YAML
jobs:
- template: /eng/common/templates/job/job.yml
  parameters:
    agentOs: Windows_NT
    name: Windows_NT
    enableTelemetry: true

    variables:
      _HelixType: build/product
      _HelixBuildConfig: $(_BuildConfig)
      ${{ if notIn(variables['Build.Reason'], 'IndividualCI', 'BatchedCI', 'PullRequest') }}:
        _HelixSource: official/dotnet/arcade-minimalci-sample/$(Build.SourceBranch)
      ${{ if in(variables['Build.Reason'], 'IndividualCI', 'BatchedCI', 'PullRequest') }}:
        _HelixSource: pr/dotnet/arcade-minimalci-sample/$(Build.SourceBranch)

    steps:
    - ${{ if notIn(variables['Build.Reason'], 'IndividualCI', 'BatchedCI', 'PullRequest') }}:
      - task: AzureKeyVault@1
        inputs:
          azureSubscription: 'HelixProd_KeyVault'
          KeyVaultName: HelixProdKV
          SecretsFilter: 'HelixApiAccessToken'
        # conditions - https://docs.microsoft.com/en-us/azure/devops/pipelines/process/conditions?view=vsts
        condition: always()
```

## Use Helix for cross-platform testing

Arcade integrates with Helix, making it easy to do cross-platform testing at scale. To get started, you'll need to reference the Helix SDK in your [`global.json`](global.json):

```json
"msbuild-sdks": {
  "Microsoft.DotNet.Helix.Sdk": "<most-recent-version>"
}
```

Finally, reference the `send-to-helix.yml` template after your build step. Make sure to do it for each phase.

```yaml
# defining your variables first makes them easy to reuse between queues
variables:
  - name: _HelixType
    value: build/product
  - name: _HelixSource
    value: pr/dotnet/arcade-minimalci-sample/$(Build.SourceBranch)
  - name: _HelixTestType
    value: test/product/
  - name: _XUnitProject  # the reference(s) to the XUnit project(s) you wish to test, semicolon delimited
    value: $(Build.SourcesDirectory)/HelloTests/HelloTests.csproj
  - name: _XUnitTargetFramework # the xUnit framework you want to test on
    value: netcoreapp2.0
  - name: _XUnitRunnerVersion # the version of the xUnit runner you wish to use
    value: 2.4.1
  - name: _DotNetCliPackageType # either 'runtime' or 'sdk', depending on which you want to bootstrap onto the Helix machine
    value: sdk
  - name: _DotNetCliVersion # the version of the dotnet cli you want to bootstrap (probably the same as the one in your global.json!)
    value: 2.1.403
# ...
# steps:
# build step here
- template: /eng/common/templates/steps/send-to-helix.yml
  parameters:
    HelixSource: $(_HelixSource)
    HelixType: $(_HelixTestType)
    HelixTargetQueues: Windows.10.Amd64.Open;Windows.7.Amd64.Open  # set queues appropriately for the machine you're building on
    XUnitProjects: $(Build.SourcesDirectory)/HelloTests/HelloTests.csproj
    XUnitTargetFramework: $(_XUnitTargetFramework)
    XUnitRunnerVersion: $(_XUnitRunnerVersion)
    IncludeDotNetCli: true
    DotNetCliPackageType: $(_DotNetCliPackageType)
    DotNetCliVersion: $(_DotNetCliVersion)
    EnableXUnitReporter: true
    WaitForWorkItemCompletion: true
    condition: eq(variables['_BuildConfig'], 'Debug')
```

For more information, see the main documentation on sending jobs to Helix [here](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/SendingJobsToHelix.md).

## Using the SignToolTask

Arcade provides an optimized way to sign files using MicroBuild, it is wrapped in a custom MSBuild task called [SignToolTask](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.SignTool/src/SignToolTask.cs).

The Arcade SDK will automatically [find package](https://github.com/dotnet/arcade/blob/ae38bbbc25d03e1deb49b15ce88e2dd4c683e116/src/Microsoft.DotNet.Arcade.Sdk/tools/Sign.proj) files and forward them to be signed using SignToolTask. Therefore, if the only files that you care to sign are covered by the linked line above you don't have to do anything else. If not, you have options. You can specify explicit files to be signed / excluded from signing or changing the certificate / strong name to be used. For a detailed guide see the [SignTool package documentation](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.SignTool/README.md).
