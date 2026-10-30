// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupPolicyTests
{
    private readonly FakeDotnetupPolicyStore _store = new();

    [TestMethod]
    public void IsUnsignedDownloadBlocked_ReadsStore()
    {
        var policy = new DotnetupPolicy(_store);
        policy.IsUnsignedDownloadBlocked().Should().BeFalse();

        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        policy.IsUnsignedDownloadBlocked().Should().BeTrue();
    }

    [TestMethod]
    public void UnsignedSourcePolicy_ConstantsAliasDotnetupPolicy()
    {
        UnsignedSourcePolicy.WindowsPolicyKey.Should().Be(DotnetupPolicy.WindowsPolicyKey);
        UnsignedSourcePolicy.WindowsPolicyValueName.Should().Be(DotnetupPolicy.BlockUnsignedDownloadsValueName);
    }
}
