// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public record class ManifestSpecifier(ManifestId Id, ManifestVersion Version, SdkFeatureBand FeatureBand)
    {
        public override string ToString() => $"{Id}: {Version}/{FeatureBand}";
    }
}

//  Add attribute to support init-only properties on .NET Framework
#if !NET
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}
#endif
