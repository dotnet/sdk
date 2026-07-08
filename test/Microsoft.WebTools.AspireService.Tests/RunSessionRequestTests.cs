// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Aspire.Tools.Service.UnitTests;

[TestClass]
public class RunSessionRequestTests
{
    [TestMethod]
    public void RunSessionRequest_ToProjectLaunchRequest()
    {
        var request = new RunSessionRequest()
        {
            Arguments = [ "--someArg" ],
            Environment = 
            [
                new() { Name = "var1", Value = "value1"},
                new() { Name = "var2", Value = "value2"},
                new() { Name = "var3", Value = null},
            ],
            LaunchConfigurations =
            [
                new()
                {
                    ProjectPath = @"c:\test\Projects\project1.csproj",
                    LaunchType = RunSessionRequest.ProjectLaunchConfigurationType,
                    LaunchMode= RunSessionRequest.DebugLaunchMode,
                    LaunchProfile = "specificProfileName",
                    DisableLaunchProfile = true
                }
            ]
        };

        var info = request.ToProjectLaunchInformation();

        Assert.AreSequenceEqual(
        [
            "--someArg"
        ], info.Arguments);

        Assert.AreSequenceEqual(
        [
            "var1='value1'",
            "var2='value2'",
            "var3=''"
        ], info.Environment.Select(e => $"{e.Key}='{e.Value}'"));

        Assert.AreEqual(@"c:\test\Projects\project1.csproj", info.ProjectPath);
        Assert.IsTrue(info.Debug);
        Assert.AreEqual("specificProfileName", info.LaunchProfile);
        Assert.IsTrue(info.DisableLaunchProfile);
    }
}
