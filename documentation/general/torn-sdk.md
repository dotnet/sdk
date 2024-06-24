# Torn .NET SDK

## Terminology

- msbuild: refers the .NET Framework based msbuild included with Visual Studio.
- dotnet msbuild: refers to the .NET Core based msbuild included with the .NET SDK.
- analyzers: this document will use analyzers to refer to both analyzers and generators.

## Summary

Visual Studio and .NET SDK are separate products but they are intertwined in command line and design time build scenarios as different components are loaded from each product.

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From Visual Studio | From .NET SDK |
| dotnet msbuild | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Both |

Generally this mixing of components is fine because Visual Studio will install a .NET SDK that is functionally paired with it. That is the compiler and analyzers are the same hence mixing poses no real issue. For example 17.10 installs .NET SDK 8.0.3xx, 17.9 installs .NET SDK 8.0.2xx, etc ... However when the .NET SDK is not paired with the Visual Studio version,then compatibility issues can, and will, arise. Particularly when the .NET SDK is _newer_ than Visual Studio customers will end up with the following style of error:

> CSC : warning CS9057: The analyzer assembly '..\dotnet\sdk\8.0.200\Sdks\Microsoft.NET.Sdk.Razor\source-
generators\Microsoft.CodeAnalysis.Razor.Compiler.SourceGenerators.dll' references version '4.9.0.0' of the compiler, which is newer
than the currently running version '4.8.0.0'.

This torn state is a common customer setup:

|Visual Studio Version | Match% | Float % | Torn % |
| --- | --- | --- | --- |
| 16.11 | 81.4% | 10.8% | 7.8% |
| 17.2 | 91% | 8% | 1% |
| 17.4 | 91.5% | 5.7% | 2.7% |
| 17.6 | 91.6% | 5% | 3.5% |
| 17.7 | 94.9% | 2.2% | 3% |
| 17.8 | 96% | 0% | 4% |

To address this issue we are going to separate Visual Studio and the .NET SDK in build scenarios. That will change our matrix to the following:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From .NET SDK | From .NET SDK |
| dotnet msbuild | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Visual Studio |

Specifically we will be

1. Changing msbuild to use a compiler from the .NET SDK
2. Changing Visual Studio to use analyzers from Visual Studio

In addition to making our builds more reliable this will also massively simplify our [analyzer Development strategy][sdk-lifecycle]. Analyzers following this model can always target the latest Roslyn version without the need for complicated multi-targeting.

## Motivations

There are a number of scenarios where customers end up in a torn state. The most common is when customers have a CI setup with the following items:

1. They use a task like [actions/setup-dotnet][setup-dotnet] to install the .NET SDK and use a flexible version format like `8.x`.
2. They use `msbuild` to actually drive their build. This `msbuild` comes from the Visual Studio installed on the machine image.

This means that CI systems are updated to the latest .NET SDK virtually as soon as we release them. However the version of Visual Studio is updated much later as CI images usually take several weeks to upgrade to a new Visual Studio version. This is a very common CI setup and means a significant number of our customers end up in a torn state for several weeks.

Another reason is that teams use older Visual Studio versions due to internal constraints: like an organizational policy. At the same time they install the latest .NET SDK which puts them into a torn state.

This also hits any customer that uses a preview version of .NET SDK. These inherently represent a torn SDK state because they almost never match the compiler in Visual Studio This results in blockers for big teams like Bing from testing out our previews.

## Goals

This design has a number of goals:

1. The `msbuild` and `dotnet build` build experience should be equivalent.
1. The Visual Studio Design time experience is independent of the .NET SDK installed.
1. To make explicit that it is okay, and even expected, that the design time and command line build experiences can differ when the SDK is in a torn state.

## MSBuild using .NET SDK Compiler

The .NET SDK will start producing a package named Microsoft.Net.Sdk.Compilers.Toolset. This will contain a .NET Framework version of the Roslyn compiler that matches .NET Core version. This will be published as a part of the .NET SDK release process meaning it's available for all publicly available .NET SDKs.

The .NET SDK has the capability to [detect a torn state][pr-detect-torn-state]. When this is detected the matching Microsoft.Net.Sdk.Compilers.Toolset package for this version of the .NET SDK will be downloaded via `<PackageDownload>`. Then the `$(RoslynTargetsPath)` will be reset to point into the package contents. This will cause MSBuild to load the Roslyn compiler from that location vs. the Visual Studio location.

One downside to this approach is that it is possible to end up with two VBCSCompiler server processes. Consider a solution that has a mix of .NET SDK style projects and non-SDK .NET projects. In a torn the .NET SDK projects will use the .NET SDK compiler and non-SDK .NET projects will use the Visual Studio compiler. While not a desirable outcome, it is a correct one. Customers who wish to only have one VBCSCompiler should correct the torn state in their build tools.

## Visual Studio using Visual Studio Analyzers

### .NET SDK in box analyzers dual insert

Analyzers which ship in the .NET SDK box will change to having a copy checked into Visual Studio. When .NET SDK based projects are loaded at design time, the Visual Studio copy of the analyzer will be loaded. Roslyn already understands how to prefer Visual Studio copies of analyzers. That work will need to be extended a bit but that is pretty straight forward code.

This approach enables us to take actions like NGEN or R2R analyzers inside of Visual Studio. This is a long standing request from the Visual Studio perf team but very hard to satisfy in our current

This approach is already taken by [the Razor generator][code-razor-vs-load]. This work was done for other reliability reasons but turned out working well for this scenario. There is initial upfront work to get the Visual Studio copy loading in design time builds but it has virtually zero maintanence cost.

This also means these analyzers can vastly simplify their development model by always targeting the latest version of Roslyn. There is no more need to multi-target because the version of the compiler the analyzer will be used with is known at ship time for all core build scenarios.

This does mean that our design time experience can differ from our command line experience when customers are in a torn state. Specifically it's possible, even likely in some cases, that the diagonstics produced by design time builds will differ from command line builds. That is a trade off that we are willing to make for reliability. Customers who wish to have a consistent experience between design time should not operate in a torn state.

## .NET SDK in box analyzers target oldest

Analyzers which have no need to target the latest Roslyn could instead choose to target the oldest supported version of Roslyn. That ensures they can safely load into any supported scenario without issue.

This strategy should be considered short term. The Roslyn API is under constant development to respond to the performance demands of Visual Studio. New APIs can quickly become virtually mandatory for analyzers and generators to use and will only be available on latest. At that point the analyzer will need to also update to use dual insertions.

### NuGet based analyzers

Analyzers which ship via NuGet will continue to following the existing [support policies][matrix-of-paine]. This means that they will either need to target a sufficiently old version of Roslyn or implement multi-targeting support.

In the case of [multi-targeting][issue-analyzer-mt] this proposal is effectively a no-op. Analyzer multi-targeting is already based off of Roslyn versions, not .NET SDK versions, hence the proper version of the analyzer will still load in a torn state. The version of the Roslyn compiler in a torn state for msbuild is the same used for dotnet build hence it's already a scenario the analyzer needs to support.

## Alternative

### .NET SDK in box analyzers multi-target

Technically in box analyzers can have a multi-targeting strategy just as NuGet based analyzers do. This is actively discouraged because it leads to unnecessary increases in .NET SDK sizes. The time spent implementing multi-targeting is likely better spent moving to a dual insertion to keep .NET SDK sizes down and provide a consistent experience with other analyzers.

### PackageReference

Instead of `<PackageDownload>` the toolset package could be installed via `<PackageReference>`. This is how the existing Microsoft.Net.Compilers.Toolset package works today. This has a number of disadvantages:

1. PackageReferences are not side effect free and have been shown to cause issues in complex builds. For example this package did not work by default in the Visual Studio or Bing builds. Deploying this at scale to all our customers will almost ceratinly cause issues.
2. This approach does not solve all scenarios. The mechanics of restore mean that the compiler is not swapped until part way through the build. Build operations that happen earlier, such as [legacy WPF][issue-legacy-wpf], will still use the Visual Studio compiler and hence experience analyzer compatibility issues.
3. Mixing builds that do and do not restore can lead to different build outcomes as they can end up choosing different compilers. Mixing the version of msbuild / dotnet msbuild can also lead to strange outcomes as they can end up choosing different compilers.

### Install the Framework compiler

An alternative is simply shipping both Roslyn compilers in the Windows .NET SDK. This means there is no need for a `<PackageDownload>` step as the compiler is always there. This is a simpler build but it means that all .NET SDK installs on Windows will incease by ~6-7%.

This approach does have downsides:

1. The Windows .NET SDK increased in size even when it's not needed. For example our main Windows containers do not install Visual Studio hence are never in a torn state yet they'll see increased size cost here. Changing the .NET SDK such that these extra compilers are only installed sometimes would add significant complexity to our installer setup.
2. This will make the Windows and Linux .NET SDK's visibly different in layout. Comparing them will show different contents which could be confusing to customers. At the same time the current proposal is functionally the same, it just changes where the framework compilers are placed on disk and that also could be confusing to customers.

### Use the .NET Core compiler

Instead of downloading a .NET Framework Roslyn in a torn state, the SDK could just use the .NET Core Roslyn it comes with. That would have virtually no real compatibility issues. This has a number of disadvantages:

1. Changes the process name from VBCSCompiler to `dotnet`. That seems insignificant but the process name is important as it's referenced in a lot of build scripts. This would be a subtle breaking change that we'd have to work with customers on.
2. This would make Visual Studio dependent on the servicing lifetime of the .NET Core runtime in the SDK. That has a very different lifetime and could lead to surprising outcomes down the road. For example if policy removed out of support runtimes from machines it would break the Visual Studio build.

## Related Issues

- [Roslyn tracking issue for torn state](https://github.com/dotnet/roslyn/issues/72672)
- [MSBuild property for torn state detection](https://github.com/dotnet/installer/pull/19144)
- [Long term build-server shutdown issue](https://github.com/dotnet/msbuild/issues/10035)

[microsoft-common-tasks]: https://github.com/dotnet/msbuild/blob/main/src/Tasks/Microsoft.Common.tasks#L106-L109
[matrix-of-paine]: https://aka.ms/dotnet/matrix-of-paine
[sdk-lifecycle]: https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle
[code-razor-vs-load]: https://github.com/dotnet/roslyn/blob/9aea80927e3d4e5a2846efaa710438c0d8d2bfa2/src/Workspaces/Core/Portable/Workspace/ProjectSystem/ProjectSystemProject.cs#L1009
[setup-dotnet]: https://github.com/actions/setup-dotnet
[pr-detect-torn-state]: https://github.com/dotnet/installer/pull/19144
[issue-analyzer-mt]: https://github.com/dotnet/sdk/issues/20355
[issue-legacy-wpf]: https://github.com/dotnet/wpf/issues/9112

## Work Breakdown

This will be moved to an issue when this PR is opened against dotnet/sdk proper:

- [ ]: Flow the Micosoft.Net.Compilers.Toolset.Framework package to the .NET SDK
- [ ]: Create a new package Micosoft.Microsoft.Net.Sdk.Compilers.Toolset in .NET SDK.
  - [ ]: The contents of this package will include the contents of the `tasks\net472` folder in the Microsoft.Net.Compilers.Toolset.Framework package. This subset is all that is needed and makes the package not usable via `<PackageReference>`. The latter reduces the incentive for customers to use it directly.
  - [ ]: The contents will include a README.md stating the package is **not** supported for direct user consumption.
  - [ ]: The package will follow the versioning scheme of the .NET SDK.
  - [ ]: The package will be unlisted (ideal but not a hard requirement)
- [ ]: Change the Sdk.targets file to have copies of the following three `<UsingTasks>` from [Microsoft.Common.tasks][microsoft-common-tasks]. Having a copy in Sdk.targets means that resetting `$(RoslynTargetsPath)` during build will change the chosen compiler.
- [ ]: When the .NET SDK detects a torn state
  - [ ]: Use a `<PackageDownload>` to acquire the Microsoft.Net.Sdk.Compilers.Toolset package
  - [ ]: Change `$(RoslynTargetsPath)` to point into the package contents
