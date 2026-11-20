// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

[TestClass]
public class ShellInfoTests
{
    [TestMethod]
    [DataRow("/bin/bash", ShellType.Bash, "bash")]
    [DataRow("/bin/PowerShell", ShellType.PowerShell, "pwsh")]
    [DataRow("/usr/bin/pwsh", ShellType.PowerShell, "pwsh")]
    [DataRow("/usr/bin/nu", ShellType.Nushell, "nushell")]
    [DataRow("/usr/local/bin/nushell", ShellType.Nushell, "nushell")]
    [DataRow("/bin/unknown", ShellType.Unknown, null)]
    public void ShellNamesAreNormalized(string path, ShellType expectedType, string? expectedName)
    {
        var shell = ShellInfo.GetShellType(path);

        Assert.AreEqual(expectedType, shell);
        Assert.AreEqual(expectedName, ShellInfo.GetCanonicalName(shell));
    }

    [TestMethod]
    [DataRow(ShellType.Bash, "a'b\\c", "'a'\\''b\\c'")]
    [DataRow(ShellType.Fish, "a'b\\c", "'a\\'b\\\\c'")]
    [DataRow(ShellType.PowerShell, "a'b\u2019c", "'a''b\u2019\u2019c'")]
    [DataRow(ShellType.Nushell, "a'#b", "r##'a'#b'##")]
    public void LiteralArgumentsAreQuotedForTheirShell(ShellType shell, string value, string expected)
    {
        Assert.AreEqual(expected, ShellInfo.QuoteLiteralArgument(shell, value));
    }
}
