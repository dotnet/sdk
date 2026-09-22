// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateDefaultChannelTests
{
    [TestMethod]
    [DataRow("0.2.0", "stable")]
    [DataRow("1.0.0+0123456789abcdef", "stable")]
    [DataRow("0.2.0-preview.1.26465.7", "preview")]
    [DataRow("0.2.0-preview.1.26465.7+0123456789abcdef", "preview")]
    [DataRow("0.2.0-PREVIEW.1", "preview")]
    [DataRow("0.2.0-daily.1.26465.7", "daily")]
    [DataRow("0.1.3-dev", "daily")]
    [DataRow("0.2.0-previewer.1", "daily")]
    [DataRow("unknown", "daily")]
    public void DerivesChannelFromLoadedVersionLabel(string loadedVersion, string expected)
    {
        SelfUpdateDefaultChannel.FromLoadedVersion(loadedVersion).Should().Be(expected);
    }
}
