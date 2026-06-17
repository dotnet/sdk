// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class ToolPackageInstanceTests
    {
        [Fact]
        public void MissingToolSettingsFileMessageIncludesPackageAndVersion()
        {
            var message = ToolPackageInstance.GetMissingToolSettingsFileMessage(
                new PackageId("dotnet-inspect"),
                NuGetVersion.Parse("1.2.3"));

            message.Should().Contain("dotnet-inspect@1.2.3");
        }
    }
}
