# .NET SDK Workload Preview Bands

In .NET 6, the [SDK Band](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workload-manifest.md#sdk-band) (also sometimes called the "SDK feature band") for workloads was defined as the SDK version with the patch number set to 0, and with any prerelease information removed.  For example, the SDK Band would be 6.0.100 for SDKs with version numbers 6.0.100-rc.1.21379.2, 6.0.100, and 6.0.102.

Since different previews used the same SDK band, and workloads by default update to the latest manifests available for the SDK band, this made it easy to get a mismatch between the preview version of the SDK and the version of the workloads that were being used.

Because of this, in .NET 7 we changed the definition of the SDK band to include the first two components of the SDK version prerelease specifier, if present. So the SDK band for 7.0.100-preview.1.12345 is 7.0.100-preview.1, and the SDK band for 7.0.100-rc.2.21505.57 is 7.0.100-rc.2.

Previews identified by an Arcade build number instead of a numbered preview phase share the stable SDK band. For example, `11.0.200-preview.26568.113` uses the `11.0.200` SDK band, while `11.0.200-preview.5.26568.113` uses `11.0.200-preview.5`. The same rule applies when resolving workload-set versions.

The logic to handle this is primarily in the [SdkFeatureBand class](/src/Resolvers/Microsoft.NET.Sdk.WorkloadManifestReader/SdkFeatureBand.cs). Additional impacted code is linked in https://github.com/dotnet/sdk/issues/23373.