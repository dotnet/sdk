// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Base class for all tests that create dotnet watch process.
/// </summary>
public abstract partial class DotNetWatchTestBase
{
    /// <summary>Set by the MSTest runtime before each test runs.</summary>
    public TestContext TestContext { get; set; } = null!;

    private DualOutputHelper? _logger;
    private WatchableApp? _app;
    private TestAssetsManager? _testAssetsManager;

    internal DualOutputHelper Logger
        => _logger ??= new DualOutputHelper(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));

    internal WatchableApp App
        => _app ??= WatchableApp.CreateDotnetWatchApp(Logger);

    internal TestAssetsManager TestAssets
        => _testAssetsManager ??= new TestAssetsManager(Logger);

    [TestInitialize]
    public void InitializeTest()
    {
        // Reset lazy state so each test gets a fresh logger/app
        _logger = null;
        _app = null;
        _testAssetsManager = null;
    }

    [TestCleanup]
    public async Task CleanupTestAsync()
    {
        Logger.Log("Disposing test");
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    internal TestAsset CopyTestAsset(
        string assetName,
        object[]? testParameters = null,
        [CallerMemberName] string callingMethod = "",
        [CallerFilePath] string? callerFilePath = null)
        => TestAssets.CopyTestAsset(assetName, callingMethod, callerFilePath, identifier: string.Join(";", testParameters ?? [])).WithSource();

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => Logger.Log(message, testPath, testLine);

    public void UpdateSourceFile(string path, string text, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
    {
        var existed = File.Exists(path);
        WriteAllText(path, text);
        Log($"File '{path}' " + (existed ? "updated" : "added"), testPath, testLine);
    }

    public void UpdateSourceFile(string path, Func<string, string> contentTransform, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => UpdateSourceFile(path, contentTransform(File.ReadAllText(path, Encoding.UTF8)), testPath, testLine);

    /// <summary>
    /// Replacement for <see cref="File.WriteAllText"/>, which fails to write to hidden file.
    /// Uses FileShare.Read so that dotnet-watch (via Roslyn workspace) can still read the file
    /// while it's being written, avoiding IOException file-lock races.
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(text);
        }

        // truncate the rest of the file content:
        stream.SetLength(stream.Position);
    }

    public void UpdateSourceFile(string path)
        => UpdateSourceFile(path, content => content);

    internal InProcTestWatcher CreateInProcWatcher(TestAsset testAsset, string[] args, string? workingDirectory = null)
    {
        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);
        var loggerFactory = new TestLoggerFactory(Logger);
        var eventObserver = new TestEventObserver();
        var observableLoggerFactory = new TestObservableLoggerFactory(eventObserver, loggerFactory);
        var environmentOptions = TestOptions.GetEnvironmentOptions(workingDirectory ?? testAsset.Path, testAsset);
        var processRunner = new ProcessRunner(environmentOptions.GetProcessCleanupTimeout());

        var program = Program.TryCreate(
           TestOptions.GetCommandLineOptions(["--verbose", .. args]),
           console,
           environmentOptions,
           observableLoggerFactory,
           reporter,
           out var errorCode);

        Assert.AreEqual(0, errorCode);
        Assert.IsNotNull(program);

        var serviceHolder = new StrongBox<TestRuntimeProcessLauncher?>();
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            serviceHolder.Value = s;
        });

        var context = program.CreateContext(processRunner);
        var watcher = new HotReloadDotNetWatcher(context, console, runtimeProcessLauncherFactory: factory, selectionPrompt: null);
        var shutdownSource = new CancellationTokenSource();

        return new InProcTestWatcher(Logger, watcher, context, eventObserver, reporter, console, serviceHolder, shutdownSource);
    }
}
