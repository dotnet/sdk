# Eliminate Duplicate SDK Files

## Overview

This proposal focuses on eliminating duplicated files within the .NET SDK to reduce installation size and improve disk footprint.
By removing duplicate assemblies, we can reduce the SDK size by **35% (53 MB compressed, 140 MB on disk)** on Linux x64.

**Tracking issue:** [dotnet/sdk#41128](https://github.com/dotnet/sdk/issues/41128)

## Customer Impact: Why SDK Size Matters

While we often envision the .NET SDK as something installed once on a developer's machine, the reality is that most SDK installations occur in ephemeral, high-volume scenarios where the SDK is repeatedly downloaded and extracted.
SDK size directly affects both network costs (download/wire transfer) and time costs (extraction overhead).

Containers represent one of the largest and most measurable areas of impact. Official .NET SDK container images are pulled approximately [750,000 times per week](https://msit.powerbi.com/groups/6b5ffb99-5fd3-492b-bd02-724f09fe9eff/reports/7e5d7fef-a86c-4f94-8aa3-d356c3125ee0?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47&pbi_source=linkShare&bookmarkGuid=f44da1fd-c619-4158-aa51-f050b379a2b3).
When developers build within containers—whether for local development or CI/CD pipelines—they're pulling that full SDK image.
A 50 MB reduction in compressed size translates to 37.5 TB per week in bandwidth saved from container pulls alone.
Beyond containers, SDK installations also happen at high volume in CI/CD pipelines, cloud development environments, and through various tooling extensions.

The [Native AOT SDK epic](https://github.com/dotnet/sdk/issues/40931) represents a significant investment in improving performance, but it comes at the cost of increased SDK size.
Without addressing duplication first, Native AOT will exacerbate the size problem and likely make customers in CI and container scenarios unhappy.
This deduplication work gives us the headroom needed to absorb the Native AOT expansion while still delivering a better overall experience.

## By the Numbers

A duplicate file analysis was performed on the SDK layout of .NET 10.0.100, where file uniqueness is defined by *name*, *TFM* (Target Framework Moniker), and *culture*.
The following data is the result of this analysis using the [SdkLayoutAnalyzer](https://github.com/MichaelSimons/SdkLayoutAnalyzer) tool.

The goal is for the vast majority if not all of the components within the SDK to depend on and use the same version of their dependencies.

**Note:** The baseline measurements in the following tables compare the complete .NET installation (including runtimes, packs, shared frameworks, host, etc.) versus just the SDK directory contents.

### Linux x64

| Metric       | Baseline 10.0 (dotnet / SDK only) | Detected SDK Duplicates | % Duplicates (dotnet / SDK only) |
|--------------|-----------------------------------|-------------------------|----------------------------------|
| Archive Size | 229 MB / 150 MB                   | 53 MB                   | 23.1% / 35.3%                    |
| Disk Size    | 623 MB / 402 MB                   | 140 MB                  | 22.5% / 34.8%                    |
| File Count   | 4,915 / 3,667                     | 816 files               | 16.6% / 22.2%                    |

### Windows x64

| Metric       | Baseline 10.0 (dotnet / SDK only) | Detected SDK Duplicates | % Duplicates (dotnet / SDK only) |
|--------------|-----------------------------------|-------------------------|----------------------------------|
| Disk Size    | 789 MB / 411 MB                   | 148 MB                  | 18.8% / 36.0%                    |
| File Count   | 5,610 / 3,791                     | 908 files               | 16.2% / 24.0%                    |

**Note:** The WindowsDesktop runtime and target pack, along with .NET Framework support are the major reasons for the differences between Windows and Linux.

### Download impact of removing duplicates

The following table shows download times for the .NET Linux x64 archive at various network speeds, comparing the baseline 229 MB archive against the optimized 176 MB archive (53 MB reduction).
These times represent pure wire transfer costs and do not include connection overhead such as DNS resolution, TLS handshake, or other protocol negotiations.

| Network Speed        | Before (229 MB) | After (176 MB) | Time Saved |
| -------------------- | --------------: | -------------: | ---------: |
| 100 Mbps             | 18.32s          | 14.08s         | 4.24s      |
| 500 Mbps             | 3.66s           | 2.82s          | 0.85s      |
| 1 Gbps (1000 Mbps)   | 1.83s           | 1.41s          | 0.42s      |
| 10 Gbps (10,000 Mbps)| 0.18s           | 0.14s          | 0.04s      |

The deduplicated archive downloads **23% faster** on average across all network speeds.

### Extraction impact of removing duplicates

The following table shows .NET archive extraction times measured on a Linux x64 development environment with SSD storage.

| Metric  | Before (228.05 MB) | After (175.73 MB) | Time Saved |
| ------- | -----------------: | ----------------: | ---------: |
| Mean    | 3.81s              | 2.91s             | 0.90s      |
| Median  | 3.78s              | 2.92s             | 0.86s      |
| Min     | 3.77s              | 2.88s             | 0.89s      |
| Max     | 3.92s              | 2.96s             | 0.96s      |

The deduplicated archive extracts **23.5% faster** on average, saving approximately 0.90 seconds per extraction.

### Duplicate categorization (relative to lowest version file to keep)

- Duplicates with same hash as file to keep: 663 (100.3 MB)
- Duplicates with different version: 40 (5.9 MB)
- Duplicates with same version but different hash: 113 (33.8 MB)
  - Of which, same version but different arch: 89 (31.4 MB)

### Top 10 Largest Duplicates

| Filename                                  | Culture | TFM            | Duplicate Count | Duplicate Size (MB) |
|-------------------------------------------|---------|--------------- |-----------------|---------------------|
| Microsoft.CodeAnalysis.CSharp.dll         | neutral | net9.0         | 3               | 24.7                |
| Microsoft.CodeAnalysis.dll                | neutral | net9.0         | 3               | 10.8                |
| Microsoft.CodeAnalysis.Features.dll       | neutral | net9.0         | 2               | 5.3                 |
| Newtonsoft.Json.dll                       | neutral | net6.0         | 7               | 5.2                 |
| Microsoft.CodeAnalysis.VisualBasic.dll    | neutral | net9.0         | 2               | 4.6                 |
| Microsoft.CodeAnalysis.Workspaces.dll     | neutral | net9.0         | 2               | 4.0                 |
| Microsoft.CodeAnalysis.Razor.Compiler.dll | neutral | netstandard2.0 | 2               | 3.6                 |
| Microsoft.Build.Tasks.Core.dll            | neutral | net10.0        | 2               | 2.6                 |
| Microsoft.Build.dll                       | neutral | net10.0        | 2               | 2.5                 |
| System.Diagnostics.EventLog.Messages.dll  | neutral | netstandard2.0 | 4               | 2.3                 |
| **Total**                                 |         |                | **29**          | **65.6**            |

### Trends

An analysis of the current in-support versions of .NET (Linux x64) illustrates that this problem is not trending towards a desirable end state.
Notice the 33% increase in duplicate file size in 10.0 compared to 9.0.

| .NET Version | SDK Only File Count | SDK Only Disk Size | # Duplicate Files | Duplicate Size |
|--------------|---------------------|--------------------|-------------------|----------------|
| 8.0          | 3499                | 394 MB             | 772               | 114 MB         |
| 9.0          | 3619                | 380 MB             | 811               | 107 MB         |
| 10.0         | 3667                | 402 MB             | 816               | 140 MB         |

#### Top 4 Duplicate File Size Increases in 10.0

| File Name                             | Source                       | Change   | Size Increase (MB) |
|---------------------------------------|------------------------------|----------|--------------------|
| Microsoft.CodeAnalysis.CSharp.dll     | sdk/                         | New Copy | 18.2               |
| Microsoft.CodeAnalysis.dll            | sdk/                         | New Copy | 7.9                |
| Microsoft.CodeAnalysis.Features.dll   | sdk/DotnetTools/dotnet-watch | New Copy | 5.3                |
| Microsoft.CodeAnalysis.Workspaces.dll | sdk/DotnetTools/dotnet-watch | New Copy | 4.0                |
| **Total**                             |                              |          | **35.4**           |

## Proposed Approach

The overall direction of this effort is to eliminate the vast majority of duplicate assemblies within the .NET SDK so that each shared dependency is carried only once.
There may be a few special cases where different versions, etc. need to be retained.
Achieving this requires solving two distinct but related problems.
First, from a runtime and execution perspective, SDK components must be able to reliably load a single shared copy of each assembly from a common location.
Second, from a build and production perspective, SDK components would ideally be compiled against the same shared set of assemblies, and the SDK layout and packaging process must ensure that only one copy of each shared assembly is included in the final SDK distribution.
The proposed approach addresses both sides of this problem by defining a shared assembly location, enabling components to load from it, and restructuring the SDK build to populate and enforce this unified dependency model.

### Define a Common Assembly Location

A well-defined common assembly location is essential for shared assemblies to be loaded by SDK components, particularly out-of-process components like global tools. All SDK components should depend on a single version of shared dependencies as much as possible.

The root SDK directory is a natural location for this common assembly cache. However, the root SDK folder is already quite large, and consolidating shared assemblies there would add over 100 files to the root directory. This level of clutter doesn't seem desirable and would negatively impact readability.

A dedicated subdirectory for shared assemblies is preferred. This approach keeps the root directory clean and makes it clear which assemblies are part of the shared cache. Since we're centralizing on a single version of each shared dependency, there's no need for version-specific subdirectories within the common location.

**Directory Structure Considerations:**

While version folders aren't needed, we must account for framework versus core components. Some framework-specific assemblies are shared and would need to be placed in a subdirectory to distinguish them from core assemblies (e.g., `shared/net472/` for framework components, with core assemblies directly in `shared/`).

**Possible names for the common assembly location:**

- `shared` — simple, clear, and consistent with .NET conventions (like the existing `shared` directory for runtimes).
- `common` — widely recognized across ecosystems for shared dependencies and utilities.
- `dependencies` / `deps` — descriptive and clear.
- `libs` — short and familiar in many build systems.

#### Side Effects of a Common Assembly Location

Consolidating assemblies into a common location introduces potential side effects that must be carefully considered.
Discussions with compiler experts have noted that the compiler toolchain is particularly sensitive to assembly availability and resolution paths.
The presence of assemblies in a shared location can affect behavior.

Beyond the compiler, other SDK components may have similar sensitivities to assembly placement and availability.
These areas require careful analysis to identify potential behavioral changes when moving to a shared assembly model.
Further investigation in these areas will be required.

### Load from the Common Assembly Location

With the introduction of a formalized common assembly location, SDK components must be able to load assemblies from it.
The approach varies based on the type of component:

**Out-of-Process Components:** Components that run in their own process, such as global tools, will use **AssemblyLoadContext** to load shared assemblies from the common location.
This approach is already used today by [dotnet-watch](https://github.com/dotnet/sdk/blob/26bbbd92e5a3cc58037e696147fa25e03e68e3a8/src/BuiltInTools/dotnet-watch/Program.cs#L288).

**In-Process Framework Components:** These components are hosted within the SDK's framework context and define the assembly resolution paths.

**In-Process Core Components:** These components are hosted within the SDK's core context and define the assembly resolution paths.

**Performance:** Performance is a concern that must be validated.
We need to ensure that any changes to assembly loading do not regress performance in any way.

### SDK Layout Adjustments

The SDK layout needs to be updated to:

1. Populate the common assembly location.
1. Exclude common assemblies from individual components.

The strategy for achieving this varies based on the type of component:

**Global Tools:** Global tools have special layout targets today.
A good strategy would be for these targets to list out the assemblies to keep in the tool's directory, and the rest would be added to a shared assemblies list that gets copied to the shared assembly location.

**In-Process Components:** Other components that load in the same process would just need to ensure that the shared assemblies are defined and copied to the shared assembly location.
This can be done via various copying logic or using the `ExcludeAssets` mechanism.
The approach will vary based on the component type, but whenever possible we want to use a systematic approach.

**Version Control:** It's important to define which component controls what version is placed in the shared assembly cache.
This is critical for resolving version conflicts.
Higher version references may cause load failures, while lower version references could trigger security alerts.
Compile-time checks can be added to enforce the desired version rules. The VMR has helped reduce version conflict occurrences.

The upcoming [NuGet Vision 2027 work](https://microsoft-my.sharepoint.com/:w:/r/personal/aortiz_microsoft_com/Documents/NuGet%20Vision%202027.docx?d=w9f413c3dc36a4e7d887fe007071e10c0&csf=1&web=1&e=MD76Wr&nav=eyJoIjoiMjE2NDM5NDIwIn0) for "better supporting apps running in hosted environments or with specific composition/deployment patterns" will help facilitate version conflict resolution in this area. This work will introduce the capability to declare dependencies as "provided by the hosting environment," allowing components to use the host environment's version rather than carrying their own copy. This approach will make it much easier to eliminate version differences and consolidate on shared dependency versions within the SDK.

Similar patterns will be used for architecture differences as discussed in the [Architecture Differences](#architecture-differences) section.

### Testing Strategy

Once duplicated assemblies are removed, a regression test should:

- Detect duplicate files in the SDK layout.
- Fail the build if duplicates are found.
- Prevent regressions.

## Proof of Concept Results

A proof of concept was implemented for `dotnet-watch` and `dotnet-format`, two of the largest sources of duplication.
The POC used the **AssemblyLoadContext** approach with a shared assembly location and yielded the following results:

| Metric       | Baseline 11.0 (SDK only) | Size Reduction | % Reduction |
|--------------|--------------------------|----------------|-------------|
| Archive Size | 100 MB                   | 21 MB          | 21.0%       |
| Disk Size    | 296 MB                   | 62 MB          | 20.8%       |
| File Count   | 3,957                    | 384 files      | 9.7%        |

**Note:** These numbers are from a Linux development build, which differs significantly from official signed/optimized builds included in the [By the Numbers](#by-the-numbers) section.

## Other Concerns

### Architecture Differences

As noted in the [duplicate categorization](#linux-x64), a portion of duplicates with the same version but different hash are due to architecture differences (AnyCPU vs x64).
The plan is to eliminate these differences by standardizing on CPU-specific versions.

Initial analysis indicates these differences stem from AnyCPU builds coexisting with CPU-specific builds of the same assembly.
The general approach will be to prefer the CPU-specific version over the AnyCPU version when eliminating these duplicates, as CPU-specific builds can offer better performance characteristics for the target platform.

### Non-Assembly Duplicates

The data above only covers assemblies.
There are also duplicated non-assembly files such as `msdia140.dll.manifest`, `Microsoft.TestPlatform.targets`, `Microsoft.TemplateEngine.Cli.xml`, and `dotnet.runtimeconfig.json`.

- Duplicate non-assembly files (same hash): 65
- Total size of duplicates: 0.45 MB
- Largest duplicated file: `Microsoft.TemplateEngine.Cli.xml` — 0.18 MB

Non-assembly files are not directly targeted in this work.
They will be removed when the cost is low; otherwise, they remain out of scope due to limited ROI.

### Different TFM Duplicates

Beyond files duplicated with the same TFM (the primary focus of this work), some files with the same name and culture target different TFMs.
Eliminating these duplicates is outside the scope of the planned work.
Once same-TFM duplicates are addressed, additional analysis can be performed to evaluate the ROI of consolidating files across different TFMs.

The analysis categorizes these groups as follows:

**Linux x64 (10.0.100):**

Group Categorization:
- Groups differing by Core vs FX: 228
- Groups with different FX versions: 3
- Groups with different Core versions: 1
- Groups with multiple NetStandard versions: 0
- Groups with NetStandard + NetFx: 5
- Groups with NetStandard + Core: 30

Potential Savings (if duplicates were eliminated):
- Different FX versions (keep lowest): 1.9 MB
- Different Core versions (keep lowest): 0.1 MB
- NetStandard + NetFx (keep NetStandard): 0.9 MB
- NetStandard + Core (keep NetStandard): 5.9 MB

Total potential savings: 8.7 MB

**Key Observations:**

- **Core vs FX differences** are generally expected and necessary to support both frameworks.
- **Different Core versions** (e.g., net8.0 and net10.0) represent potential consolidation opportunities where the SDK could standardize on newer TFMs.
- **Different FX versions** (e.g., net472 and net48) may be necessary for backward compatibility but should be reviewed.
- **NetStandard combinations** indicate multi-targeting strategies that may be optimizable in some cases.

These groups are not mutually exclusive—a single file group can appear in multiple categories.
For example, a file with `netstandard2.0`, `net472`, and `net8.0` would be counted in both "NetStandard + NetFx" and "NetStandard + Core" categories.

Addressing different TFM duplicates requires different strategies than same-TFM duplicates and may involve API surface area analysis and compatibility considerations.

### Mixed RID Content

The SDK currently ships with some content placement issues related to Runtime Identifier (RID) specificity that contribute to unnecessary bloat:

#### RID-Specific Content in Inappropriate RIDs
In some cases, we ship RID-specific content to runtime identifiers where it's not applicable. This represents content that should be trimmed out entirely as it serves no purpose on the target platform.

**Example:** [dotnet/sdk#51743](https://github.com/dotnet/sdk/issues/51743) - Windows-specific assemblies shipped in Linux distributions.

#### Cross-Platform Support Content
In other cases, we ship content to support cross-platform development scenarios—for example, Windows-specific assemblies included in Linux SDKs to enable cross-compilation or multi-targeting scenarios. While this content does serve a purpose, it should be analyzed case-by-case to determine whether it should:
- Ship in-box as part of the core SDK experience
- Be available as optional packages that can be dynamically acquired when needed

**Example:** [dotnet/sdk#51835](https://github.com/dotnet/sdk/issues/51835) - Cross-platform development tooling dependencies.

#### Scope and Next Steps
Both of these content placement issues are outside the scope of the duplicate elimination work. However, they will likely be surfaced and made more visible as part of this effort. When identified, independent issues will be logged to address these concerns separately.

## Related

- [Visual Studio de-duplication effort](https://microsoft.sharepoint.com/:w:/s/b3f10b15-fb59-4650-957a-2c632aa943ba/IQBR2aXv7jC8RatIyxJJNPCeAQxjDmLii-R65o0yUvOJatk?e=IcnsAG)
- [NuGet Vision 2027 - Better supporting apps running in hosted environments or with specific composition/deployment patterns](https://microsoft-my.sharepoint.com/:w:/r/personal/aortiz_microsoft_com/Documents/NuGet%20Vision%202027.docx?d=w9f413c3dc36a4e7d887fe007071e10c0&csf=1&web=1&e=MD76Wr&nav=eyJoIjoiMjE2NDM5NDIwIn0)
