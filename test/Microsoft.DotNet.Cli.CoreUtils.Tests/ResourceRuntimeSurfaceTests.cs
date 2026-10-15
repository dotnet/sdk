// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class ResourceRuntimeSurfaceTests
{
    [TestMethod]
    public void GeneratedFacingTypesArePublicAndImplementationTypesAreInternal()
    {
        Type[] generatedFacingTypes =
        [
            typeof(EmbeddedStringResourceManager),
            typeof(SatelliteStringResourceManager),
            typeof(SatelliteStringResourceProbeMode),
            typeof(StringResourceManager),
            typeof(StringResourceManagerOptions),
            typeof(StringResourceManagerProvider)
        ];

        foreach (Type type in generatedFacingTypes)
        {
            Assert.IsTrue(type.IsPublic, $"{type.FullName} must be accessible to generated code.");
        }

        Assert.IsFalse(typeof(MappedMemoryManager).IsPublic);
        Assert.IsFalse(typeof(RawResourceReader).IsPublic);
        Assert.IsFalse(typeof(StringResourceTableLoader).IsPublic);
    }
}
