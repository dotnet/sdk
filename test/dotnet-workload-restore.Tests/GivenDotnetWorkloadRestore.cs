// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Workload.Restore.Tests;

public class GivenDotnetWorkloadRestore : SdkTest
{
    public GivenDotnetWorkloadRestore(ITestOutputHelper log) : base(log)
    {
    }

    public static string DcProjAssetName = "SolutionWithAppAndDcProj";

    [Fact]
    public void ProjectsThatDoNotSupportWorkloadsAreNotInspected()
    {
        var projectPath =
            _testAssetsManager
                .CopyTestAsset(DcProjAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
        .WithWorkingDirectory(projectPath)
        .Execute()
        .Should()
        // if we did try to restore the dcproj in this TestAsset we would fail, so passing means we didn't!
        .Pass();
    }
}
