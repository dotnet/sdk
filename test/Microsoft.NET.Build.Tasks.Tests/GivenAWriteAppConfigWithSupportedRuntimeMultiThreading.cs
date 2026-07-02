// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAWriteAppConfigWithSupportedRuntimeMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        [TestMethod]
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
