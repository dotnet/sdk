// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class ProcessReaperTests : SdkTest
    {
        // Regression test for https://github.com/dotnet/sdk/issues/55096.
        //
        // On Unix the reaper handles AppDomain.ProcessExit (raised when the CLI itself
        // receives SIGTERM and calls Environment.Exit) to forward the signal to the child.
        // That callback used to WaitOne on a Mutex that Dispose could dispose concurrently,
        // throwing "Cannot access a disposed object" during shutdown. This verifies that a
        // ProcessExit callback which runs after Dispose is a harmless no-op.
        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void ProcessExitCallbackAfterDisposeDoesNotThrow()
        {
            int savedExitCode = Environment.ExitCode;
            try
            {
                using var process = CreateShortLivedProcess();

                var reaper = ProcessReaper.Create(process);
                process.Start();
                reaper.NotifyProcessStarted();
                process.WaitForExit();

                var handleProcessExit = GetProcessExitCallback(reaper);

                // Dispose first (this disposed the shutdown handle in the buggy version),
                // then run the ProcessExit callback that raced with it.
                reaper.Dispose();

                // In the buggy version this threw ObjectDisposedException on the disposed
                // Mutex's SafeWaitHandle. It must now be a no-op.
                handleProcessExit(sender: null, e: EventArgs.Empty);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        // Stress the actual race: Dispose on one thread and the ProcessExit callback on
        // another. After the fix this is race-free and must never throw.
        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void ProcessExitCallbackRacingDisposeDoesNotThrow()
        {
            int savedExitCode = Environment.ExitCode;
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    using var process = CreateShortLivedProcess();

                    var reaper = ProcessReaper.Create(process);
                    process.Start();
                    reaper.NotifyProcessStarted();
                    process.WaitForExit();

                    var handleProcessExit = GetProcessExitCallback(reaper);

                    Exception callbackException = null;
                    var callbackThread = new Thread(() =>
                    {
                        try
                        {
                            handleProcessExit(sender: null, e: EventArgs.Empty);
                        }
                        catch (Exception e)
                        {
                            callbackException = e;
                        }
                    });

                    callbackThread.Start();
                    reaper.Dispose();
                    callbackThread.Join();

                    callbackException.Should().BeNull(
                        $"the ProcessExit callback must not throw when racing Dispose (iteration {i})");
                }
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        private static Process CreateShortLivedProcess()
        {
            // Unix-only test (Windows is excluded), so /bin/sh is available. Exit
            // immediately so WaitForExit inside the callback returns promptly.
            var startInfo = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
            };

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("exit 0");

            return new Process { StartInfo = startInfo };
        }

        private static EventHandler GetProcessExitCallback(ProcessReaper reaper)
        {
            // HandleProcessExit is a private instance method on the internal
            // UnixProcessReaper. It matches the EventHandler signature, so bind it as a
            // delegate to invoke it directly (exceptions surface without wrapping).
            MethodInfo method = reaper.GetType().GetMethod(
                "HandleProcessExit",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Should().NotBeNull("UnixProcessReaper.HandleProcessExit should exist on Unix");

            return (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), reaper, method);
        }
    }
}
