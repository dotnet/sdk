// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class LoggingTests : DotNetWatchTestBase
{
    [TestMethod]
    public void BrowserToolsNotSupportedByProjectSettings()
    {
        var reporter = new TestReporter(Logger);
        var logger = new LoggerFactory(reporter, LogLevel.Debug).CreateLogger(DotNetWatchContext.DefaultLogComponentName);

        logger.Log(LogEvents.BrowserToolsNotSupportedByProjectSettings);

        AssertEx.SequenceEqual(
            [(LogLevel.Warning, "Hot Reload in the browser is not supported due to project settings")],
            reporter.Messages);
    }

    [TestMethod]
    public async Task BinaryLogs()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
            .WithSource();

        var projectPath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");
        var logDir = Path.Combine(testAsset.Path, "logs");
        var binLogPath = Path.Combine(logDir, "Test.binlog");
        var binLogPathBase = Path.ChangeExtension(binLogPath, "").TrimEnd('.');

        Assert.IsFalse(Directory.Exists(logDir));

        App.SuppressVerboseLogging();
        App.Start(testAsset, ["--verbose", $"-bl:{binLogPath}"], testFlags: TestFlags.None);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        var expectedLogs = new List<string>()
        {
            // dotnet build log
            binLogPath,
            // dotnet run log
            binLogPathBase + "-dotnet-run.binlog",
            // initial DTB:
            binLogPathBase + "-dotnet-watch.DesignTimeBuild.WatchHotReloadApp.csproj.1.binlog"
        };

        VerifyExpectedLogFiles();

        UpdateSourceFile(projectPath, content => content.Replace("""
            <!-- items placeholder -->
            """,
            """
            <Using Include="System.Xml.Linq" />
            """));

        await App.WaitUntilOutputContains(MessageDescriptor.ReEvaluationCompleted);

        // project update triggered restore and DTB:
        expectedLogs.Add(binLogPathBase + "-dotnet-watch.Restore.WatchHotReloadApp.csproj.2.binlog");
        expectedLogs.Add(binLogPathBase + "-dotnet-watch.DesignTimeBuild.WatchHotReloadApp.csproj.3.binlog");

        VerifyExpectedLogFiles();

        void VerifyExpectedLogFiles()
        {
            AssertEx.SequenceEqual(
                expectedLogs.Order(),
                Directory.EnumerateFileSystemEntries(logDir, "*.*", SearchOption.AllDirectories).Order());
        }
    }
}
