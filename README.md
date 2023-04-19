## Welcome to dotnet sdk

This repository contains core functionality needed to create .NET projects that is shared between Visual Studio and the [.NET CLI](https://learn.microsoft.com/dotnet/core/tools/).

* MSBuild tasks are under [/src/Tasks/Microsoft.NET.Build.Tasks/](src/Tasks/Microsoft.NET.Build.Tasks).

See [dotnet/project-system](https://github.com/dotnet/project-system) for the project system work that is specific to Visual Studio.

Common project and item templates are found in [template_feed](https://github.com/dotnet/sdk/tree/main/template_feed).

## Build status

|Windows x64 |
|:------:|
|[![](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/sdk/DotNet-Core-Sdk%203.0%20(Windows)%20(YAML)%20(Official))](https://dev.azure.com/dnceng/internal/_build?definitionId=140)|

## Installing the SDK
[Official builds](https://dotnet.microsoft.com/download/dotnet-core)

[Latest builds](https://github.com/dotnet/installer#installers-and-binaries)

## How do I engage and contribute?

We welcome you to try things out, [file issues](https://github.com/dotnet/sdk/issues), make feature requests and join us in design conversations. Be sure to check out our [project documentation](documentation)

This project has adopted the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct) to clarify expected behavior in our community.

## How do I build the SDK?

Start with the [Developer Guide](documentation/project-docs/developer-guide.md).

## How do I test an SDK I have built?

To test your locally built SDK, run `eng\dogfood.cmd` after building. That script starts a new Powershell with the environment configured to redirect SDK resolution to your build.

From that shell your SDK is available in:

- any Visual Studio instance launched via `& devenv.exe`
- `dotnet build`
- `msbuild`

## How do I determine the timeline I must follow to get my changes in for a specific version of .NET?

Please see the [Pull Request Timeline Guide](documentation/project-docs/SDK-PR-guide.md).

## How we triage and review PRs

With the SDK repository being the home for many different areas, we've started trying to label incoming issues for the area they are related to using `Area-` labels.  Then we rely on the [codeowners](https://github.com/dotnet/sdk/blob/main/CODEOWNERS) to manage and triages issues in their areas. Feel free to contact the owners listed in that file if you're not getting a response on a particular issue or PR. Please try to label new issues as that'll help us route them faster.

For PRs, we assign a reviewer once a week on Wednesday, looking only at PRs that are green in the build.  If you are contributing:

* Get the PR green.
* Include a test if possible.
* Mention  `@dotnet-cli` if you want to raise visibility of the PR.
