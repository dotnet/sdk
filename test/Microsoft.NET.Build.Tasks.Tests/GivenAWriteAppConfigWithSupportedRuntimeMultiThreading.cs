// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection("CwdSensitive")]
    public class GivenAWriteAppConfigWithSupportedRuntimeMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        [Fact]
        public void DecoyCwdPathResolutionUsesTaskEnvironment()
        {
            string realWorkDir = CreateTempDirectory();
            string decoyWorkDir = CreateTempDirectory();

            string appConfigPath = Path.Combine(realWorkDir, "input.config");
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string outputPath = Path.Combine(realWorkDir, "output.config");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(realWorkDir);
            taskEnv.SetEnvironmentVariable("CWD_DECOY_TEST", decoyWorkDir);

            var engine = new MockBuildEngine();
            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                AppConfigFile = new MockTaskItem(appConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(outputPath, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.2"
            };

            string originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyWorkDir);
                task.Execute().Should().BeTrue("task should succeed even with decoy CWD");
                File.Exists(outputPath).Should().BeTrue("output should be written to TaskEnvironment-resolved path");
                File.Exists(Path.Combine(decoyWorkDir, "output.config")).Should().BeFalse("output should not be written to process CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public async Task CrossThreadIsolationWithDifferentEnvironments()
        {
            string dir1 = CreateTempDirectory();
            string dir2 = CreateTempDirectory();

            string appConfig1 = Path.Combine(dir1, "app1.config");
            File.WriteAllText(appConfig1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string appConfig2 = Path.Combine(dir2, "app2.config");
            File.WriteAllText(appConfig2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string output1 = Path.Combine(dir1, "out1.config");
            string output2 = Path.Combine(dir2, "out2.config");

            var env1 = TaskEnvironmentHelper.CreateForTest(dir1);
            var env2 = TaskEnvironmentHelper.CreateForTest(dir2);

            var task1 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = env1,
                AppConfigFile = new MockTaskItem(appConfig1, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(output1, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.5"
            };

            var task2 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = env2,
                AppConfigFile = new MockTaskItem(appConfig2, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(output2, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.6.2"
            };

            var result1Task = Task.Run(() => task1.Execute());
            var result2Task = Task.Run(() => task2.Execute());

            await Task.WhenAll(result1Task, result2Task);

            (await result1Task).Should().BeTrue("task1 should succeed");
            (await result2Task).Should().BeTrue("task2 should succeed");

            File.Exists(output1).Should().BeTrue("output1 should exist");
            File.Exists(output2).Should().BeTrue("output2 should exist");

            string content1 = File.ReadAllText(output1);
            string content2 = File.ReadAllText(output2);

            content1.Should().Contain("v4.5", "output1 should contain v4.5");
            content2.Should().Contain("v4.6.2", "output2 should contain v4.6.2");

            content1.Should().NotContain("v4.6.2", "output1 should not contain v4.6.2");
            content2.Should().NotContain("v4.5", "output2 should not contain v4.5");
        }

        [Fact]
        public void MultiProcessParityWithAndWithoutTaskEnvironment()
        {
            string workDir = CreateTempDirectory();

            string appConfigPath = Path.Combine(workDir, "app.config");
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string output1 = Path.Combine(workDir, "out1.config");
            string output2 = Path.Combine(workDir, "out2.config");

            var task1 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                AppConfigFile = new MockTaskItem(appConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(output1, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.1"
            };

            var task2 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(workDir),
                AppConfigFile = new MockTaskItem(appConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(output2, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.1"
            };

            task1.Execute().Should().BeTrue("task1 should succeed");
            task2.Execute().Should().BeTrue("task2 should succeed");

            string content1 = File.ReadAllText(output1);
            string content2 = File.ReadAllText(output2);

            var doc1 = XDocument.Parse(content1);
            var doc2 = XDocument.Parse(content2);

            var runtime1 = doc1.Descendants("supportedRuntime").Single();
            var runtime2 = doc2.Descendants("supportedRuntime").Single();

            runtime1.Attribute("version").Value.Should().Be(runtime2.Attribute("version").Value);
            runtime1.Attribute("sku").Value.Should().Be(runtime2.Attribute("sku").Value);
        }

        [Fact]
        public void TaskWritesFileUsingTaskEnvironmentResolvedPath()
        {
            string workDir = CreateTempDirectory();

            string appConfigPath = Path.Combine(workDir, "app.config");
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string relativeOutputPath = "obj\\Debug\\output.config";
            string expectedAbsolutePath = Path.Combine(workDir, relativeOutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(expectedAbsolutePath));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(workDir);

            var outputItem = new MockTaskItem(relativeOutputPath, new Dictionary<string, string>());

            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                AppConfigFile = new MockTaskItem(appConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = outputItem,
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.8"
            };

            task.Execute().Should().BeTrue("task should succeed");

            File.Exists(expectedAbsolutePath).Should().BeTrue("output should exist at TaskEnvironment-resolved absolute path");

            string content = File.ReadAllText(expectedAbsolutePath);
            content.Should().Contain("v4.8", "output should contain the target framework version");
        }

        [Fact]
        public void CreatesOutputWhenAppConfigIsNull()
        {
            string workDir = CreateTempDirectory();
            string outputPath = Path.Combine(workDir, "output.config");

            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(workDir),
                AppConfigFile = null,
                OutputAppConfigFile = new MockTaskItem(outputPath, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.0"
            };

            task.Execute().Should().BeTrue("task should succeed with null AppConfigFile");
            File.Exists(outputPath).Should().BeTrue("output should be created");

            string content = File.ReadAllText(outputPath);
            var doc = XDocument.Parse(content);
            doc.Root.Name.LocalName.Should().Be("configuration");
            doc.Descendants("supportedRuntime").Should().ContainSingle();
        }

        [Fact]
        public async Task ConcurrentExecutionWithDifferentFrameworkVersions()
        {
            const int concurrency = 8;
            var tasks = new WriteAppConfigWithSupportedRuntime[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            var versions = new[] { "v4.0", "v4.5", "v4.5.1", "v4.5.2", "v4.6", "v4.6.1", "v4.6.2", "v4.7" };

            for (int i = 0; i < concurrency; i++)
            {
                string workDir = CreateTempDirectory();
                string outputPath = Path.Combine(workDir, $"out{i}.config");

                tasks[i] = new WriteAppConfigWithSupportedRuntime
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(workDir),
                    AppConfigFile = null,
                    OutputAppConfigFile = new MockTaskItem(outputPath, new Dictionary<string, string>()),
                    TargetFrameworkIdentifier = ".NETFramework",
                    TargetFrameworkVersion = versions[i]
                };
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = tasks[i];
                executeTasks[i] = Task.Run(() => t.Execute());
            }

            await Task.WhenAll(executeTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");

                string outputPath = tasks[i].OutputAppConfigFile.ItemSpec;
                AbsolutePath resolvedPath = tasks[i].TaskEnvironment.GetAbsolutePath(outputPath);
                File.Exists(resolvedPath.Value).Should().BeTrue($"output {i} should exist");

                string content = File.ReadAllText(resolvedPath.Value);
                var doc = XDocument.Parse(content);
                var supportedRuntime = doc.Descendants("supportedRuntime").Single();

                string expectedSku = $".NETFramework,Version={versions[i]}";
                supportedRuntime.Attribute("sku").Value.Should().Be(expectedSku, 
                    $"output {i} should have correct SKU for version {versions[i]}");

                for (int j = 0; j < concurrency; j++)
                {
                    if (i != j)
                    {
                        string otherSku = $".NETFramework,Version={versions[j]}";
                        supportedRuntime.Attribute("sku").Value.Should().NotBe(otherSku,
                            $"output {i} should not have SKU for version {versions[j]}");
                    }
                }
            }
        }

        private string CreateTempDirectory([CallerMemberName] string testName = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"WriteAppConfigTest_{testName}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            _tempDirs.Add(tempDir);
            return tempDir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
