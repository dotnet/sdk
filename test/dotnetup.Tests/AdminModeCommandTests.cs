// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminModeCommandTests
{
    [TestMethod]
    public void Init_ModeOption_IsNotSupported()
    {
        Parser.Parse(["init", "--mode", "admin"]).Errors.Should().NotBeEmpty();
    }
}
