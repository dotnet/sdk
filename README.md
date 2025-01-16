## Welcome to dotnet sdk

This repository contains core functionality needed to create .NET projects that are shared between Visual Studio and the [.NET CLI](https://learn.microsoft.com/dotnet/core/tools/).

* MSBuild tasks are under [/src/Tasks/Microsoft.NET.Build.Tasks/](src/Tasks/Microsoft.NET.Build.Tasks).

See [dotnet/project-system](https://github.com/dotnet/project-system) for the project system work that is specific to Visual Studio.

Common project and item templates are found in [template_feed](https://github.com/dotnet/sdk/tree/main/template_feed).

## Build status

Visibility|All jobs|
|:------|:------|
|Public|[![Status](https://dev.azure.com/dnceng-public/public/_apis/build/status/101)](https://dev.azure.com/dnceng-public/public/_build?definitionId=101)|
|Microsoft Internal|[![Status](https://dev.azure.com/dnceng/internal/_apis/build/status/140)](https://dev.azure.com/dnceng/internal/_build?definitionId=140)|

## Installing the SDK

You can download the .NET SDK as either an installer (MSI, PKG) or a zip (zip, tar.gz). The .NET SDK contains both the .NET runtime and CLI tools.

- [Official builds](https://dotnet.microsoft.com/download/dotnet)
- [**Latest builds table**](documentation/package-table.md)

> [!NOTE]
> When acquiring installers from the latest builds table, be aware that the installers are the **latest bits**. With development builds, internal NuGet feeds are necessary for some scenarios (for example, to acquire the runtime pack for self-contained apps). You can use the following NuGet.config to configure these feeds. See the following document [Configuring NuGet behavior](https://docs.microsoft.com/nuget/consume-packages/configuring-nuget-behavior) for more information on where to modify your NuGet.config to apply the changes.

### For .NET 10 builds
```xml
<configuration>
  <packageSources>
    <add key="dotnet9" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

### For .NET 9 builds
```xml
<configuration>
  <packageSources>
    <add key="dotnet9" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

### For .NET 8 builds
```xml
<configuration>
  <packageSources>
    <add key="dotnet8" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

### Debian package dependencies

Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing the SDK from the .deb file (via dpkg or similar), then you'll need to install the corresponding dependencies first:
- [Host, Host FX Resolver, and Shared Framework](https://github.com/dotnet/runtime/blob/main/docs/project/dogfooding.md#daily-builds-table)
- [ASP.NET Core Shared Framework](https://github.com/dotnet/aspnetcore/blob/main/docs/DailyBuilds.md)

### Looking for dotnet-install sources?

Sources for dotnet-install.sh and dotnet-install.ps1 are in the [install-scripts repo](https://github.com/dotnet/install-scripts).

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

For issues related to the central SDK team, typically they are assigned out to a team member in the first half of each week. Then each member is asked to review and mark those needing further discussion as "needs team triage" and otherwise setting a milestone for the issue. Backlog means we will consider it in the future if there is more feedback. Discussion means we have asked for more information from the filer. All other milestones indicate our best estimate for when a fix will be targeted for noting that not all issues will get fixed. If you are not getting a quick response on an issue assigned to a team member, please ping them.

The example query used for triage of .NET SDK issues can be viewed [here](https://github.com/dotnet/sdk/issues?q=is%3Aissue+is%3Aopen+-label%3AArea-NuGet+-label%3AArea-format+-label%3AArea-implicitusings+-label%3AArea-SourceBuild+-label%3AArea-Host+-label%3AArea-NativeAOT+-label%3AArea-readytorun+-label%3AArea-websdk+-label%3AArea-watch+-label%3AArea-illink+-label%3AArea-aspnetcore+-label%3AArea-compatibility+-label%3A%22Area-dotnet+test%22+-label%3AArea-FSharp+-label%3AArea-GenAPI+-label%3AArea-ApiCompat+label%3Auntriaged+no%3Amilestone+no%3Aassignee+)

For PRs, we assign a reviewer once a week on Wednesday, looking only at PRs that are green in the build.  If you are contributing:

* Get the PR green.
* Include a test if possible.
* Mention  `@dotnet-cli` if you want to raise visibility of the PR.

## License

The .NET SDK project uses the [MIT license](LICENSE.TXT).

The *LICENSE.txt* and *ThirdPartyNotices.txt* in any downloaded archives are authoritative.
