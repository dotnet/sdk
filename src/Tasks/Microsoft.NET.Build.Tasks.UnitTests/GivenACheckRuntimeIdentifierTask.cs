// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests;

public class GivenACheckRuntimeIdentifierTask : SdkTest
{
    private const string AssetsFileName = "project.assets.json";
    private const string TargetFramework = "net8.0";
    private const string UbuntuX64RuntimeIdentifier = "ubuntu-x64";
    private const string WindowsX64RuntimeIdentifier = "win-x64";
    private const string ExpectedErrorCode = "NETSDK1214";

    private readonly CheckRuntimeIdentifier _task;
    private readonly string _assetsFilePath;

    public GivenACheckRuntimeIdentifierTask(ITestOutputHelper log) : base(log)
    {
        var testDirectory = _testAssetsManager.CreateTestDirectory();
        _assetsFilePath = Path.Combine(testDirectory.Path, AssetsFileName);
        _task = new CheckRuntimeIdentifier
        {
            ProjectAssetsFile = _assetsFilePath,
            TargetFramework = TargetFramework,
            BuildEngine = new MockBuildEngine()
        };
    }

    [Fact]
    public void ItShouldFailWithABuildErrorIfAssetsFileIsMissingTargetRuntimeIdentifier()
    {
        WriteToAssetsFile(AssetsJsonWithoutAddedRuntimeIdentifiers);
        _task.RuntimeIdentifier = WindowsX64RuntimeIdentifier;
        _task.Execute();
        AssertThatTaskThrewExpectedBuildError();
    }

    [Fact]
    public void ItShouldExecuteWithoutErrorsIfRuntimeIdentifierIsPresentInAssetsFile()
    {
        WriteToAssetsFile(AssetsJsonWithWithAddedWinX64RuntimeIdentifier);
        _task.RuntimeIdentifier = WindowsX64RuntimeIdentifier;
        _task.Execute();
        AssertThatTaskThrewNoErrors();
    }

    [Fact]
    public void ItShouldFailWithABuildErrorIfTargetIdentifierDiffersFromCurrentOne()
    {
        WriteToAssetsFile(AssetsJsonWithWithAddedWinX64RuntimeIdentifier);
        _task.RuntimeIdentifier = UbuntuX64RuntimeIdentifier;
        _task.Execute();
        AssertThatTaskThrewExpectedBuildError();
    }

    [Fact]
    public void ItShouldNotFailIfRuntimeIdentifierIsMissing()
    {
        WriteToAssetsFile(AssetsJsonWithWithAddedWinX64RuntimeIdentifier);
        _task.RuntimeIdentifier = null;
        _task.Execute();
        AssertThatTaskThrewNoErrors();
    }

    private void WriteToAssetsFile(string content) => File.WriteAllText(_assetsFilePath, content);

    private void AssertThatTaskThrewExpectedBuildError()
    {
        var engine = (MockBuildEngine)_task.BuildEngine;
        engine.Errors.Should().HaveCount(1);
        var error = engine.Errors.Single();
        error.Code.Should().Be(ExpectedErrorCode);
    }

    private void AssertThatTaskThrewNoErrors()
    {
        var engine = (MockBuildEngine)_task.BuildEngine;
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
