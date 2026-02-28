// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

internal record class InProcTestWatcher(
    DebugTestOutputLogger Logger,
    HotReloadDotNetWatcher Watcher,
    DotNetWatchContext Context,
    TestReporter Reporter,
    TestConsole Console,
    StrongBox<TestRuntimeProcessLauncher?> ServiceHolder,
    CancellationTokenSource ShutdownSource) : IAsyncDisposable
{
    public TestRuntimeProcessLauncher? Service => ServiceHolder.Value;
    private Task? _lazyTask;

    public void Start()
    {
        Assert.Null(_lazyTask);

        _lazyTask = Task.Run(async () =>
        {
            Logger.Log("Starting watch");

            try
            {
                await Watcher.WatchAsync(ShutdownSource.Token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ShutdownSource.Cancel();
                Logger.WriteLine($"Unexpected exception {e}");
                throw;
            }
            finally
            {
                Context.Dispose();
            }
        }, ShutdownSource.Token);
    }

    public async ValueTask DisposeAsync()
    {
        Assert.NotNull(_lazyTask);

        if (!ShutdownSource.IsCancellationRequested)
        {
            Logger.Log("Shutting down");
            ShutdownSource.Cancel();
        }

        try
        {
            await _lazyTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public TaskCompletionSource CreateCompletionSource()
    {
        var source = new TaskCompletionSource();
        ShutdownSource.Token.Register(() => source.TrySetCanceled(ShutdownSource.Token));
        return source;
    }
}
