# Diagnosing issues with .NET SDK Workloads

## Installing Workloads

### Finding New Workloads to Install

The .NET SDK is designed on workload install, update, and restore to look for the latest available workload of the matching feature band in your configured nuget feeds.

First, we try to find a new version that matches your currently installed .NET SDK band (so if you have an 8.0.3xx SDK, we'll look for an 8.0.300 band workload). Then we'll look for a workload in the band matching what's currently installed.

These new manifests get installed into the dotnet/sdk-manifests folder.
Next we will install the packs (nuget packages we install into the dotnet/packs folder) needed for the workloads you're trying to install as defined in the appropriate manifest.

### Likely Install Failures

When installing workloads, you may encounter the following common failures:

1. Workload pack not found in the nuget feed. This means that a manifest you've installed cannot find a pack referenced from that manifest. Most likely you updated your workloads with one set of feeds and now have a new set of feeds configured. Either find the feed you need or use a rollback file to switch to an older version of the workloads.
_Workload installation failed: One or more errors occurred. (microsoft.netcore.app.runtime.mono.ios-arm.msi.x64::7.0.16 is not found in NuGet feeds <feed>".)_
2. Mismatched workload manifest versions. This is likely because your feed had a workload from runtime but not the matching workload from dotnet/emsdk. If you're not using any special feeds, that probably means it's release day and the emsdk workload is in the process of being released. We've been trying to improve this process with each release to avoid this issue.
_Installation rollback failed: Workload manifest dependency 'Microsoft.NET.Workload.Emscripten.Current' version '8.0.3' is lower than version '8.0.4' required by manifest 'microsoft.net.workload.mono.toolchain.current'_

## Diagnosing Issues With Installed Workloads

### Common Workload State Failures

1. Workload is not installed. Try running `dotnet workload restore`. If that does not work, try running `dotnet build -getItem:MissingWorkloadPack` to determine what workload packs are missing. Our workload detection logic could be wrong and you could need a different workload than we list. This call should provide the pack we need and file an issue in the SDK repo with this information.
_NETSDK1147: To build this project, the following workloads must be installed:_
2. You installed workloads previously but now your workload templates are missing (Aspire and MAUI templates are installed by the workloads). This could be because your workloads were installed correctly at some point in the past but are now out of sync.
  1. You installed a new band of the SDK. Workloads are installed per band so installing a new SDK could lead to your workloads not working. [Workload versions](https://github.com/dotnet/designs/pull/294) should improve that.
  2. You installed a different workload from the dotnet CLI. We've improved this a few times but it's still possible to install a different workload which updates your workload manifests without updating your workloads. Please file a bug if this happens to you.
  3. You install a new version of Visual Studio that doesn't have worklaods selected. Visual Studio should include all available workloads so make sure to select them in Visual Studio setup.

### Collecting Workload Information

1. `dotnet workload --info`
2. `dotnet build -getItem:MissingWorkloadPack`
3. `dotnet --info`
4. `dotnet nuget list source`
5. https://aka.ms/vscollect <-- for admin install failures only
