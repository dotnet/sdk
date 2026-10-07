// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
// Provider selection is process-wide state, so these tests cannot safely overlap other provider tests.
[DoNotParallelize]
public sealed class StringResourceManagerProviderTests
{
    private static readonly Assembly s_assembly = ResourceTestUtilities.TestAssembly;

    [TestInitialize]
    public void ResetBeforeTest() => StringResourceManagerProvider.ResetForTests();

    [TestCleanup]
    public void ResetAfterTest() => StringResourceManagerProvider.ResetForTests();

    [TestMethod]
    public void Create_WithoutRegistration_ReturnsRuntimeSatelliteManager()
    {
        StringResourceManager manager =
            StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        Assert.IsInstanceOfType<SatelliteStringResourceManager>(manager);
    }

    [TestMethod]
    public void Create_RegisteredProvider_ReturnsManagerAndPassesInputs()
    {
        StringResourceManager expected = new("Expected", s_assembly);
        string? receivedBaseName = null;
        Assembly? receivedAssembly = null;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                receivedBaseName = baseName;
                receivedAssembly = ownerAssembly;
                return expected;
            });

        StringResourceManager actual =
            StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        Assert.AreSame(expected, actual);
        Assert.AreEqual("Test.Resources", receivedBaseName);
        Assert.AreSame(s_assembly, receivedAssembly);
    }

    [TestMethod]
    public void RegisterEmbedded_UsesEmbeddedResourceManager()
    {
        StringResourceManagerProvider.RegisterEmbedded();

        StringResourceManager manager =
            StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        Assert.IsInstanceOfType<EmbeddedStringResourceManager>(manager);
    }

    [TestMethod]
    public void Register_AfterSelection_ThrowsInvalidOperationException()
    {
        _ = StringResourceManagerProvider.Create("Test.Resources", s_assembly);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => StringResourceManagerProvider.Register(
                (baseName, ownerAssembly) => new StringResourceManager(baseName, ownerAssembly)));
    }
}
