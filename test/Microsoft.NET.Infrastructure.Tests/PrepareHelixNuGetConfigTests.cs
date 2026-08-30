// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.SdkCustomHelix.Sdk;

namespace Microsoft.NET.Infrastructure.Tests;

[TestClass]
public class PrepareHelixNuGetConfigTests : SdkTest
{
    [TestMethod]
    public void ItPreparesPackageSourcesForTheHelixEnvironment()
    {
        TestDirectory directory = TestAssetsManager.CreateTestDirectory();
        string sourceFile = Path.Combine(directory.Path, "source.config");
        string destinationFile = Path.Combine(directory.Path, "output", "NuGet.config");
        File.WriteAllText(
            sourceFile,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="dotnet11" value="https://example.test/dotnet11" />
                <add key="dotnet10-transport" value="https://example.test/transport" />
                <add key="richnav" value="https://example.test/richnav" />
              </packageSources>
            </configuration>
            """);

        var task = new PrepareHelixNuGetConfig
        {
            SourceFile = sourceFile,
            DestinationFile = destinationFile,
        };

        Assert.IsTrue(task.Execute());

        XElement packageSources = XDocument.Load(destinationFile).Root!.Element("packageSources")!;
        var sources = packageSources
            .Elements("add")
            .ToDictionary(
                source => (string)source.Attribute("key")!,
                source => (string)source.Attribute("value")!);

        Assert.HasCount(3, sources);
        Assert.AreEqual("https://example.test/dotnet11", sources["dotnet11"]);
        Assert.AreEqual("%DOTNET_ROOT%/.nuget", sources["dotnet-under-test"]);
        Assert.AreEqual(
            "%DOTNET_SDK_TEST_EXECUTION_DIRECTORY%/Testpackages",
            sources["testpackages"]);
    }
}
