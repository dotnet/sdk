# Arcade Minimal CI Sample
 [![Build status](https://dotnet.visualstudio.com/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_apis/build/status/116?branchName=master)](https://dotnet.visualstudio.com/public/_build/latest?definitionId=116&branch=master)

This repository serves as an example of how to link GitHub repositories to VSTS for CI and PR builds.

## Before You Start
You'll want to start by following the [VSTS Onboarding](https://github.com/dotnet/arcade/blob/master/Documentation/VSTS/VSTSOnboarding.md) instructions, which provide a thorough, step-by-step list of instructions for creating VSTS CI builds for GitHub repos. From there, you'll find the [VSTS YAML documentation](https://github.com/Microsoft/vsts-agent/blob/master/docs/preview/yamlgettingstarted.md), which details the creation of VSTS CI YAML files.

The purpose of this repository is to provide a jumping off point with an example YAML CI file that already has the basic architecture you'll want for your builds. All examples below are taken from this repository's [.vsts-ci.yml](.vsts-ci.yml).

## Set build triggers in your YAML
Documentation on setting CI triggers in YAML can be found [here](https://github.com/Microsoft/vsts-agent/blob/master/docs/preview/yamlgettingstarted-ci.md). The syntax for pull request triggers is identical, and will trigger whenever a PR is created merging into your specified branches.

**Note: YAML-based PR triggers are a feature currently being rolled out by VSTS. Until they are completed, you must override the YAML PR trigger settings from the build definition GUI on VSTS.**

```yaml
trigger:
- master

# Commenting out until VSTS supports YAML PR triggers
# pr:
# - master
```

## Run builds on multiple operating systems with phases
VSTS uses **phases** to parallelize your builds. Each phase definition corresponds to a specific queue. Thus, if you would like to have builds run on Windows, OSX, and Linux, you will need to define three phases (each corresponding to a separate queue) as seen in the following example.

```yaml
phases:
  # Define a Windows phase
  - phase: Windows
    queue:
      name: Helix # the Helix queue is currently the recommended queue for Windows builds
    ...

  - phase: OSX
    queue:
      name: Hosted macOS Preview # as of Aug 2 2018 this is correct; in the future we will switch to a DotNetCore-Mac pool
    ...

  - phase: Linux
    queue:
      name: DotNetCore-Linux
    ...
```

## Use matrices to quickly create phases for different build configurations
VSTS supports using a **matrix** in a phase definition to quickly create several different phases on the same queue with slightly different build configurations. This is the recommended way to quickly add debug and release configuration builds.

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

## Avoid code-duplication through templates
Most builds run nearly identical steps on all configurations. In YAML, **templates** can be used to prevent code duplication and make the build process easier to maintain.

In this sample, our build process is placed in a [build.yml](eng/build.yml) template file. This template is then referenced in the main CI file:

```yaml
- phase: Windows
  ...
  steps:
  - template: eng/build.yml

- phase: OSX
  ...
  steps:
  - template: eng/build.yml

- phase: Linux
  ...
  steps:
  - template: eng/build.yml
```

## Run both CI and PR builds out of the same file
The current recommendation is that all repositories have a single `.vsts-ci.yml` file which defines all of their builds (CI, PR, and internal). To do this, use YAML `{{ if }}` directives and the VSTS built-in `Build.Reason` variable.

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
