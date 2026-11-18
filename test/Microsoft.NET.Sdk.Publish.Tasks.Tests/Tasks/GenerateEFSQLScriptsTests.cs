// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.Tasks
{
    [TestClass]
    public class GenerateEFSQLScriptsProcessTests : SdkTest
    {
        [TestMethod]
        public void FailedProcessLogsOnlyItsOwnOutput()
        {
            List<string> messages = [];
            Mock<IBuildEngine> engine = new();
            engine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(e => messages.Add(e.Message ?? ""));
            GenerateEFSQLScripts task = new() { BuildEngine = engine.Object };

            Assert.IsFalse(task.ExecuteProcess(Shell("first-out", "first-err", 7), TimeSpan.FromSeconds(30), isLoggingEnabled: true));
            Assert.IsFalse(task.ExecuteProcess(Shell("second-out", "second-err", 3), TimeSpan.FromSeconds(30), isLoggingEnabled: true));

            List<string> output = messages.Where(m => !m.StartsWith("Executing command:")).ToList();
            foreach (string marker in new[] { "first-out", "first-err", "second-out", "second-err" })
            {
                Assert.ContainsSingle(output.Where(m => m.Contains(marker)), marker);
            }
        }

#if NET
        [TestMethod]
        public void TimeoutKillsProcessTree()
        {
            string directory = TestAssetsManager.CreateTestDirectory().Path;
            string marker = Path.Combine(directory, "marker");
            // The direct child waits for a descendant that creates the marker after a delay.
            ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new("cmd.exe", "/c cmd /c \"ping -n 4 127.0.0.1 > nul & type nul > marker\"")
                : new("/bin/sh", "-c \"sh -c 'sleep 3; touch marker'; true\"");
            startInfo.WorkingDirectory = directory;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            GenerateEFSQLScripts task = new() { BuildEngine = Mock.Of<IBuildEngine>() };

            Assert.IsFalse(task.ExecuteProcess(startInfo, TimeSpan.FromSeconds(1), isLoggingEnabled: true));

            Thread.Sleep(TimeSpan.FromSeconds(6));
            Assert.IsFalse(File.Exists(marker), "The descendant process must be killed on timeout.");
        }
#endif

        private static ProcessStartInfo Shell(string output, string error, int exitCode)
        {
            ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new("cmd.exe", $"/c echo {output}& echo {error} 1>&2& exit /b {exitCode}")
                : new("/bin/sh", $"-c \"echo {output}; echo {error} >&2; exit {exitCode}\"");
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            return startInfo;
        }
    }

#if NET472
    [TestClass]
#endif
    public class GenerateEFSQLScriptsTests
    {
        private static readonly ITaskItem DefaultContext = new TaskItem("DefaultContext", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem CarContext = new TaskItem("CarContext", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=CarDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem PersonContext = new TaskItem("PersonContext", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=PersonDb;Trusted_Connection=True;MultipleActiveResultSets=true" } });

        private static readonly List<object[]> testData = new()
        {
            new object[] {new ITaskItem[] { DefaultContext } },
            new object[] {new ITaskItem[] { DefaultContext, CarContext, PersonContext } }
        };

        public static IEnumerable<object[]> EFMigrations
        {
            get { return testData; }
        }

#if NET472
        [TestMethod]
        [DynamicData(nameof(EFMigrations))]
        public void GenerateEFScripts_ReturnsFalse_forInValidContexts(ITaskItem[] efMigrationsData)
        {
            //Arrange
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(publishDir))
            {
                Directory.CreateDirectory(publishDir);
            }

            // Act
            GenerateEFSQLScripts task = new()
            {
                ProjectDirectory = projectFolder,
                EFPublishDirectory = publishDir,
                EFMigrations = efMigrationsData
            };

            bool isSucces = task.GenerateEFSQLScriptsInternal(false);

            // Assert
            Assert.IsFalse(isSucces);
        }
#endif
    }
}
