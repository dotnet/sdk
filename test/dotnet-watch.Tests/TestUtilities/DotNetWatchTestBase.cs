// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Base class for all tests that create dotnet watch process.
/// </summary>
public abstract partial class DotNetWatchTestBase : IDisposable
{
    internal TestAssetsManager TestAssets { get; }
    internal WatchableApp App { get; }

    public DotNetWatchTestBase(ITestOutputHelper logger)
    {
        var debugLogger = new DebugTestOutputLogger(logger);
        App = new WatchableApp(debugLogger);
        TestAssets = new TestAssetsManager(debugLogger);

        // disposes the test class if the test execution is cancelled:
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public void Dispose()
    {
        App.Dispose();
    }

    public DebugTestOutputLogger Logger => App.Logger;

    internal TestAsset CopyTestAsset(string assetName, params object[] testParameters)
        => TestAssets.CopyTestAsset(assetName, identifier: string.Join(";", testParameters)).WithSource();

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => App.Logger.Log(message, testPath, testLine);

    public void UpdateSourceFile(string path, string text, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
    {
        var existed = File.Exists(path);
        WriteAllText(path, text);
        Log($"File '{path}' " + (existed ? "updated" : "added"), testPath, testLine);
    }

    public void UpdateSourceFile(string path, Func<string, string> contentTransform, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => UpdateSourceFile(path, contentTransform(File.ReadAllText(path, Encoding.UTF8)), testPath, testLine);

    /// <summary>
    /// Replacement for <see cref="File.WriteAllText"/>, which fails to write to hidden file
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate);

        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(text);
        }

        // truncate the rest of the file content:
        stream.SetLength(stream.Position);
    }

    public void UpdateSourceFile(string path)
        => UpdateSourceFile(path, content => content);


    public enum TriggerEvent
    {
        HotReloadSessionStarting,
        HotReloadSessionStarted,
        WaitingForChanges,
    }

    internal InProcTestWatcher CreateInProcWatcher(TestAsset testAsset, string[] args, string? workingDirectory = null)
    {
        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);
        var loggerFactory = new LoggerFactory(reporter, LogLevel.Trace);
        var environmentOptions = TestOptions.GetEnvironmentOptions(workingDirectory ?? testAsset.Path, TestContext.Current.ToolsetUnderTest.DotNetHostPath, testAsset);
        var processRunner = new ProcessRunner(environmentOptions.GetProcessCleanupTimeout());

        var program = Program.TryCreate(
           TestOptions.GetCommandLineOptions(["--verbose", .. args]),
           console,
           environmentOptions,
           loggerFactory,
           reporter,
           out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        var serviceHolder = new StrongBox<TestRuntimeProcessLauncher?>();
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            serviceHolder.Value = s;
        });

        var context = program.CreateContext(processRunner);
        var watcher = new HotReloadDotNetWatcher(context, console, runtimeProcessLauncherFactory: factory);
        var shutdownSource = new CancellationTokenSource();

        return new InProcTestWatcher(Logger, watcher, context, reporter, console, serviceHolder, shutdownSource);
    }
}
