// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
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

        [Fact]
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


        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void ItDoesNotOverwriteFileWithSameContent()
        {
            // Execute task first time
            var task = CreateBasicTestTask();
            task.PublicExecuteCore();
            var firstWriteTime = File.GetLastWriteTimeUtc(_runtimeConfigPath);

            // Wait a bit to ensure timestamp would change if file is rewritten
            Thread.Sleep(100);

            // Execute task again with same configuration
            var task2 = CreateBasicTestTask();
            task2.PublicExecuteCore();
            var secondWriteTime = File.GetLastWriteTimeUtc(_runtimeConfigPath);

            // File should not have been rewritten when content is the same
            secondWriteTime.Should().Be(firstWriteTime, "file should not be rewritten when content is unchanged");
        }

        [Fact]
        public void GivenDifferentRuntimeHostOptionsItWritesNewConfig()
        {
            // Execute task first time
            var task = CreateBasicTestTask();
            task.PublicExecuteCore();
            var firstWriteTime = File.GetLastWriteTimeUtc(_runtimeConfigPath);

            // Wait a bit to ensure timestamp would change if file is rewritten
            Thread.Sleep(100);

            // Execute task again with different host options
            var task2 = CreateBasicTestTask();
            task2.HostConfigurationOptions = [
                new TaskItem("System.Runtime.TieredCompilation", new Dictionary<string, string>{{"Value", "false"}}),
                new TaskItem("System.GC.Concurrent", new Dictionary<string, string>{{"Value", "false"}}),
            ];
            task2.PublicExecuteCore();
            var secondWriteTime = File.GetLastWriteTimeUtc(_runtimeConfigPath);
            // File should have been rewritten when content is different
            secondWriteTime.Should().BeAfter(firstWriteTime, "file should be rewritten when content is different");
        }

        private TestableGenerateRuntimeConfigurationFiles CreateBasicTestTask()
        {
            return new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
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
        }

        private class TestableGenerateRuntimeConfigurationFiles : GenerateRuntimeConfigurationFiles
        {
            public void PublicExecuteCore()
            {
                base.ExecuteCore();
            }
        }
    }
}
