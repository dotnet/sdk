// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.New;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.New.Tests
{
    public class SdkInfoProviderTests
    {
        [Fact]
        public void GetInstalledVersionsAsync_ShouldContainCurrentVersion()
        {
            string dotnetRootUnderTest = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string pathOrig = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", dotnetRootUnderTest + Path.PathSeparator + pathOrig);

            try
            {
                ISdkInfoProvider sp = new SdkInfoProvider();

                string currentVersion = sp.GetCurrentVersionAsync(default).Result;
                List<string> allVersions = sp.GetInstalledVersionsAsync(default).Result?.ToList();

                currentVersion.Should().NotBeNullOrEmpty("Current Sdk version should be populated");
                allVersions.Should().NotBeNull();
                allVersions.Should().Contain(currentVersion, "All installed versions should contain current version");
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", pathOrig);
            }
        }
    }
}
