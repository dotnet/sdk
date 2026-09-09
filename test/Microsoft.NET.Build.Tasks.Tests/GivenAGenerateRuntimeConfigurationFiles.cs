// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAGenerateRuntimeConfigurationFiles
    {
        private readonly string _runtimeConfigPath;
        private readonly string _runtimeConfigDevPath;

        public GivenAGenerateRuntimeConfigurationFiles()
        {
            string testTempDir = Path.Combine(Path.GetTempPath(), "dotnetSdkTests");
            Directory.CreateDirectory(testTempDir);
            _runtimeConfigPath =
                Path.Combine(testTempDir, nameof(ItCanGenerateWithoutAssetFile) + "runtimeconfig.json");
            _runtimeConfigDevPath =
                Path.Combine(testTempDir, nameof(ItCanGenerateWithoutAssetFile) + "runtimeconfig.dev.json");
            if (File.Exists(_runtimeConfigPath))
            {
                File.Delete(_runtimeConfigPath);
            }

            if (File.Exists(_runtimeConfigDevPath))
            {
                File.Delete(_runtimeConfigDevPath);
            }
        }

        [TestMethod]
        public void ItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}");
            File.Exists(_runtimeConfigDevPath).Should().BeFalse("No nuget involved, so no extra probing path");
        }

        [TestMethod]
        public void Given3RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.AspNetCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.AspNetCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""frameworks"": [
      {{
        ""name"": ""Microsoft.WindowsDesktop.App"",
        ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
      }},
      {{
        ""name"": ""Microsoft.AspNetCore.App"",
        ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
      }}
    ]
  }}
}}",
                    "There is no Microsoft.NETCore.App. And it is under frameworkS.");
        }

        [TestMethod]
        public void Given2RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.WindowsDesktop.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}",
                    "There is no Microsoft.NETCore.App.");
        }

        [TestMethod]
        public void GivenTargetMonikerItGeneratesShortName()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}");
        }

        [TestMethod]
        public void GivenHostConfigurationOptionsItWritesConfigProperties()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor",
                HostConfigurationOptions = new[]
                {
                    new MockTaskItem("System.GC.Server", new Dictionary<string, string> { {"Value", "true"} }),
                    new MockTaskItem("MaxThreads", new Dictionary<string, string> { {"Value", "42"} }),
                    new MockTaskItem("SomeText", new Dictionary<string, string> { {"Value", "hello"} })
                }
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            // configProperties is the [JsonExtensionData] bag, written after the declared
            // properties. Bool/int/string host option values keep their JSON types
            // (true, 42, "hello"), matching the previous reflection-based serializer.
            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }},
    ""configProperties"": {{
      ""System.GC.Server"": true,
      ""MaxThreads"": 42,
      ""SomeText"": ""hello""
    }}
  }}
}}");
        }

        [TestMethod]
        public void GivenAdditionalProbingPathsItWritesThemToMainConfig()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor",
                WriteAdditionalProbingPathsToMainConfig = true,
                AdditionalProbingPaths = new[]
                {
                    new MockTaskItem("/probing/path1", new Dictionary<string, string>()),
                    new MockTaskItem("/probing/path2", new Dictionary<string, string>())
                }
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }},
    ""additionalProbingPaths"": [
      ""/probing/path1"",
      ""/probing/path2""
    ]
  }}
}}");
        }

        [TestMethod]
        public void GivenUserRuntimeConfigItMergesExtensionData()
        {
            var userConfigPath = Path.Combine(
                Path.GetTempPath(), "dotnetSdkTests", nameof(GivenUserRuntimeConfigItMergesExtensionData) + ".template.json");
            File.WriteAllText(
                userConfigPath,
                "{\"configProperties\":{\"TestProperty\":true},\"customSection\":{\"value\":42}}");
            try
            {
                var task = new TestableGenerateRuntimeConfigurationFiles
                {
                    BuildEngine = new MockNeverCacheBuildEngine4(),
                    TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                    RuntimeConfigPath = _runtimeConfigPath,
                    RuntimeConfigDevPath = _runtimeConfigDevPath,
                    RuntimeFrameworks = new[]
                    {
                        new MockTaskItem(
                            "Microsoft.NETCore.App",
                            new Dictionary<string, string>
                            {
                                {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                            }
                        )
                    },
                    RollForward = "LatestMinor",
                    UserRuntimeConfig = userConfigPath
                };

                Action a = () => task.PublicExecuteCore();
                a.Should().NotThrow();

                // The user template's properties flow through [JsonExtensionData] and are written
                // after the declared properties, preserving nested objects and value types exactly.
                File.ReadAllText(_runtimeConfigPath).Should()
                    .Be(
                        $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }},
    ""configProperties"": {{
      ""TestProperty"": true
    }},
    ""customSection"": {{
      ""value"": 42
    }}
  }}
}}");
            }
            finally
            {
                if (File.Exists(userConfigPath))
                {
                    File.Delete(userConfigPath);
                }
            }
        }

        private class TestableGenerateRuntimeConfigurationFiles : GenerateRuntimeConfigurationFiles
        {
            public TestableGenerateRuntimeConfigurationFiles()
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            }

            public void PublicExecuteCore()
            {
                base.ExecuteCore();
            }
        }
    }
}
