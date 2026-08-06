// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Watch.UnitTests;

namespace Aspire.Tools.Service.UnitTests;

public class RunSessionRequestTests
{
    [Fact]
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

        AssertEx.SequenceEqual(
        [
            "--someArg"
        ], info.Arguments);

        AssertEx.SequenceEqual(
        [
            "var1='value1'",
            "var2='value2'",
            "var3=''"
        ], info.Environment.Select(e => $"{e.Key}='{e.Value}'"));

        Assert.Equal(@"c:\test\Projects\project1.csproj", info.ProjectPath);
        Assert.True(info.Debug);
        Assert.Equal("specificProfileName", info.LaunchProfile);
        Assert.True(info.DisableLaunchProfile);
    }
}
