# Decoupling the .NET SDK and Visual Studio

## Goals

Visual Studio and the .NET SDK are separate products, but their experiences are tightly coupled. Changes to the .NET SDK version can influence Visual Studio's design-time behavior, while updating Visual Studio can impact command-line builds for .NET SDK projects. When the products are in sync these interactions are benign, but when they are not, they lead to compatibility and reliability issues. This document proposes a design to decouple these products, allowing each to provide a more defined and independent experience.

Design Goals:

1. **Consistent Build Experiences**: The `msbuild` and `dotnet build` command line build experience for a solution should be functionally equivalent.
2. **Independent Design Time Behavior**: Visual Studio's design-time experience should be largely independent of the .NET SDK used by the solution.
3. **Clear Handling of Divergence**: To make explicit that it is okay, and even expected, that the design time and command line build experiences can differ when the two products are not in sync.

## Terminology

- **msbuild**: refers to the .NET Framework-based msbuild included with Visual Studio.
- **dotnet build**: refers to the .NET Core based msbuild included with the .NET SDK.
- **analyzers**: this document will use analyzers to refer to both analyzers and generators.
- **torn state**: when the .NET SDK and Visual Studio are not in sync.
- **.NET SDK project**: a project that uses the .NET SDK project format.

## Summary

Visual Studio and the .NET SDK are separate products but they are intertwined in command line and design time build scenarios as different components are loaded from each product. This table represents how intertwined Roslyn specifically is currently:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From Visual Studio | From .NET SDK |
| dotnet build | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Both |

There are other products that have similar intertwined behavior such as NuGet, MSBuild, Razor, etc ... Roslyn is generally the most impactful and the one we are focusing on in this document as its path forward can be generalized to the other products.

Generally this mixing of components is fine because Visual Studio will install a .NET SDK that is functionally paired with it. For example 17.10 installs .NET SDK 8.0.3xx, 17.9 installs .NET SDK 8.0.2xx, etc ... In that scenario the components are the same version and the mixing is largely benign. However when the customer is in a torn state, that is the .NET SDK used is not paired with the Visual Studio version, then compatibility issues can, and **will**, arise. Particularly when the .NET SDK is _newer_ than Visual Studio customers will end up with the following style of error[^1]:

> CSC : warning CS9057: The analyzer assembly '..\dotnet\sdk\8.0.200\Sdks\Microsoft.NET.Sdk.Razor\source-
> generators\Microsoft.CodeAnalysis.Razor.Compiler.SourceGenerators.dll' references version '4.9.0.0' of the compiler, which is newer
> than the currently running version '4.8.0.0'.

These bugs slip by because even though a given .NET SDK is supported in many Visual Studio versions, we do not fully validate its correctness. Specifically, consider that when releasing the .NET 8.0.400 SDK. It is supported in 17.8-17.11 but only fully validated in 17.11. Likewise, when Visual Studio releases servicing fixes it does not attempt to fully validate all supported .NET SDKs. This work on both sides would be prohibitively expensive. Instead, we seek to decouple these experiences so that such testing is not necessary.

Customers existing in a torn state is a common scenario:

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
| dotnet build | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Visual Studio |

Specifically we will be:

1. Changing `msbuild` to use tools and tasks from the .NET SDK when building .NET SDK projects.
2. Changing Visual Studio to use analyzers installed with Visual Studio.

In addition to making our builds more reliable this will also massively simplify our [analyzer development strategy][sdk-lifecycle]. Analyzers in the SDK following this model can always target the latest Roslyn version without the need for complicated multi-targeting. Further it will allow Visual Studio the ability to NGEN or R2R analyzers which is a long standing request from the Visual Studio perf team.

## Torn State Scenarios

There are a number of scenarios where customers end up in a torn state. The most common is when customers have a CI setup with the following items:

1. They use a task like [actions/setup-dotnet][setup-dotnet] to install the .NET SDK and use a flexible version format like `8.x`.
2. They use `msbuild` to actually drive their build. This `msbuild` comes from the Visual Studio installed on the machine image.

This means that CI systems are updated to the latest .NET SDK virtually as soon as we release them. However the version of Visual Studio is updated much later as CI images usually take several weeks to upgrade to a new Visual Studio version. This is a very common CI setup and means a significant number of our customers end up in a torn state for several weeks.

Teams also get into this state when the Visual Studio used for development is older than the .NET SDK they are using:

1. Teams can get locked into older Visual Studio via org policy but freely update to newer .NET SDKs.
2. Using a preview version of the .NET SDK. These inherently represent a torn SDK state because they almost never match the compiler in Visual Studio. This results in blockers for big teams like Bing from testing out our previews.
3. Using a global.json that specifies a different version of the .NET SDK than the Visual Studio version.

## Solution

### MSBuild will use the .NET SDK tools for SDK projects

When building .NET SDK projects, `msbuild` will load tasks from the .NET SDK instead of Visual Studio. Specifically it will load the Roslyn compiler task from the .NET SDK. That task will then function the same as it does when launched from `dotnet build`.

To facilitate tasks from the .NET SDK launching .NET Core based processes, the `msbuild` host will [set the property][dotnet-host-path] `DOTNET_HOST_PATH`. This will point to the same .NET Core host that the .NET SDK uses. This will allow tasks to launch .NET Core based processes with the same behavior as the .NET SDK would.

This change will allow for `msbuild` to have a more consistent build experience with `dotnet build` for .NET SDK projects. It will have no impact on non-SDK projects as they will continue to use the compiler tasks that come from Visual Studio.

### .NET SDK analyzers will load from Visual Studio

Analyzers and generators which ship in the .NET SDK box will change to having a copy checked into Visual Studio. This will occur as part of the .NET SDK insertion process. When .NET SDK based projects are loaded at design time, the Visual Studio copy of the analyzer [will be loaded](documentation/general/analyzer-redirecting.md).

This means that the behavior of analyzers in Visual Studio design time will be independent of the .NET SDK used by the project. This increases the predictability of our product as we know at ship time what analyzer and generator experience our customers will be getting. It will no longer change as the underlying SDK does.

Having a consistent version also enables us to take actions like NGEN or R2R analyzers inside of Visual Studio. This is a long standing request from the Visual Studio perf team but impossible to satisfy when the versions change based on global.json resolution.

This approach is already taken by [the Razor generator][code-razor-vs-load] to great success. It allowed us to provide a consistent, and testable, experience for Razor editing. Recently it was also adopted by [C# code style analyzers](https://github.com/dotnet/roslyn/pull/75250) to similar success. There is every reason to expect similar success as we expand this to all analyzers and generators in the .NET SDK.

This also benefits the developement model for analyzers and generators that ship in the .NET SDK as they can now target the latest version of Roslyn. There is no more need to multi-target because the version of the compiler the analyzer will be used with is known at ship time for all core build scenarios.

This does mean that our design time experience can differ from our command line experience when customers are in a torn state. Specifically it's possible, even likely in some cases, that the diagnostics produced by design time builds will differ from command line builds. That is a trade off that we are willing to make for reliability. Customers who wish to have a consistent experience between design time should not operate in a torn state.

### NuGet based analyzers

Analyzers which ship via NuGet will continue to follow the existing [support policies][matrix-of-paine]. This means that they will either need to target a sufficiently old version of Roslyn or implement multi-targeting support to function in Visual Studio.

In the case of [multi-targeting][issue-analyzer-mt] this proposal is effectively a no-op. Analyzer multi-targeting is already based off of Roslyn versions, not .NET SDK versions, hence the proper version of the analyzer will still load in a torn state. The version of the Roslyn compiler in a torn state for `msbuild` is the same used for `dotnet build` hence it's already a scenario the analyzer needs to support.

There is nothing preventing NuGet based analyzers from following the same model as .NET SDK based analyzers. The mechanism for redirection is extensible. This is not a requirement for this proposal and is left as a future work item.

## Issues

### Multiple compiler servers

Solutions that mix .NET SDK and Visual Studio projects will end up with multiple compiler servers running. This is a result of the .NET SDK projects using the compiler from the .NET SDK and non-SDK projects using the compiler from Visual Studio. There is nothing functionally wrong with this but it's possible customers will notice this and ask questions about it.

The compiler will offer a property that allows SDK projects to use the MSBuild version of the compiler when being built with `msbuild`: `<RoslynCompilerType>Framework</RoslynCompilerType>`. This can be added to a `Directory.Build.props` file to impact the entire solution. This is not expected to be a common scenario but is available for customers who need it. This property will be ignored when using `dotnet build` as there is no way to fall back to the Visual Studio compiler in that scenario.

### .NET Framework based analyzers

There are a few analyzers which are built against .NET Framework TFMs. That means when loaded in a .NET Core compiler it could lead to compatibility issues. This is not expected to be a significant issue as our processes have been pushing customers to target `netstandard` in analyzers for 5+ years. However it is possible that some customers will run into issues here.

For those customers we will recommend that they set `<RoslynCompilerType>Framework</RoslynCompilerType>` in their build to ensure a .NET Framework based compiler is used.  However, it is not our intent to support loading .NET Framework based analyzers in perpetuity. Starting in .NET 12 the compiler will begin issueing warnings is this setup when it detects framework analyzers, and this will become an error in .NET 13. Non-SDK projects will support loading framework based analyzers for the foreseeable future.

### Build server shutdown

Today there is not a 100% reliable way to shutdown the VBCSCompiler process. The `dotnet build-server shutdown` command works in common cases but fails in a number of corner cases. This has lead to customers who require the server to be shutdown to add lines like `kill VBCSCompiler` into their infrastructure scripts. This proposal will break those scripts as it will change the process name of the compiler server from `VBCSCompiler` to `dotnet`.

To mitigate this we will be fixing the `build-server shutdown` command to be reliable across all the scenarios we care about. The details of this are captured in [issue 45956](https://github.com/dotnet/sdk/issues/45956).

## RoslynCompilerType

Based on the value of the `RoslynCompilerType` property, the SDK (or compiler toolset packages) set the following properties:
- `RoslynTasksAssembly` to a full path to a [Roslyn build task DLL][roslyn-build-task],
  and the SDK targets use `$(RoslynTasksAssembly)` to load the build task
- `RoslynTargetsPath` to the directory path of the roslyn tasks assembly. This property is used by some targets
  but it should be avoided if possible because the tasks assembly name can change as well, not just the directory path.
  This property is a misnomer for historical reasons, it really points to _tasks_, there is no guarantee there will be any _targets_ in the directory.
- `RoslynAssembliesPath` to the directory path of other roslyn assemblies (like `Microsoft.CodeAnalysis.dll`).
  In builds using .NET Framework MSBuild, the path is set to the Roslyn directory that ships with MSBuild (no .NET Framework Roslyn assemblies ship with the .NET SDK).
- `RoslynCoreAssembliesPath` to the directory path of other roslyn assemblies which target .NET Core regardless of the host being .NET Framework or Core MSBuild.

These values are recognized for property `RoslynCompilerType`:
- `Core`: use the compiler that comes with the .NET SDK
- `Framework`: use the compiler that comes with .NET Framework MSBuild
- `FrameworkPackage`: download the Microsoft.Net.Sdk.Compilers.Toolset package which contains the .NET Framework compiler corresponding to the .NET SDK version
- `Custom`: the SDK will not override `RoslynTasksAssembly` and the other properties listed above - used for example by Microsoft.Net.Compilers.Toolset package which injects its own version of the build task

## Alternative

### Make the compiler in Visual Studio pluggable

An alternative approach is to allow the compiler inside Visual Studio, both the design time and MSBuild copy, to be pluggable. Essentially for a given .NET SDK, grab the equivalent .NET Framework bits and have Visual Studio load them at startup.

This proposal is deemed very unlikely to be successful. Visual Studio is **highly** sensitive to the version of Roslyn it uses. Keeping Visual Studio perf neutral on every Roslyn insertion requires active work by both the Roslyn and Visual Studio teams. It is very unlikely that we could load arbitrary versions of Roslyn into Visual Studio and expect to get equivalent performance.  Consider that concretely this would mean that switching between solutions that used different .NET SDK versions would require a Visual Studio restart as well as rebuilding the MEF cache.

Further Visual Studio supports loading many different versions of the .NET SDK. For this to work Visual Studio would need to be tolerant of loading much older Roslyns into current shipping versions. That is a significant engineering effort that is unlikely to be successful.

### .NET SDK in box analyzers multi-target

Technically in box analyzers can have a multi-targeting strategy just as NuGet based analyzers do. This is actively discouraged because it leads to unnecessary increases in .NET SDK sizes. The time spent implementing multi-targeting is likely better spent moving to a dual insertion to keep .NET SDK sizes down and provide a consistent experience with other analyzers.

### .NET SDK in box analyzers use light up

In box could also employ a light up strategy. Essentially reference older versions of the compiler and use reflection to opportunistically discover + use newer APIs. This is successfully employed in analyzers like StyleCop.

### Avoid the torn state

Customers can avoid these problems by not pinning the .NET SDK and simply using the version that is naturally paired with Visual Studio.

## Misc

### Visual Studio Code

This is how Visual Studio Code fits into our matrix after this work is complete:

| Scenario | Loads Roslyn | Loads Analyzers / Generators |
| --- | --- | --- |
| msbuild | From .NET SDK | From .NET SDK |
| dotnet build | From .NET SDK | From .NET SDK |
| Visual Studio Design Time | From Visual Studio | From Visual Studio |
| DevKit | From DevKit | From .NET SDK |

On the surface it seems like VS Code has the same issues as Visual Studio does today. However this is not the case. Visual Studio is problematic because at any given time there can be ~5 different versions in active support each with a different version of the compiler. Every Visual Studio but the latest is an older compiler that run into issues with analyzers.

There is only one version of the DevKit extension. It is released using the latest compilers from roslyn at a very high frequency. That frequency is so high that the chance it has an older compiler than the newest .NET SDK is virtually zero. That means at the moment there is no need to solve the problem here. If DevKit changes its release cadence in the future this may need to be revisited.

### Related Issues

- [Roslyn tracking issue for torn state](https://github.com/dotnet/roslyn/issues/72672)
- [MSBuild property for torn state detection](https://github.com/dotnet/installer/pull/19144)
- [Long term build-server shutdown issue](https://github.com/dotnet/msbuild/issues/10035)
- [MSBuild decouple issue](https://github.com/dotnet/msbuild/issues/11142)

[^1]: This specific problem is now largely mitigated in `msbuild` scenarios by the .NET SDK implicitly adding the Microsoft.Net.Sdk.Compilers.Toolset package to builds in a torn state. This will install and use a newer C# compiler when MSBuild is older than the .NET SDK. That fixes a large amount of the problems but has a number of downsides: increases restore time, compiler must go through full JIT, corner case scenarios like `$(NuGetPackageRoot)` not being set lead to errors, only fixes one type of torn state, etc ... 


[matrix-of-paine]: https://aka.ms/dotnet/matrixofpaine
[sdk-lifecycle]: https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle
[code-razor-vs-load]: https://github.com/dotnet/roslyn/blob/9aea80927e3d4e5a2846efaa710438c0d8d2bfa2/src/Workspaces/Core/Portable/Workspace/ProjectSystem/ProjectSystemProject.cs#L1009
[roslyn-build-task]: https://github.com/dotnet/roslyn/blob/ccb05769e5298ac23c01b33a180a0b3715f53a18/src/Compilers/Core/MSBuildTask/README.md
[setup-dotnet]: https://github.com/actions/setup-dotnet
[pr-detect-torn-state]: https://github.com/dotnet/installer/pull/19144
[issue-analyzer-mt]: https://github.com/dotnet/sdk/issues/20355
[issue-legacy-wpf]: https://github.com/dotnet/wpf/issues/9112
[dotnet-host-path]: https://github.com/dotnet/msbuild/issues/11086
