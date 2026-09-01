// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Combinatorial.MSTest;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class SourceFileUpdateTests_HotReloadNotSupported : DotNetWatchTestBase
{
    [TestMethod]
    [DataRow("PublishAot", "True")]
    [DataRow("PublishTrimmed", "True")]
    [DataRow("StartupHookSupport", "False")]
    [DataRow("Optimize", "True")]
    public async Task ChangeFileInAotProject_PriorNet11(string propertyName, string propertyValue)
    {
        var tfm = "net9.0";
        var projectDisplay = $"WatchHotReloadApp ({tfm})";

        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: $"{propertyName};{propertyValue}")
            .WithSource()
            .WithTargetFramework(tfm)
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

    [TestMethod]
    [CombinatorialData]
    public async Task ChangeFileInAotProject_Net11_DisabledInConfigDevFile(bool startupHookSupport)
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;
        var projectDisplay = $"WatchHotReloadApp ({tfm})";

        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: $"{startupHookSupport}")
            .WithSource()
            .WithTargetFramework(tfm)
            .WithProjectChanges(project =>
            {
                project.Root.Descendants()
                    .First(e => e.Name.LocalName == "PropertyGroup")
                    .Add(
                        XElement.Parse($"<EnableHotReloadInRuntimeConfigDevFile>false</EnableHotReloadInRuntimeConfigDevFile>"),
                        XElement.Parse($"<MetadataUpdaterSupport>{!startupHookSupport}</MetadataUpdaterSupport>"),
                        XElement.Parse($"<StartupHookSupport>{startupHookSupport}</StartupHookSupport>"));
            });

        var programPath = Path.Combine(testAsset.Path, "Program.cs");

        App.Start(testAsset, ["--non-interactive"]);

        var propertyName = startupHookSupport ? "MetadataUpdaterSupport" : "StartupHookSupport";
        var message = MessageDescriptor.ProjectDoesNotSupportHotReload_Property.GetMessage((propertyName, "False", "EnableHotReloadInRuntimeConfigDevFile", "True"));
        await App.WaitForOutputLineContaining($"[{projectDisplay}] {message}");
        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(programPath, content => content.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<updated>\");"));

        await App.WaitForOutputLineContaining($"[{projectDisplay}] [auto-restart] {programPath}(1,1): error ENC0097"); //  Applying source changes while the application is running is not supported by the runtime.
        await App.WaitForOutputLineContaining("<updated>");
    }

    [TestMethod]
    [CombinatorialData]
    public async Task ChangeFileInAotProject_Net11_EnabledInConfigFile(bool enabledInDevFile)
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;
        var projectDisplay = $"WatchHotReloadApp ({tfm})";

        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: $"{enabledInDevFile}")
            .WithSource()
            .WithTargetFramework(tfm)
            .WithProjectChanges(project =>
            {
                project.Root.Descendants()
                    .First(e => e.Name.LocalName == "PropertyGroup")
                    .Add(
                        XElement.Parse($"<EnableHotReloadInRuntimeConfigDevFile>{enabledInDevFile}</EnableHotReloadInRuntimeConfigDevFile>"),
                        XElement.Parse($"<MetadataUpdaterSupport>{!enabledInDevFile}</MetadataUpdaterSupport>"),
                        XElement.Parse($"<StartupHookSupport>{!enabledInDevFile}</StartupHookSupport>"));
            });

        var programPath = Path.Combine(testAsset.Path, "Program.cs");

        App.Start(testAsset, ["--non-interactive"]);

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
        App.AssertOutputDoesNotContain("⚠");
        App.Process.ClearOutput();

        UpdateSourceFile(programPath, content => content.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<updated>\");"));

        await App.WaitForOutputLineContaining(MessageDescriptor.ManagedCodeChangesApplied);
        await App.WaitForOutputLineContaining("<updated>");
    }

    [TestMethod]
    public async Task ChangeFileInFSharpProject()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

        UpdateSourceFile(Path.Combine(testAsset.Path, "Program.fs"), content => content.Replace("Hello World!", "<Updated>"));

        await App.WaitUntilOutputContains("<Updated>");
    }

    [TestMethod]
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
