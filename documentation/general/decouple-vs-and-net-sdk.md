# Decoulping the .NET SDK and Visual Studio

## Goals

Visual Studio and the .NET SDK are separate products, but their experiences are tightly coupled. Changes to the .NET SDK version can influence Visual Studio's design-time behavior, while updating Visual Studio can impact command-line builds for .NET SDK projects. When the products are in sync these interactions are benign, but when they are not, they lead to compatibility and reliability issues. This document proposes a design to decouple these products, allowing each to provide a more defined and independent experience.

Design Goals:

1. **Consistent Build Experiences**: The `msbuild` and `dotnet msbuild` command line build experience for a solution should be functionally equivalent.
2. **Independent Design Time Behavior**: Visual Studio's design-time experience should be independent of the .NET SDK used by the solution.
3. **Clear Handling of Divergence**: To make explicit that it is okay, and even expected, that the design time and command line build experiences can differ when the two products are not in sync.

## Terminology

- **msbuild**: refers the .NET Framework based msbuild included with Visual Studio.
- **dotnet msbuild**: refers to the .NET Core based msbuild included with the .NET SDK.
- **analyzers**: this document will use analyzers to refer to both analyzers and generators.
- **torn state**: when the .NET SDK and Visual Studio are not in sync.
- **.NET SDK project**: a project that uses the .NET SDK project format.
- **anaylzers**: refer to both analyzers and generators unless otherwise specified.

## Summary

Visual Studio and .NET SDK are separate products but they are intertwined in command line and design time build scenarios as different components are loaded from each product. This table represents how intertwined Roslyn specifically is currently:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From Visual Studio | From .NET SDK |
| dotnet msbuild | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Both |

There are other products that have similar intertwined behavior such as NuGet, MSBuild, Razor, etc ... Roslyn is generally the most impactful and the one we are focusing on in this document as it's path forward can be generalized to the other products.

Generally this mixing of components is fine because Visual Studio will install a .NET SDK that is functionally paired with it. For example 17.10 installs .NET SDK 8.0.3xx, 17.9 installs .NET SDK 8.0.2xx, etc ... In that scenario the components are the same versio and the mixing is largely benign. However when the customer is in a torn state, .NET SDK used is not paired with the Visual Studio version, then compatibility issues can, and **will**, arise. Particularly when the .NET SDK is _newer_ than Visual Studio customers will end up with the following style of error:

> CSC : warning CS9057: The analyzer assembly '..\dotnet\sdk\8.0.200\Sdks\Microsoft.NET.Sdk.Razor\source-
> generators\Microsoft.CodeAnalysis.Razor.Compiler.SourceGenerators.dll' references version '4.9.0.0' of the compiler, which is newer
> than the currently running version '4.8.0.0'.

This torn state is a common customer setup:

|Visual Studio Version | Match% | Float % | Torn % |
| --- | --- | --- | --- |
| 16.11 | 81.4% | 10.8% | 7.8% |
| 17.2 | 91% | 8% | 1% |
| 17.4 | 91.5% | 5.7% | 2.7% |
| 17.6 | 91.6% | 5% | 3.5% |
| 17.7 | 94.9% | 2.2% | 3% |
| 17.8 | 96% | 0% | 4% |

To address this issue we are going to separate Visual Studio and the .NET SDK in build scenarios. That will change our matrix to be the following:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From .NET SDK | From .NET SDK |
| dotnet msbuild | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Visual Studio |

Specifically we will be

1. Changing msbuild to use tools and tasks from the .NET SDK when building .NET SDK projects.
2. Changing Visual Studio to use analyzers installed with Visual Studio.

In addition to making our builds more reliable this will also massively simplify our [analyzer Development strategy][sdk-lifecycle]. Analyzers in the SDK following this model can always target the latest Roslyn version without the need for complicated multi-targeting. Further it will allow Visual Studio the ability to NGEN or R2R analyzers which is a long standing request from the Visual Studio perf team.

## Torn State Scenarios

There are a number of scenarios where customers end up in a torn state. The most common is when customers have a CI setup with the following items:

1. They use a task like [actions/setup-dotnet][setup-dotnet] to install the .NET SDK and use a flexible version format like `8.x`.
2. They use `msbuild` to actually drive their build. This `msbuild` comes from the Visual Studio installed on the machine image.

This means that CI systems are updated to the latest .NET SDK virtually as soon as we release them. However the version of Visual Studio is updated much later as CI images usually take several weeks to upgrade to a new Visual Studio version. This is a very common CI setup and means a significant number of our customers end up in a torn state for several weeks.

Teams also get into this state when the Visual Studio used for developement is older than the .NET SDK they are using:

1. Teams can get locked into older Visual Studio via org policy but freely update to newer .NET SDKs.
2. Using a preview version of the .NET SDK. These inherently represent a torn SDK state because they almost never match the compiler in Visual Studio. This results in blockers for big teams like Bing from testing out our previews.

## Solution

### MSBuild will use the .NET SDK tools for SDK projects

When building .NET SDK projects, msbuild will load tasks from the .NET SDK instead of Visual Studio. Specifically it will load the Roslyn compiler task from the .NET SDK. That task will then function the same as it does when launched from dotnet build.

To facilitate tasks from the .NET SDK launching .NET Core based processes, the msbuild host will [set the environment variable][dotnet-host-path] `DOTNET_HOST_PATH`. This will point to the same .NET Core host that the .NET SDK uses. This will allow tasks to launch .NET Core based processes with the same behavior as the .NET SDK would.

This change will allow for msbuild to have a consistent build experience with dotnet build for .NET SDK projects. It will have no impact on non-SDK projects as they will continue to use the compiler tasks that come from Visual Studio.

## .NET SDK analyzers will load from Visual Studio

Analyzers and generators which ship in the .NET SDK box will change to having a copy checked into Visual Studio. This will occur as part of the .NET SDK insertion process. When .NET SDK based projects are loaded at design time, the Visual Studio copy of the analyzer will be loaded.

This means that the behavior of analyzers in Visual Studio design time will be independent of the .NET SDK used by the project. This increases the predictability of our product as we know at ship time what analyzer and generator experience our customers will be getting. It will no longer change as the underyling SDK does.

Having a consistent version also enables us to take actions like NGEN or R2R analyzers inside of Visual Studio. This is a long standing request from the Visual Studio perf team but impossible to satisfy when the versions change based on global.json resolution.

This approach is already taken by [the Razor generator][code-razor-vs-load] to great success. It allowed us to provide a consistent, and testable, experience for Razor editting. Recently it was also adopted by C# code style analyzers to similar success. There is every reason to expect similar success as we expand this to all analyzers and generators in the .NET SDK.

This also benefits analyzer and generator development models as they can now target the latest version of Roslyn. There is no more need to multi-target because the version of the compiler the analyzer will be used with is known at ship time for all core build scenarios.

This does mean that our design time experience can differ from our command line experience when customers are in a torn state. Specifically it's possible, even likely in some cases, that the diagnostics produced by design time builds will differ from command line builds. That is a trade off that we are willing to make for reliability. Customers who wish to have a consistent experience between design time should not operate in a torn state.

### NuGet based analyzers

Analyzers which ship via NuGet will continue to following the existing [support policies][matrix-of-paine]. This means that they will either need to target a sufficiently old version of Roslyn or implement multi-targeting support to function in Visual Studio.

In the case of [multi-targeting][issue-analyzer-mt] this proposal is effectively a no-op. Analyzer multi-targeting is already based off of Roslyn versions, not .NET SDK versions, hence the proper version of the analyzer will still load in a torn state. The version of the Roslyn compiler in a torn state for msbuild is the same used for dotnet build hence it's already a scenario the analyzer needs to support.

There is nothing preventing NuGet based analyzers from following the same model as .NET SDK based analyzers. The mechanism for redirection is extensible. This is not a requirement for this proposal and is left as a future work item.

## Issues

### Multiple compiler servers

Solutions that mix .NET SDK and Visual Studio projects will end up with multiple compiler servers running. This is a result of the .NET SDK projects using the compiler from the .NET SDK and non-SDK projects using the compiler from Visual Studio. There is nothing functionally wrong with this but it's possible customers will notice this and ask questions about it.

The compiler will offer an msbuild property that allows non-SDK projects to opt into using the .NET SDK based compiler: `<RoslynUseSdkCompiler>true</RoslynUseSdkCompiler>`. This can be added to a `Directory.Build.props` file to impact the entire solution.

### .NET Framework based analyzers

There are a few analyzers which are built against .NET Framework TFMs. That means when loaded in a .NET Core compiler it could lead to compatibility issues. This is not expected to be a significant issue as our processes have been pushing customers to target `netstandard` in analyzers for 5+ years. However it is possible that some customers will run into issues here.

For those customers we will offer a NuGet package or property that overrides the .NET Core compiler with a .NET Framewor based on (at the same version). That will allow customers to upgrade to the new .NET SDK with minimal impact.

However, it is not our intent to support this inperpetuity. The documentation around this property will make it clear that by .NET 12 we will no longer support this property. By that time such analyzers will need to move to target `netstandard`.

## Alternative

### Make the compiler in Visual Studio pluggable

An alternative approach is to allow the compiler inside Visual Studio, both the design time and msbuild copy, to be pluggable. Essentially for a given .NET SDK, grab the equivalent .NET Framework bits and have Visual Studio load them at startup.

This proposal is deemed very unlikley to be successful. Visual Studio is **highly** sensitive to the version of Roslyn it usses. Keeping Visual Studio perf neutral on every Roslyn insertion requires active work by both the Roslyn and Visual Studio teams. It is very unlikely that we could load arbitrary versions of Roslyn into Visual Studio and expect to get equvialent performance.

Further Visual Studio supports loading many different versions of the .NET SDK. For this to work Visual Studio would need to be tolerant of loading much older Roslyns into current shipping versions. That is a significant engineering effort that is unlikely to be successful.

### .NET SDK in box analyzers multi-target

Technically in box analyzers can have a multi-targeting strategy just as NuGet based analyzers do. This is actively discouraged because it leads to unnecessary increases in .NET SDK sizes. The time spent implementing multi-targeting is likely better spent moving to a dual insertion to keep .NET SDK sizes down and provide a consistent experience with other analyzers.

### .NET SDK in box analyzers use light up

In box could also employ a light up strategy. Essentially reference older versions of the compiler and use reflection to opportunistically discover + use newer APIs. This is successfully employed in analyzers like StyleCop.

## Misc

### Visual Studio Code

This is how Visual Studio Code fits into our matrix after this work is complete:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From .NET SDK | From .NET SDK |
| dotnet msbuild | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Visual Studio |
| DevKit | From DevKit | From .NET SDK |

On the surface it seems like VS Code has the same issues as Visual Studio does today. However this is not the case. Visual Studio is problematic because at any given time there can be ~5 different versions in active support each with a different version of the compiler. Every Visual Studio but the latest is an older compiler that run into issues with analyzers.

There is only one version of the DevKit extension. It is released using the latest compilers from roslyn at a very high frequency. That frequency is so high that the chance it has an older compiler than the newest .NET SDK is virtually zero. That means at the moment there is no need to solve the problem here. If DevKit changes its release cadance in the future this may need to be revisited.

### Related Issues

- [Roslyn tracking issue for torn state](https://github.com/dotnet/roslyn/issues/72672)
- [MSBuild property for torn state detection](https://github.com/dotnet/installer/pull/19144)
- [Long term build-server shutdown issue](https://github.com/dotnet/msbuild/issues/10035)
- [MSBuild decouple issue](https://github.com/dotnet/msbuild/issues/11142)

[matrix-of-paine]: https://aka.ms/dotnet/matrixofpaine
[sdk-lifecycle]: https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle
[code-razor-vs-load]: https://github.com/dotnet/roslyn/blob/9aea80927e3d4e5a2846efaa710438c0d8d2bfa2/src/Workspaces/Core/Portable/Workspace/ProjectSystem/ProjectSystemProject.cs#L1009
[setup-dotnet]: https://github.com/actions/setup-dotnet
[pr-detect-torn-state]: https://github.com/dotnet/installer/pull/19144
[issue-analyzer-mt]: https://github.com/dotnet/sdk/issues/20355
[issue-legacy-wpf]: https://github.com/dotnet/wpf/issues/9112
[dotnet-host-path]: https://github.com/dotnet/msbuild/issues/11086
