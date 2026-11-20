// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Tools.Service;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AspireServiceFactoryTests
{
    [TestMethod]
    public void GetRunCommandArguments_Empty()
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = false,
            LaunchProfile = null,
            Environment = null,
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: null);

        AssertEx.SequenceEqual(["--project", "a.csproj"], args);
        AssertLaunchProfileName(request, hostLaunchProfile: null, expectedHasValue: true, expectedValue: null);
    }

    [TestMethod]
    public void GetRunCommandArguments_DisableLaunchProfile()
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = true,
            LaunchProfile = "P",
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--no-launch-profile" ], args);
        AssertLaunchProfileName(request, hostLaunchProfile: "H", expectedHasValue: false, expectedValue: null);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void GetRunCommandArguments_NoLaunchProfile_HostProfile(string? launchProfile)
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = false,
            LaunchProfile = launchProfile,
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--launch-profile", "H"], args);
        AssertLaunchProfileName(request, hostLaunchProfile: "H", expectedHasValue: true, expectedValue: "H");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void GetRunCommandArguments_DisableLaunchProfile_HostProfile(string? launchProfile)
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = true,
            LaunchProfile = launchProfile,
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--no-launch-profile"], args);
        AssertLaunchProfileName(request, hostLaunchProfile: "H", expectedHasValue: false, expectedValue: null);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void GetRunCommandArguments_NoLaunchProfile_NoHostProfile(string? launchProfile)
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = false,
            LaunchProfile = launchProfile,
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: null);

        AssertEx.SequenceEqual(["--project", "a.csproj"], args);
        AssertLaunchProfileName(request, hostLaunchProfile: null, expectedHasValue: true, expectedValue: null);
    }
    [TestMethod]
    public void GetRunCommandArguments_LaunchProfile_NoArgs()
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = null,
            DisableLaunchProfile = false,
            LaunchProfile = "P",
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--launch-profile", "P"], args);
        AssertLaunchProfileName(request, hostLaunchProfile: "H", expectedHasValue: true, expectedValue: "P");
    }

    [TestMethod]
    public void GetRunCommandArguments_LaunchProfile_EmptyArgs()
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = [],
            DisableLaunchProfile = false,
            LaunchProfile = "P",
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--launch-profile", "P", "--no-launch-profile-arguments"], args);
    }

    [TestMethod]
    public void GetRunCommandArguments_LaunchProfile_NonEmptyArgs()
    {
        var request = new ProjectLaunchRequest()
        {
            Arguments = ["a", "b"],
            DisableLaunchProfile = false,
            LaunchProfile = "P",
            Environment = [],
            ProjectPath = "a.csproj"
        };

        var args = AspireServiceFactory.SessionManager.GetRunCommandArguments(request, hostLaunchProfile: "H");

        AssertEx.SequenceEqual(["--project", "a.csproj", "--launch-profile", "P", "a", "b"], args);
    }

    private static void AssertLaunchProfileName(ProjectLaunchRequest request, string? hostLaunchProfile, bool expectedHasValue, string? expectedValue)
    {
        var launchProfileName = AspireServiceFactory.SessionManager.GetLaunchProfileName(request, hostLaunchProfile);
        Assert.AreEqual(expectedHasValue, launchProfileName.HasValue);
        Assert.AreEqual(expectedValue, launchProfileName.Value);
    }
}
