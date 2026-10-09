// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotResourceTests
{
    [TestMethod]
    public void NativeClosureDoesNotEmbedTaskDiagnostics()
    {
        foreach (string name in typeof(NativeEntryPoint).Assembly.GetManifestResourceNames())
        {
            Assert.IsFalse(name.StartsWith("Microsoft.NET.Build.Tasks.Strings", StringComparison.Ordinal), name);
        }
    }
}
