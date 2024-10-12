// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class SdkInfoProviderTests
    {
        [Fact]
        public async Task GetInstalledVersionsAsync_ShouldContainCurrentVersion()
        {
            string dotnetRootUnderTest = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string? pathOrig = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", dotnetRootUnderTest + Path.PathSeparator + pathOrig);

            try
            {
                // make sure current process path is not picked up as the dontet executable location
                ISdkInfoProvider sp = new SdkInfoProvider(() => string.Empty);

                string currentVersion = await sp.GetCurrentVersionAsync(default);
                IEnumerable<string> allVersions = await sp.GetInstalledVersionsAsync(default);

                currentVersion.Should().NotBeNullOrEmpty("Current Sdk version should be populated");
                allVersions.ToList().Should().NotBeNull();
                allVersions.ToList().Should().Contain(currentVersion, "All installed versions should contain current version");
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", pathOrig);
            }
        }
    }
}
