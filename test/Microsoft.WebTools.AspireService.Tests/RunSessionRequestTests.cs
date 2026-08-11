// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.WebTools.AspireServer.Models;

namespace Microsoft.WebTools.AspireServer.UnitTests;

public class RunSessionRequestTests
{
    [Fact]
    public void RunSessionRequest_ToProjectLaunchRequest()
    {
        var runSessionReq = new RunSessionRequest()
        {
            Arguments = new string[] { "--someArg" },
            Environment = new EnvVar[]
             {
                new EnvVar { Name = "var1", Value = "value1"},
                new EnvVar { Name = "var2", Value = "value2"},
             },
            LaunchConfigurations = new LaunchConfiguration[]
            {
                new() {
                    ProjectPath = @"c:\test\Projects\project1.csproj",
                    LaunchType = RunSessionRequest.ProjectLaunchConfigurationType,
                    LaunchMode= RunSessionRequest.DebugLaunchMode,
                    LaunchProfile = "specificProfileName",
                    DisableLaunchProfile = true
                }
            }
        };

        var projectReq = runSessionReq.ToProjectLaunchInformation();

        Assert.Equal(runSessionReq.Arguments[0], projectReq.Arguments.First());
        Assert.Equal(runSessionReq.Environment.Length, projectReq.Environment.Count());
        Assert.Equal(runSessionReq.Environment[0].Name, projectReq.Environment.First().Key);
        Assert.Equal(runSessionReq.Environment[0].Value, projectReq.Environment.First().Value);
        Assert.Equal(runSessionReq.LaunchConfigurations[0].ProjectPath, projectReq.ProjectPath);
        Assert.True(projectReq.Debug);
        Assert.Equal(runSessionReq.LaunchConfigurations[0].LaunchProfile, projectReq.LaunchProfile);
        Assert.Equal(runSessionReq.LaunchConfigurations[0].DisableLaunchProfile, projectReq.DisableLaunchProfile);
    }
}
