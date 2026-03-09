// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class SourceFileUpdateTests_HotReloadNotSupported(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [Theory]
        [InlineData("PublishAot", "True")]
        [InlineData("PublishTrimmed", "True")]
        [InlineData("StartupHookSupport", "False")]
        public async Task ChangeFileInAotProject(string propertyName, string propertyValue)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: $"{propertyName};{propertyValue}")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    project.Root.Descendants()
                        .First(e => e.Name.LocalName == "PropertyGroup")
                        .Add(XElement.Parse($"<{propertyName}>{propertyValue}</{propertyName}>"));
                });

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, ["--non-interactive"]);

            await App.WaitForOutputLineContaining($"[WatchHotReloadApp ({tfm})] " + MessageDescriptor.ProjectDoesNotSupportHotReload.GetMessage($"'{propertyName}' property is '{propertyValue}'"));
            await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            UpdateSourceFile(programPath, content => content.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<updated>\");"));

            await App.WaitForOutputLineContaining($"[auto-restart] {programPath}(1,1): error ENC0097"); //  Applying source changes while the application is running is not supported by the runtime.
            await App.WaitForOutputLineContaining("<updated>");
        }

        [Fact]
        public async Task ChangeFileInFSharpProject()
        {
            var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
                .WithSource();

            App.Start(testAsset, []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            UpdateSourceFile(Path.Combine(testAsset.Path, "Program.fs"), content => content.Replace("Hello World!", "<Updated>"));

            await App.WaitUntilOutputContains("<Updated>");
        }

        [Fact]
        public async Task ChangeFileInFSharpProjectWithLoop()
        {
            var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
                .WithSource();

            var source = """
            module ConsoleApplication.Program

            open System
            open System.Threading

            [<EntryPoint>]
            let main argv =
                printfn "Waiting"
                Thread.Sleep(Timeout.Infinite)
                0
            """;

            var sourcePath = Path.Combine(testAsset.Path, "Program.fs");

            File.WriteAllText(sourcePath, source);

            App.Start(testAsset, ["--non-interactive"]);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<Updated>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("<Updated>");
            App.Process.ClearOutput();

            UpdateSourceFile(sourcePath, content => content.Replace("<Updated>", "<Updated2>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("<Updated2>");
        }
    }
}
