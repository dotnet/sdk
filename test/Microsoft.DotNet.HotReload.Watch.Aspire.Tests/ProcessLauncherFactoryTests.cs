// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProcessLauncherFactoryTests
{
    private const string NoLaunchProfile = "<no launch profile>";

    /// <summary>
    /// The launch profile recorded in the project options is the one `dotnet run` uses given the command arguments.
    /// </summary>
    [TestMethod]
    [DataRow(NoLaunchProfile, "H", new[] { "--no-launch-profile", "x" }, false, null)]
    [DataRow("P", "H", new[] { "--launch-profile", "P", "x" }, true, "P")]
    [DataRow(null, "H", new[] { "--launch-profile", "H", "x" }, true, "H")]
    [DataRow("", "H", new[] { "--launch-profile", "H", "x" }, true, "H")]
    [DataRow(null, null, new[] { "x" }, true, null)]
    public void GetLaunchProfileName(string? launchProfileName, string? hostLaunchProfile, string[] expectedArguments, bool expectedHasValue, string? expectedValue)
    {
        var request = new LaunchResourceRequest()
        {
            EntryPoint = "a.csproj",
            ApplicationArguments = ["x"],
            EnvironmentVariables = new Dictionary<string, string>(),
            LaunchProfileName = launchProfileName == NoLaunchProfile ? Optional<string?>.NoValue : launchProfileName,
        };

        Assert.AreSequenceEqual(expectedArguments, ProcessLauncherFactory.Launcher.GetRunCommandArguments(request, hostLaunchProfile));

        var actual = ProcessLauncherFactory.Launcher.GetLaunchProfileName(request, hostLaunchProfile);
        Assert.AreEqual(expectedHasValue, actual.HasValue);
        Assert.AreEqual(expectedValue, actual.Value);
    }
}
