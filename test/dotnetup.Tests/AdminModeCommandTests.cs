// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminModeCommandTests
{
    [TestMethod]
    public void Init_ModeOption_IsNotSupported()
    {
        Parser.Parse(["init", "--mode", "admin"]).Errors.Should().NotBeEmpty();
    }

    [TestMethod]
    [DataRow("enableadminmode")]
    [DataRow("disableadminmode")]
    public void ElevatedSystemPath_AdminModeOperation_ParsesWithoutDotnetDir(string operation)
    {
        var parseResult = Parser.Parse(["elevatedsystempath", operation, "output.txt"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(ElevatedSystemPathCommandParser.DotnetDir).Should().BeNull();
    }
}
