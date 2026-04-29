// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [CollectionDefinition("CwdSensitive", DisableParallelization = true)]
    public sealed class CwdSensitiveCollection
    {
    }

    [Collection("CwdSensitive")]
    public class GivenAWriteAppConfigWithSupportedRuntimeMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        [Fact]
        public void DecoyCwdPathResolutionUsesTaskEnvironment()
        {
            string realWorkDir = CreateTempDirectory();
            string decoyWorkDir = CreateTempDirectory();

            string relativeAppConfigPath = "input.config";
            string appConfigPath = Path.Combine(realWorkDir, relativeAppConfigPath);
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string relativeOutputPath = Path.Combine("obj", "Debug", "output.config");
            string outputPath = Path.Combine(realWorkDir, relativeOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(realWorkDir);
            taskEnv.SetEnvironmentVariable("CWD_DECOY_TEST", decoyWorkDir);

            var engine = new MockBuildEngine();
            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                AppConfigFile = new MockTaskItem(relativeAppConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(relativeOutputPath, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.2"
            };

            string originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyWorkDir);
                task.Execute().Should().BeTrue("task should succeed even with decoy CWD");
                File.Exists(outputPath).Should().BeTrue("output should be written to TaskEnvironment-resolved path");
                File.Exists(Path.Combine(decoyWorkDir, relativeOutputPath)).Should().BeFalse("output should not be written to process CWD");
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

            string relativeAppConfigPath = "app.config";
            string appConfig1 = Path.Combine(dir1, relativeAppConfigPath);
            File.WriteAllText(appConfig1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string appConfig2 = Path.Combine(dir2, relativeAppConfigPath);
            File.WriteAllText(appConfig2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string relativeOutputPath = "out.config";
            string output1 = Path.Combine(dir1, relativeOutputPath);
            string output2 = Path.Combine(dir2, relativeOutputPath);

            var env1 = TaskEnvironmentHelper.CreateForTest(dir1);
            var env2 = TaskEnvironmentHelper.CreateForTest(dir2);

            var task1 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = env1,
                AppConfigFile = new MockTaskItem(relativeAppConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(relativeOutputPath, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.5"
            };

            var task2 = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = env2,
                AppConfigFile = new MockTaskItem(relativeAppConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(relativeOutputPath, new Dictionary<string, string>()),
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
        public void TaskEnvironmentMustBeInjected()
        {
            string workDir = CreateTempDirectory();

            string appConfigPath = Path.Combine(workDir, "app.config");
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string outputPath = Path.Combine(workDir, "out.config");

            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                AppConfigFile = new MockTaskItem(appConfigPath, new Dictionary<string, string>()),
                OutputAppConfigFile = new MockTaskItem(outputPath, new Dictionary<string, string>()),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.1"
            };

            Action execute = () => task.Execute();
            execute.Should().Throw<InvalidOperationException>()
                .WithMessage("*TaskEnvironment*");
            File.Exists(outputPath).Should().BeFalse("the task should not fall back to process CWD without TaskEnvironment");
        }

        [Fact]
        public void TaskWritesFileUsingTaskEnvironmentResolvedPathWithRealTaskItems()
        {
            string workDir = CreateTempDirectory();

            string relativeAppConfigPath = "app.config";
            string appConfigPath = Path.Combine(workDir, relativeAppConfigPath);
            File.WriteAllText(appConfigPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

            string relativeOutputPath = Path.Combine("obj", "Debug", "output.config");
            string expectedAbsolutePath = Path.Combine(workDir, relativeOutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(expectedAbsolutePath));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(workDir);

            var outputItem = new TaskItem(relativeOutputPath);

            var task = new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                AppConfigFile = new TaskItem(relativeAppConfigPath),
                OutputAppConfigFile = outputItem,
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.8"
            };

            task.Execute().Should().BeTrue("task should succeed with real MSBuild task items");

            File.Exists(expectedAbsolutePath).Should().BeTrue("output should exist at TaskEnvironment-resolved absolute path");
            outputItem.ItemSpec.Should().Be(relativeOutputPath, "the original output item path should be preserved");

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
            const int concurrency = 64;
            const string relativeAppConfigPath = "app.config";
            string relativeOutputPath = Path.Combine("obj", "Debug", "output.config");
            var tasks = new WriteAppConfigWithSupportedRuntime[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            using var readyGate = new CountdownEvent(concurrency);
            using var startGate = new ManualResetEventSlim(false);
            var versions = Enumerable.Range(0, concurrency).Select(i => $"v4.{i}").ToArray();

            for (int i = 0; i < concurrency; i++)
            {
                string workDir = CreateTempDirectory();
                string appConfigPath = Path.Combine(workDir, relativeAppConfigPath);
                File.WriteAllText(appConfigPath, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""Project"" value=""{i}"" />
  </appSettings>
</configuration>");

                string outputPath = Path.Combine(workDir, relativeOutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                tasks[i] = new WriteAppConfigWithSupportedRuntime
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(workDir),
                    AppConfigFile = new MockTaskItem(relativeAppConfigPath, new Dictionary<string, string>()),
                    OutputAppConfigFile = new MockTaskItem(relativeOutputPath, new Dictionary<string, string>()),
                    TargetFrameworkIdentifier = ".NETFramework",
                    TargetFrameworkVersion = versions[i]
                };
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = tasks[i];
                executeTasks[i] = Task.Factory.StartNew(() =>
                {
                    readyGate.Signal();
                    startGate.Wait();
                    return t.Execute();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            bool allWorkersReady = readyGate.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            startGate.Set();

            await Task.WhenAll(executeTasks);
            allWorkersReady.Should().BeTrue("all workers should be ready before the start gate opens");

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");

                string outputPath = tasks[i].OutputAppConfigFile.ItemSpec;
                Path.IsPathRooted(outputPath).Should().BeFalse($"task {i} should keep the shared relative output ItemSpec");
                AbsolutePath resolvedPath = tasks[i].TaskEnvironment.GetAbsolutePath(outputPath);
                File.Exists(resolvedPath.Value).Should().BeTrue($"output {i} should exist");

                string content = File.ReadAllText(resolvedPath.Value);
                content.Should().Contain($@"value=""{i}""", $"output {i} should come from its own ProjectDirectory");
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
