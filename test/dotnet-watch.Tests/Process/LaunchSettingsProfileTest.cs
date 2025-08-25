// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class LaunchSettingsProfileTest
{
    private readonly ILogger _logger;
    private readonly TestAssetsManager _testAssets;

    public LaunchSettingsProfileTest(ITestOutputHelper output)
    {
        _logger = new TestLogger(output);
        _testAssets = new TestAssetsManager(output);
    }

    [Fact]
    public void LoadsLaunchProfiles()
    {
        var project = _testAssets.CreateTestProject(new TestProject("Project1")
        {
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
        });

        WriteFile(project, Path.Combine("Properties", "launchSettings.json"),
        """
        {
          "profiles": {
            "http": {
              "applicationUrl": "http://localhost:5000",
              "commandName": "Project",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            },
            "https": {
              "applicationUrl": "https://localhost:5001",
              "commandName": "Project",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }, // This comment and trailing comma shouldn't cause any issues
          }
        }
        """);

        var projectPath = Path.Combine(project.TestRoot, "Project1", "Project1.csproj");

        var expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, launchProfileName: "http", _logger);
        Assert.NotNull(expected);
        Assert.Equal("http://localhost:5000", expected.ApplicationUrl);

        expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, "https", _logger);
        Assert.NotNull(expected);
        Assert.Equal("https://localhost:5001", expected.ApplicationUrl);

        expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, "notfound", _logger);
        Assert.NotNull(expected);
    }

    [Fact]
    public void DefaultLaunchProfileWithoutProjectCommand()
    {
        var project = _testAssets.CreateTestProject(new TestProject("Project1")
        {
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
        });

        WriteFile(project, Path.Combine("Properties", "launchSettings.json"),
        """
        {
          "profiles": {
            "profile": {
              "applicationUrl": "http://localhost:5000"
            }
          }
        }
        """);

        var projectPath = Path.Combine(project.Path, "Project1", "Project1.csproj");

        var expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, launchProfileName: null, _logger);
        Assert.Null(expected);
    }

    private static string WriteFile(TestAsset testAsset, string name, string contents = "")
    {
        var path = Path.Combine(GetTestProjectDirectory(testAsset), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, contents);

        return path;
    }

    private static string GetTestProjectDirectory(TestAsset testAsset)
        => Path.Combine(testAsset.Path, testAsset.TestProject.Name);
}
