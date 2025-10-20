// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal abstract class SimpleTerminal : ITerminal
{
    private object? _batchingLock;
    private bool _isBatching;

    public SimpleTerminal(IConsole console)
        => Console = console;

#pragma warning disable CA1416 // Validate platform compatibility
    public int Width => Console.IsOutputRedirected ? int.MaxValue : Console.BufferWidth;

    public int Height => Console.IsOutputRedirected ? int.MaxValue : Console.BufferHeight;

    protected IConsole Console { get; }

    public void Append(char value)
        => Console.Write(value);

    public virtual void Append(string value)
        => Console.Write(value);

    public void AppendLine()
        => Console.WriteLine();

    public virtual void AppendLine(string value)
        => Console.WriteLine(value);

    public void AppendLink(string path, int? lineNumber)
    {
        Append(path);
        if (lineNumber.HasValue)
        {
            Append($":{lineNumber}");
        }
    }

    public void EraseProgress()
    {
        // nop
    }

    public void HideCursor()
    {
        // nop
    }

    public void RenderProgress(TestProgressState?[] progress)
    {
        int count = 0;
        foreach (TestProgressState? p in progress)
        {
            if (p == null)
            {
                continue;
            }

            count++;

            string durationString = HumanReadableDurationFormatter.Render(p.Stopwatch.Elapsed);

            int passed = p.PassedTests;
            int failed = p.FailedTests;
            int skipped = p.SkippedTests;

            // Use just ascii here, so we don't put too many restrictions on fonts needing to
            // properly show unicode, or logs being saved in particular encoding.
            Append('[');
            SetColor(TerminalColor.DarkGreen);
            Append('+');
            Append(passed.ToString(CultureInfo.CurrentCulture));
            ResetColor();

            Append('/');

            SetColor(TerminalColor.DarkRed);
            Append('x');
            Append(failed.ToString(CultureInfo.CurrentCulture));
            ResetColor();

            Append('/');

            SetColor(TerminalColor.DarkYellow);
            Append('?');
            Append(skipped.ToString(CultureInfo.CurrentCulture));
            ResetColor();
            Append(']');

            Append(' ');
            Append(p.AssemblyName);

            if (p.TargetFramework != null || p.Architecture != null)
            {
                Append(" (");
                if (p.TargetFramework != null)
                {
                    Append(p.TargetFramework);
                    Append('|');
                }

                if (p.Architecture != null)
                {
                    Append(p.Architecture);
                }

                Append(')');
            }

            TestDetailState? activeTest = p.TestNodeResultsState?.GetRunningTasks(1).FirstOrDefault();
            if (!String.IsNullOrWhiteSpace(activeTest?.Text))
            {
                Append(" - ");
                Append(activeTest.Text);
                Append(' ');
            }

            Append(durationString);

            AppendLine();
        }

        // Do not render empty lines when there is nothing to show.
        if (count > 0)
        {
            AppendLine();
        }
    }

    public void ShowCursor()
    {
        // nop
    }

    public void StartBusyIndicator()
    {
        // nop
    }

    // TODO: Refactor NonAnsiTerminal and AnsiTerminal such that we don't need StartUpdate/StopUpdate.
    // It's much better if we use lock C# keyword instead of manually calling Monitor.Enter/Exit
    // Using lock also ensures we don't accidentally have `await`s in between that could cause Exit to be on a different thread.
    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException(CliCommandStrings.ConsoleIsAlreadyInBatchingMode);
        }

        bool lockTaken = false;

        // We store Console.Out in a field to make sure we will be doing
        // the Monitor.Exit call on the same instance.
        _batchingLock = System.Console.Out;

        // Note that we need to lock on System.Out for batching to work correctly.
        // Consider the following scenario:
        // 1. We call StartUpdate
        // 2. We call a Write("A")
        // 3. User calls Console.Write("B") from another thread.
        // 4. We call a Write("C").
        // 5. We call StopUpdate.
        // The expectation is that we see either ACB, or BAC, but not ABC.
        // Basically, when doing batching, we want to ensure that everything we write is
        // written continuously, without anything in-between.
        // One option (and we used to do it), is that we append to a StringBuilder while batching
        // Then at StopUpdate, we write the whole string at once.
        // This works to some extent, but we cannot get it to work when SetColor kicks in.
        // Console methods will internally lock on Console.Out, so we are locking on the same thing.
        // This locking is the easiest way to get coloring to work correctly while preventing
        // interleaving with user's calls to Console.Write methods.
        // One extra note:
        // It's very important to lock on Console.Out (the current Console.Out).
        // Consider the following scenario:
        // 1. SystemConsole captures the original Console.Out set by runtime.
        // 2. Framework author sets his own Console.Out which wraps the original Console.Out.
        // 3. Two threads are writing concurrently:
        //    - One thread is writing using Console.Write* APIs, which will use the Console.Out set by framework author.
        //    - The other thread is writing using NonAnsiTerminal.
        // 4. **If** we lock the original Console.Out. The following may happen (subject to race) [NOT THE CURRENT CASE - imaginary situation if we lock on the original Console.Out]:
        //    - First thread enters the Console.Write, which will acquire the lock for the current Console.Out (set by framework author).
        //    - Second thread executes StartUpdate, and acquires the lock for the original Console.Out.
        //    - First thread continues in the Write implementation of the framework author, which tries to run Console.Write on the original Console.Out.
        //    - First thread can't make any progress, because the second thread is holding the lock already.
        //    - Second thread continues execution, and reaches into runtime code (ConsolePal.WriteFromConsoleStream - on Unix) which tries to acquire the lock for the current Console.Out (set by framework author).
        //        - (see https://github.com/dotnet/runtime/blob/8a9d492444f06df20fcc5dfdcf7a6395af18361f/src/libraries/System.Console/src/System/ConsolePal.Unix.cs#L963)
        //    - No thread can progress.
        //    - Basically, what happened is that the first thread acquires the lock for current Console.Out, then for the original Console.Out.
        //    - while the second thread acquires the lock for the original Console.Out, then for the current Console.Out.
        //    - That's a typical deadlock where two threads are acquiring two locks in reverse order.
        // 5. By locking the *current* Console.Out, we avoid the situation described in 4.
        Monitor.Enter(_batchingLock, ref lockTaken);
        if (!lockTaken)
        {
            // Can this happen? :/
            throw new InvalidOperationException();
        }

        _isBatching = true;
    }

    public void StopBusyIndicator()
    {
        // nop
    }

    public void StopUpdate()
    {
        Monitor.Exit(_batchingLock!);
        _batchingLock = null;
        _isBatching = false;
    }

    public abstract void SetColor(TerminalColor color);

    public abstract void ResetColor();
}
