// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class TerminationTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [PlatformSpecificFact(TestPlatforms.Windows)]
        public async Task GracefulTermination_Windows()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
               .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("Ctrl+C detected! Performing cleanup...");
                    Environment.Exit(0);
                };
                """));

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Windows Ctrl+C handling enabled.");

            await App.WaitUntilOutputContains("Started");

            App.SendControlC();

            await App.WaitUntilOutputContains("Ctrl+C detected! Performing cleanup...");
            await App.WaitUntilOutputContains("exited with exit code 0.");
        }

        [PlatformSpecificFact(TestPlatforms.AnyUnix)]
        public async Task GracefulTermination_Unix()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
               .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
                using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
                {
                    Console.WriteLine("SIGTERM detected! Performing cleanup...");
                });
                """));

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Posix signal handlers registered.");

            await App.WaitUntilOutputContains("Started");

            App.SendControlC();

            await App.WaitUntilOutputContains("SIGTERM detected! Performing cleanup...");
            await App.WaitUntilOutputContains("exited with exit code 0.");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public async Task GracefulTermination_WinExe()
        {
            // Test that WinExe apps (WinForms, WPF, MAUI) are terminated gracefully when dotnet-watch
            // sends Ctrl+C. The `dotnet run` process receives Ctrl+C and calls CloseMainWindow() on the
            // WinForms app. See https://github.com/dotnet/sdk/issues/52473

            var testAsset = TestAssets.CopyTestAsset("WinExeApp")
               .WithSource();

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

            // Wait for the WinForms app to start and show its window
            await App.WaitUntilOutputContains("Started");

            App.SendControlC();

            // The app should close gracefully via CloseMainWindow
            await App.WaitForOutputLineContaining("Closing gracefully: True");

            // The dotnet run process should exit with code 0, not be force-killed after timeout
            await App.WaitUntilOutputContains("exited with exit code 0.");
            App.AssertOutputDoesNotContain("(Kill)");
        }
    }
}
