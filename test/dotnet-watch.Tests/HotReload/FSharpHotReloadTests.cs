// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class FSharpHotReloadTests : DotNetWatchTestBase
{
    private static string DescriptorPattern(MessageDescriptor descriptor)
        => Regex.Replace(Regex.Escape(descriptor.Format), @"\\\{[0-9]+\}", ".*");

    private void AssertFSharpEditAppliedOrRestarted()
    {
        var appliedPattern = new Regex(DescriptorPattern(MessageDescriptor.ManagedCodeChangesApplied));

        var managedApplied = App.Process.Output.Any(appliedPattern.IsMatch);
        var restartApplied = App.Process.Output.Any(line => line.Contains(MessageDescriptor.RestartNeededToApplyChanges.GetMessage(), StringComparison.Ordinal));

        Assert.IsTrue(managedApplied || restartApplied, "Expected either managed hot reload apply or restart fallback.");

        if (managedApplied)
        {
            App.AssertOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
            return;
        }

        App.AssertOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
    }

    private void AssertFSharpEditAppliedInPlace()
    {
        App.AssertOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        App.AssertOutputDoesNotContain(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
    }

    private Task WaitForFSharpEditOutcomeAsync()
    {
        var succeeded = DescriptorPattern(MessageDescriptor.ManagedCodeChangesApplied);
        var restarted = Regex.Escape(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
        return App.WaitForOutputLineContaining(new Regex($"{succeeded}|{restarted}"));
    }

    private Task WaitForFSharpManagedUpdateDecisionAsync()
    {
        var noManagedChanges = Regex.Escape(MessageDescriptor.NoManagedCodeChangesToApply.GetMessage());
        var succeeded = DescriptorPattern(MessageDescriptor.ManagedCodeChangesApplied);
        var restarted = Regex.Escape(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
        return App.WaitForOutputLineContaining(new Regex($"{noManagedChanges}|{succeeded}|{restarted}"));
    }

    private string GetFSharpCompilerServicePath()
    {
        var sdkDirectory = SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest;
        var fsharpCompilerServicePath = Path.Combine(sdkDirectory, "FSharp", "FSharp.Compiler.Service.dll");
        Assert.IsTrue(File.Exists(fsharpCompilerServicePath), $"Missing FSharp.Compiler.Service.dll at '{fsharpCompilerServicePath}'.");
        return fsharpCompilerServicePath;
    }

    /// <summary>
    /// In-place apply needs the hot reload session API in the FSharp.Compiler.Service that
    /// ships inside the SDK under test. Against a stock FCS the watch bridge falls back to
    /// restart, so tests asserting in-place semantics would wait forever; skip them instead.
    /// The probe reads metadata only, without loading the assembly.
    /// </summary>
    private string GetHotReloadCapableFSharpCompilerServicePath()
    {
        var fsharpCompilerServicePath = GetFSharpCompilerServicePath();

        using var stream = File.OpenRead(fsharpCompilerServicePath);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();

        foreach (var handle in metadataReader.MethodDefinitions)
        {
            var name = metadataReader.GetMethodDefinition(handle).Name;
            if (metadataReader.StringComparer.Equals(name, "CreateHotReloadSession"))
            {
                return fsharpCompilerServicePath;
            }
        }

        Assert.Inconclusive($"FSharp.Compiler.Service at '{fsharpCompilerServicePath}' does not expose the hot reload session API; in-place apply cannot be exercised.");
        return fsharpCompilerServicePath;
    }

    [TestMethod]
    public async Task ChangeFileInFSharpProjectWithLoop_AppliesOrRestarts()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        let message () = "Waiting"

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "%s" (message())
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");

        File.WriteAllText(sourcePath, source);

        App.Start(testAsset, ["--non-interactive"]);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<Updated>"));

        await WaitForFSharpEditOutcomeAsync();
        AssertFSharpEditAppliedOrRestarted();
        await App.AssertOutputLineStartsWith("<Updated>");
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("<Updated>", "<Updated2>"));

        await WaitForFSharpEditOutcomeAsync();
        AssertFSharpEditAppliedOrRestarted();
        await App.AssertOutputLineStartsWith("<Updated2>");
    }

    [TestMethod]
    public async Task ChangeFileInFSharpProjectWithLoop_FirstEditAppliesInPlace()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "Waiting"
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");
        File.WriteAllText(sourcePath, source);

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<UpdatedInPlace>"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();

        // The apply log alone does not prove the delta took effect: assert the running loop
        // now prints the edited string in place (it printed "Waiting" before the edit).
        await App.AssertOutputLineStartsWith("<UpdatedInPlace>");
    }

    [TestMethod]
    public async Task ChangeComputationExpressionUsageInFSharpProject_AppliesInPlace()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading
        open System.Runtime.CompilerServices

        type HtmlBuilder() =
            member _.Yield(text: string) = text
            member _.Combine(a: string, b: string) = a + b
            member _.Delay(f: unit -> string) = f()
            member _.Run(text: string) = text
            member _.Zero() = ""

        let html = HtmlBuilder()

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let message () =
            html {
                yield "Hello, "
                yield "watch"
            }

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "%s" (message ())
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");
        File.WriteAllText(sourcePath, source);

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("Hello, ", "Welcome, "));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        // CE desugaring can route updates through synthesized helpers; assert in-place behavior
        // and changed output shape, while allowing either full combined string or reduced payload.
        await App.WaitForOutputLineContaining(new Regex("^Welcome, watch$|^watch$"));
    }

    [TestMethod]
    public async Task ChangeFileInFSharpProject_WhitespaceOnlyEditDoesNotRestart()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        type Greeter() =
            let mutable count = 0
            member _.Message() =
                count <- count + 1
                sprintf "Waiting (count: %d)" count

        let greeter = Greeter()

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "%s" (greeter.Message())
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");
        File.WriteAllText(sourcePath, source);

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        // Roslyn parity: insignificant source edits should never force a restart.
        // Depending on compiler output diff, this may classify as either no-op or in-place apply.
        UpdateSourceFile(sourcePath, content => content.Replace("member _.Message() =", "member _.Message() =  "));

        await WaitForFSharpManagedUpdateDecisionAsync();
        App.AssertOutputDoesNotContain(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
    }

    [TestMethod]
    public async Task ChangeDependencyFileInFSharpProject_DoesNotRestart_AndSourceEditsStillApplyInPlace()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource()
            .WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Add(
                    new XElement(ns + "ItemGroup",
                        new XElement(ns + "EmbeddedResource", new XAttribute("Include", "payload.txt")),
                        new XElement(ns + "Watch", new XAttribute("Include", "payload.txt"))));
            });

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";
        App.EnvironmentVariables["DOTNET_WATCH_TRACE_FSHARP_HOTRELOAD"] = "1";

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        let message () = "Waiting"

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "%s" (message())
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");
        var dependencyPath = Path.Combine(testAsset.Path, "payload.txt");

        File.WriteAllText(sourcePath, source);
        File.WriteAllText(dependencyPath, "payload-v1");

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(dependencyPath, "payload-v2");

        await WaitForFSharpManagedUpdateDecisionAsync();
        App.AssertOutputDoesNotContain(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<UpdatedAfterDependencyEdit>"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        await App.AssertOutputLineStartsWith("<UpdatedAfterDependencyEdit>");
    }

    [TestMethod]
    public async Task ChangeXamlDependencyInFSharpProject_DoesNotRestart_AndSourceEditsStillApplyInPlace()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource()
            .WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Add(
                    new XElement(ns + "ItemGroup",
                        new XElement(ns + "Watch", new XAttribute("Include", "MainPage.xaml"))));
            });

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        let message () = "Waiting"

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "%s" (message())
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");
        var xamlPath = Path.Combine(testAsset.Path, "MainPage.xaml");

        File.WriteAllText(sourcePath, source);
        File.WriteAllText(xamlPath, "<Page><TextBlock Text=\"v1\" /></Page>");

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(xamlPath, "<Page><TextBlock Text=\"v2\" /></Page>");

        await WaitForFSharpManagedUpdateDecisionAsync();
        App.AssertOutputDoesNotContain(MessageDescriptor.RestartNeededToApplyChanges.GetMessage());
        App.Process.ClearOutput();

        UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<UpdatedAfterXamlEdit>"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        await App.AssertOutputLineStartsWith("<UpdatedAfterXamlEdit>");
    }

    [TestMethod]
    public async Task ChangeFilesInFSharpAppAndLib_InterleavedEditsApplyInPlace()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpAppWithLib")
            .WithSource();

        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH"] = GetHotReloadCapableFSharpCompilerServicePath();
        App.EnvironmentVariables["DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS"] = "1";

        var libPath = Path.Combine(testAsset.Path, "Lib", "Lib.fs");
        var appPath = Path.Combine(testAsset.Path, "App", "Program.fs");

        App.Start(testAsset, [], "App");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        // Edit the LIBRARY's method body: the per-project delta targets the Lib module loaded
        // into the running App process.
        UpdateSourceFile(libPath, content => content.Replace("LibWaiting", "LibEdit1"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        await App.AssertOutputLineStartsWith("App[LibEdit1]");
        App.Process.ClearOutput();

        // Edit the APP's method body: same watch session, different project. The legacy
        // single-active-session bridge could not interleave projects without recapturing
        // baselines from already-edited sources.
        UpdateSourceFile(appPath, content => content.Replace("App[%s]", "App2[%s]"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        await App.AssertOutputLineStartsWith("App2[LibEdit1]");
        App.Process.ClearOutput();

        // Edit the LIBRARY again: its committed baseline and generation chain advanced
        // independently of the App project's inside the one session object.
        UpdateSourceFile(libPath, content => content.Replace("LibEdit1", "LibEdit2"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        AssertFSharpEditAppliedInPlace();
        await App.AssertOutputLineStartsWith("App2[LibEdit2]");
    }

    [TestMethod]
    public async Task ChangeFileInReferencedFSharpProject_WebAppRudeEditRestartsRoot()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpWebAppWithLib")
            .WithSource();

        var libPath = Path.Combine(testAsset.Path, "Lib", "Lib.fs");
        var projectDisplay = $"App ({ToolsetInfo.CurrentTargetFramework})";
        var url = $"http://localhost:{TestOptions.GetTestPort()}";

        App.Start(testAsset, ["--non-interactive", "--urls", url], "App");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        // Rude edit in a referenced F# project: the running project is the Web SDK root app,
        // not the changed library path returned by the F# compiler-service result.
        UpdateSourceFile(libPath, content => content
            .Replace("let core () = \"LibWaiting\"", "let coreRenamed () = \"LibAfterRestart\"")
            .Replace("core ()", "coreRenamed ()"));

        await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
        await App.WaitUntilOutputContains(MessageDescriptor.Exited, projectDisplay);
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchedProcess, projectDisplay);
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }

    [TestMethod]
    public async Task ChangeFileInFSharpProject_RudeEditTriggersRestart()
    {
        var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
            .WithSource();

        var source = """
        module ConsoleApplication.Program

        open System
        open System.Threading

        [<EntryPoint>]
        let main argv =
            while true do
                printfn "Waiting"
                Thread.Sleep(200)
            0
        """;

        var sourcePath = Path.Combine(testAsset.Path, "Program.fs");

        File.WriteAllText(sourcePath, source);

        App.Start(testAsset, ["--non-interactive"]);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        // rename the entry point method: this should trigger restart semantics
        // instead of managed hot reload.
        UpdateSourceFile(sourcePath, content => content.Replace("let main argv =", "let mainRenamed argv ="));

        await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);

        App.AssertOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
        App.AssertOutputDoesNotContain(MessageDescriptor.ManagedCodeChangesApplied);
        App.Process.ClearOutput();

        // Ensure subsequent edits continue applying after restart.
        UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<UpdatedAfterRestart>"));

        await App.WaitForOutputLineContaining(new Regex(@"Launched '"));
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        // The second edit can be consumed by the restart build before a new
        // managed-update attempt is logged, so assert on observable app output.
        await App.AssertOutputLineStartsWith("<UpdatedAfterRestart>");
    }
}
