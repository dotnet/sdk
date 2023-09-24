// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests;

public class GivenACheckRuntimeIdentifierTask : SdkTest
{
    private const string ProjectAssetsFileName = "project.assets.json";
    private const string UbuntuX64RuntimeIdentifier = "ubuntu-x64";
    private const string WindowsX64RuntimeIdentifier = "win-x64";
    private const string ExpectedErrorCode = "NETSDK1214";

    public GivenACheckRuntimeIdentifierTask(ITestOutputHelper log) : base(log) { }

    [Fact]
    public void ItShouldFailIfAssetsFileHasNoTargetRuntimeIdentifier()
    {
        var assetsFilePath = CreateProjectAssetsJsonFile(AssetsJsonWithoutAddedRuntimeIdentifiers,
            nameof(ItShouldFailIfAssetsFileHasNoTargetRuntimeIdentifier));

        var task = CreateTaskWithRuntimeIdentifier(assetsFilePath, WindowsX64RuntimeIdentifier);
        task.Execute();
        AssertThatExpectedBuildErrorHappened(task);
    }

    [Fact]
    public void ItShouldSucceedIfRuntimeIdentifierMatchesTheAssetFile()
    {
        var assetsFilePath = CreateProjectAssetsJsonFile(AssetsJsonWithWithAddedWinX64RuntimeIdentifier,
            nameof(ItShouldSucceedIfRuntimeIdentifierMatchesTheAssetFile));

        var task = CreateTaskWithRuntimeIdentifier(assetsFilePath, WindowsX64RuntimeIdentifier);
        task.Execute();
        AssertThatThereWereNoErrors(task);
    }

    [Fact]
    public void ItShouldFailIfTargetIdentifierDiffersFromAssetsFile()
    {
        var assetsFilePath = CreateProjectAssetsJsonFile(AssetsJsonWithWithAddedWinX64RuntimeIdentifier,
            nameof(ItShouldFailIfTargetIdentifierDiffersFromAssetsFile));

        var task = CreateTaskWithRuntimeIdentifier(assetsFilePath, UbuntuX64RuntimeIdentifier);
        task.Execute();
        AssertThatExpectedBuildErrorHappened(task);
    }

    private string CreateProjectAssetsJsonFile(string content, string testIdentifier)
    {
        var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testIdentifier);
        var assetsFilePath = Path.Combine(testDirectory.Path, ProjectAssetsFileName); 
        File.WriteAllText(assetsFilePath, content);
        return assetsFilePath;
    }

    private static CheckRuntimeIdentifier CreateTaskWithRuntimeIdentifier(string assetsFilePath, string msBuildRuntimeIdentifier)
    {
        return new CheckRuntimeIdentifier
        {
            ProjectAssetsFile = assetsFilePath,
            TargetFramework = "net8.0",
            RuntimeIdentifier = msBuildRuntimeIdentifier,
            BuildEngine = new MockBuildEngine()
        };
    }

    private static void AssertThatExpectedBuildErrorHappened(ITask task)
    {
        var engine = (MockBuildEngine)task.BuildEngine;
        engine.Errors.Should().HaveCount(1);
        var error = engine.Errors.Single();
        error.Code.Should().Be(ExpectedErrorCode);
    }

    private static void AssertThatThereWereNoErrors(ITask task)
    {
        var engine = (MockBuildEngine)task.BuildEngine;
        engine.Errors.Should().HaveCount(0);
    }

    private const string AssetsJsonWithoutAddedRuntimeIdentifiers = @"
    {
      ""version"": 3,
      ""targets"": {
        ""net8.0"": {}
      },
      ""libraries"": {},
      ""projectFileDependencyGroups"": {
        ""net8.0"": []
      },
      ""packageFolders"": {
        ""C:\\Users\\user\\.nuget\\packages\\"": {},
        ""C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages"": {}
      },
      ""project"": {
        ""version"": ""1.0.0"",
        ""restore"": {
          ""projectUniqueName"": ""C:\\Users\\user\\test\\another\\another.csproj"",
          ""projectName"": ""another"",
          ""projectPath"": ""C:\\Users\\user\\test\\another\\another.csproj"",
          ""packagesPath"": ""C:\\Users\\user\\.nuget\\packages\\"",
          ""outputPath"": ""C:\\Users\\user\\test\\another\\obj\\"",
          ""projectStyle"": ""PackageReference"",
          ""fallbackFolders"": [
            ""C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages""
          ],
          ""configFilePaths"": [
            ""C:\\Users\\user\\AppData\\Roaming\\NuGet\\NuGet.Config"",
            ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.FallbackLocation.config"",
            ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.Offline.config""
          ],
          ""originalTargetFrameworks"": [
            ""net8.0""
          ],
          ""sources"": {
            ""C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackages\\"": {},
            ""https://api.nuget.org/v3/index.json"": {}
          },
          ""frameworks"": {
            ""net8.0"": {
              ""targetAlias"": ""net8.0"",
              ""projectReferences"": {}
            }
          },
          ""warningProperties"": {
            ""warnAsError"": [
              ""NU1605""
            ]
          }
        },
        ""frameworks"": {
          ""net8.0"": {
            ""targetAlias"": ""net8.0"",
            ""imports"": [
              ""net461"",
              ""net462"",
              ""net47"",
              ""net471"",
              ""net472"",
              ""net48"",
              ""net481""
            ],
            ""assetTargetFallback"": true,
            ""warn"": true,
            ""frameworkReferences"": {
              ""Microsoft.NETCore.App"": {
                ""privateAssets"": ""all""
              }
            },
            ""runtimeIdentifierGraphPath"": ""C:\\Program Files\\dotnet\\sdk\\8.0.100-preview.3.23178.7\\RuntimeIdentifierGraph.json""
          }
        }
      }
    }";

    private const string AssetsJsonWithWithAddedWinX64RuntimeIdentifier = @"
    {
      ""version"": 3,
      ""targets"": {
        ""net8.0"": {},
        ""net8.0/win-x64"": {}
      },
      ""libraries"": {},
      ""projectFileDependencyGroups"": {
        ""net8.0"": []
      },
      ""packageFolders"": {
        ""C:\\Users\\user\\.nuget\\packages\\"": {},
        ""C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages"": {}
      },
      ""project"": {
        ""version"": ""1.0.0"",
        ""restore"": {
          ""projectUniqueName"": ""c:\\Users\\user\\test\\test.csproj"",
          ""projectName"": ""test"",
          ""projectPath"": ""c:\\Users\\user\\test\\test.csproj"",
          ""packagesPath"": ""C:\\Users\\user\\.nuget\\packages\\"",
          ""outputPath"": ""c:\\Users\\user\\test\\obj\\"",
          ""projectStyle"": ""PackageReference"",
          ""fallbackFolders"": [
            ""C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages""
          ],
          ""configFilePaths"": [
            ""C:\\Users\\user\\AppData\\Roaming\\NuGet\\NuGet.Config"",
            ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.FallbackLocation.config"",
            ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.Offline.config""
          ],
          ""originalTargetFrameworks"": [""net8.0""],
          ""sources"": {
            ""C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackages\\"": {},
            ""https://api.nuget.org/v3/index.json"": {}
          },
          ""frameworks"": {
            ""net8.0"": {
              ""targetAlias"": ""net8.0"",
              ""projectReferences"": {}
            }
          },
          ""warningProperties"": {
            ""warnAsError"": [""NU1605""]
          }
        },
        ""frameworks"": {
          ""net8.0"": {
            ""targetAlias"": ""net8.0"",
            ""imports"": [
              ""net461"",
              ""net462"",
              ""net47"",
              ""net471"",
              ""net472"",
              ""net48"",
              ""net481""
            ],
            ""assetTargetFallback"": true,
            ""warn"": true,
            ""downloadDependencies"": [
              {
                ""name"": ""Microsoft.AspNetCore.App.Runtime.win-x64"",
                ""version"": ""[8.0.0-preview.3.23177.8, 8.0.0-preview.3.23177.8]""
              },
              {
                ""name"": ""Microsoft.NETCore.App.Runtime.win-x64"",
                ""version"": ""[8.0.0-preview.3.23174.8, 8.0.0-preview.3.23174.8]""
              },
              {
                ""name"": ""Microsoft.WindowsDesktop.App.Runtime.win-x64"",
                ""version"": ""[8.0.0-preview.3.23178.1, 8.0.0-preview.3.23178.1]""
              }
            ],
            ""frameworkReferences"": {
              ""Microsoft.NETCore.App"": {
                ""privateAssets"": ""all""
              }
            },
            ""runtimeIdentifierGraphPath"": ""C:\\Program Files\\dotnet\\sdk\\8.0.100-preview.3.23178.7\\RuntimeIdentifierGraph.json""
          }
        },
        ""runtimes"": {
          ""win-x64"": {
            ""#import"": []
          }
        }
      }
    }";
}
