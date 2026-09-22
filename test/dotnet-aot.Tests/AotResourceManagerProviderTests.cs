// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotResourceManagerProviderTests
{
    [TestMethod]
    public void ExternalNeutralPreflight_MissingOwnerFailsBeforeRegistration()
    {
        string directory = Directory.CreateTempSubdirectory("dotnet-aot-resource-preflight-").FullName;
        bool wasConfigured = AotResourceManagerProvider.IsConfigured;

        try
        {
            Assert.ThrowsExactly<FileNotFoundException>(
                () => AotResourceManagerProvider.ValidateExternalNeutralResources(directory));
            Assert.AreEqual(wasConfigured, AotResourceManagerProvider.IsConfigured);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
