// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

public class SourceFileUpdateTests_HotReloadNotSupported(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Theory]
    [InlineData("PublishAot", "True")]
    [InlineData("PublishTrimmed", "True")]
    [InlineData("StartupHookSupport", "False")]
    [InlineData("Optimize", "True")]
    [InlineData("MetadataUpdaterSupport", "False")]
    public async Task ChangeFileInAotProject(string propertyName, string propertyValue)
    {
        var tfvParsed = Version.Parse(ToolsetInfo.CurrentTargetFrameworkVersion);
        var isNet11OrNewer = tfvParsed.Major >= 11;

        // Optimize check only applies to < .NET 11; MetadataUpdaterSupport only to >= .NET 11.
        if (propertyName == "Optimize" && isNet11OrNewer)
            return;
        if (propertyName == "MetadataUpdaterSupport" && !isNet11OrNewer)
            return;

        var projectDisplay = $"WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})";

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

        // The warning message suggests which property to set to fix the issue.
        var (suggestedProperty, suggestedValue) = propertyName switch
        {
            "Optimize" => (PropertyNames.Optimize, "False"),
            "MetadataUpdaterSupport" => (PropertyNames.MetadataUpdaterSupport, "True"),
            _ => (PropertyNames.StartupHookSupport, "True"),
        };
        var message = MessageDescriptor.ProjectDoesNotSupportHotReload_Property.GetMessage((propertyName, propertyValue, suggestedProperty, suggestedValue));
        await App.WaitForOutputLineContaining($"[{projectDisplay}] {message}");
        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(programPath, content => content.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<updated>\");"));

        await App.WaitForOutputLineContaining($"[{projectDisplay}] [auto-restart] {programPath}(1,1): error ENC0097"); //  Applying source changes while the application is running is not supported by the runtime.
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
