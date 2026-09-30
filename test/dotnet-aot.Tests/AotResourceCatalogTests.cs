// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotResourceCatalogTests
{
    [TestMethod]
    public void CompiledResourceModeMatchesRequestedMode()
    {
        string requestedMode =
            Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_RESOURCE_MODE")
            ?? nameof(AotResourceMode.ExternalLocalized);
        Assert.IsTrue(Enum.TryParse(requestedMode, out AotResourceMode expectedMode));
        Assert.AreEqual(expectedMode, AotResourceModeConfiguration.Current);
    }

    [TestMethod]
    public void CatalogContainsUniqueResourceBaseNames()
    {
        Assert.HasCount(11, AotResourceCatalog.All);
        Assert.HasCount(
            AotResourceCatalog.All.Count,
            AotResourceCatalog.All.Select(resource => resource.BaseName).Distinct(StringComparer.Ordinal));
    }

    [TestMethod]
    [DataRow("Microsoft.DotNet.Cli.CliStrings", "dotnet")]
    [DataRow("Microsoft.DotNet.Cli.Commands.CliCommandStrings", "dotnet")]
    [DataRow("Microsoft.NET.Build.Tasks.Strings", "dotnet")]
    [DataRow("Microsoft.DotNet.Cli.CommandDefinitionStrings", "Microsoft.DotNet.Cli.Definitions")]
    [DataRow("Microsoft.DotNet.Cli.Utils.LocalizableStrings", "Microsoft.DotNet.Cli.Utils")]
    [DataRow("Microsoft.DotNet.Configurer.LocalizableStrings", "Microsoft.DotNet.Configurer")]
    [DataRow("Microsoft.DotNet.ProjectTools.Resources", "Microsoft.DotNet.ProjectTools")]
    [DataRow("Microsoft.DotNet.FileBasedPrograms.FileBasedProgramsResources", "Microsoft.DotNet.ProjectTools")]
    [DataRow("Microsoft.NET.Sdk.Localization.Strings", "Microsoft.NET.Sdk.WorkloadManifestReader")]
    [DataRow("System.CommandLine.StaticCompletions.Resources.Strings", "System.CommandLine.StaticCompletions")]
    [DataRow("System.CommandLine.Properties.Resources", "System.CommandLine")]
    public void CatalogMapsResourceToManagedOwner(string baseName, string managedAssemblyName)
    {
        Assert.IsTrue(AotResourceCatalog.TryGet(baseName, out AotResourceDescriptor descriptor));
        Assert.AreEqual(managedAssemblyName, descriptor.ManagedAssemblyName);
    }
}
