// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

// This test suite invokes the smoke-test.sh which should be considered legacy.  Those tests should be migrated to this test suite overtime.
public class SmokeTests
{
    private ITestOutputHelper OutputHelper { get; }
    private DotNetHelper DotNetHelper { get; }

    public SmokeTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
        DotNetHelper = new DotNetHelper(outputHelper);
    }

    [Fact]
    public void SmokeTestsScript()
    {
        string smokeTestArgs = $"--dotnetDir {Directory.GetParent(DotNetHelper.DotNetPath)} --projectOutput --archiveRestoredPackages --targetRid {Config.TargetRid}";
        if (Config.TargetRid.Contains("osx"))
        {
            smokeTestArgs += " --excludeWebHttpsTests";
        }
        if (Config.ExcludeOmniSharpTests)
        {
            smokeTestArgs += " --excludeOmniSharpTests";
        }
        if (Config.ExcludeOnlineTests)
        {
            smokeTestArgs += " --excludeOnlineTests";
        }

        (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess("./smoke-tests/smoke-test.sh", smokeTestArgs, OutputHelper);

        Assert.Equal(0, executeResult.Process.ExitCode);
    }
}
