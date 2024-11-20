// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Base class for all tests that create dotnet watch process.
/// </summary>
public abstract class DotNetWatchTestBase : IDisposable
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

    public DebugTestOutputLogger Logger => (DebugTestOutputLogger)App.Logger;

    public void Log(string message)
        => Logger.WriteLine($"[TEST] {message}");

    public void UpdateSourceFile(string path, string text)
    {
        File.WriteAllText(path, text, Encoding.UTF8);
        Log($"File '{path}' updated ({HotReloadDotNetWatcher.FormatTimestamp(File.GetLastWriteTimeUtc(path))}).");
    }

    public void UpdateSourceFile(string path, Func<string, string> contentTransform)
    {
        File.WriteAllText(path, contentTransform(File.ReadAllText(path, Encoding.UTF8)), Encoding.UTF8);
        Log($"File '{path}' updated.");
    }

    public void UpdateSourceFile(string path)
        => UpdateSourceFile(path, content => content);

    public void Dispose()
    {
        App.Dispose();
    }
}
