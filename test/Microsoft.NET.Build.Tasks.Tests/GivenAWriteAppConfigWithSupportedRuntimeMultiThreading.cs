// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
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
        public async Task ConcurrentExecutionWithDifferentFrameworkVersions()
        {
            const int concurrency = 8;
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
                executeTasks[i] = Task.Run(() =>
                {
                    readyGate.Signal();
                    startGate.Wait();
                    return t.Execute();
                });
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
