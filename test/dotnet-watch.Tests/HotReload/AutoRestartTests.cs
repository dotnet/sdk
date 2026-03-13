// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class AutoRestartTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [Theory]
        [CombinatorialData]
        public async Task AutoRestartOnRudeEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: nonInteractive.ToString())
                .WithSource();

            if (!nonInteractive)
            {
                testAsset = testAsset
                    .WithProjectChanges(project =>
                    {
                        project.Root.Descendants()
                            .First(e => e.Name.LocalName == "PropertyGroup")
                            .Add(XElement.Parse("""
                                <HotReloadAutoRestart>true</HotReloadAutoRestart>
                                """));
                    });
            }

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(39,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", "public virtual void F() { Console.WriteLine(1); }"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/51469")]
        [CombinatorialData]
        public async Task AutoRestartOnRuntimeRudeEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: nonInteractive.ToString())
                .WithSource();

            var tfm = ToolsetInfo.CurrentTargetFramework;
            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            // Changes the type of lambda without updating top-level code.
            // The loop will end up calling the old version of the lambda resulting in runtime rude edit.

            File.WriteAllText(programPath, """
                using System;
                using System.Threading;

                var d = C.F();

                while (true)
                {
                    Thread.Sleep(250);
                    d(1);
                }

                class C
                {
                    public static Action<int> F()
                    {
                        return a =>
                        {
                            Console.WriteLine(a.GetType());
                        };
                    }
                }
                """);

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("System.Int32");
            App.Process.ClearOutput();

            UpdateSourceFile(programPath, src => src.Replace("Action<int>", "Action<byte>"));

            // The following agent messages must be reported in order.
            // The HotReloadException handler needs to be installed and update handlers invoked and completed before the
            // HotReloadException handler may proceed with runtime rude edit processing and application restart.
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] HotReloadException handler installed.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Invoking metadata update handlers.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Updates applied.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Runtime rude edit detected:");

            await App.WaitUntilOutputContains($"dotnet watch ⚠ [WatchHotReloadApp ({tfm})] " +
                "Attempted to invoke a deleted lambda or local function implementation. " +
                "This can happen when lambda or local function is deleted while the application is running.");

            await App.WaitUntilOutputContains(MessageDescriptor.RestartingApplication, $"WatchHotReloadApp ({tfm})");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("System.Byte");
        }

        [Fact]
        public async Task AutoRestartOnRudeEditAfterRestartPrompt()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            // the prompt is printed into stdout while the error is printed into stderr, so they might arrive in any order:
            await App.WaitUntilOutputContains("  ❔ Do you want to restart your app? Yes (y) / No (n) / Always (a) / Never (v)");
            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);

            await App.WaitUntilOutputContains($"❌ {programPath}(39,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            App.Process.ClearOutput();

            App.SendKey('a');

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // rude edit: deleting virtual method
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", ""));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(39,1): error ENC0033: Deleting method 'F()' requires restarting the application.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
        }

        [Theory]
        [CombinatorialData]
        public async Task AutoRestartOnNoEffectEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: nonInteractive.ToString())
                .WithSource();

            if (!nonInteractive)
            {
                testAsset = testAsset
                    .WithProjectChanges(project =>
                    {
                        project.Root.Descendants()
                            .First(e => e.Name.LocalName == "PropertyGroup")
                            .Add(XElement.Parse("""
                                <HotReloadAutoRestart>true</HotReloadAutoRestart>
                                """));
                    });
            }

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // top-level code change:
            UpdateSourceFile(programPath, src => src.Replace("Started", "<Updated>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(17,19): warning ENC0118: Changing 'top-level code' might not have any effect until the application is restarted.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            await App.WaitUntilOutputContains("<Updated>");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public void F() {}"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        }
    }
}
