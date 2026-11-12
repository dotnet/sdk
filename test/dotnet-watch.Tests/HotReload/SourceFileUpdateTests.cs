// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class SourceFileUpdateTests : DotNetWatchTestBase
{
    [TestMethod]
    public async Task AddSourceFile()
    {
        Log("AddSourceFile started");

        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

        App.Start(testAsset, [], "AppWithDeps");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // add a new file:
        UpdateSourceFile(Path.Combine(dependencyDir, "AnotherLib.cs"), """
            public class AnotherLib
            {
                public static void Print()
                    => System.Console.WriteLine("Changed!");
            }
            """);

        await App.WaitUntilOutputContains(MessageDescriptor.ReEvaluationCompleted);

        // update existing file:
        UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), """
            public class Lib
            {
                public static void Print()
                    => AnotherLib.Print();
            }
            """);

        await App.WaitUntilOutputContains("Changed!");
    }

    [TestMethod]
    public async Task ChangeFileInDependency()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

        App.Start(testAsset, [], "AppWithDeps");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        var newSrc = """
            public class Lib
            {
                public static void Print()
                    => System.Console.WriteLine("Changed!");
            }
            """;

        UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), newSrc);

        await App.WaitUntilOutputContains("dotnet watch 🔥 Hot reload capabilities: AddExplicitInterfaceImplementation AddFieldRva AddInstanceFieldToExistingType AddMethodToExistingType AddStaticFieldToExistingType Baseline ChangeCustomAttributes GenericAddFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod NewTypeDefinition UpdateParameters.");

        await App.WaitUntilOutputContains("Changed!");
    }

    /// <summary>
    /// Adding a method, then changing only a referenced project, then deleting the method
    /// must not crash Hot Reload (<see href="https://github.com/dotnet/roslyn/issues/79898"/>).
    /// </summary>
    [TestMethod]
    public async Task DeleteAddedMethodAfterDependencyUpdate()
    {
        var testAsset = CopyTestAsset("WatchHotReloadApp");
        var programPath = Path.Combine(testAsset.Path, "Program.cs");
        var projectPath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");
        var libDir = Path.Combine(testAsset.Path, "Lib");
        var libPath = Path.Combine(libDir, "Lib.cs");

        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(libPath, """
            public class Lib
            {
                public static string Get() => "lib-v1";
            }
            """);

        // Nested Lib/ must not be compiled into the app (SDK includes **/*.cs).
        // Otherwise a Lib-only edit is also an App source change and cannot fork the App compilation.
        UpdateSourceFile(projectPath, src => src
            .Replace("<!-- properties placeholder -->", "<DefaultItemExcludes>$(DefaultItemExcludes);Lib/**</DefaultItemExcludes>")
            .Replace("<!-- items placeholder -->", """<ProjectReference Include="Lib/Lib.csproj" />"""));

        var appV1 = """
            using System;
            using System.Threading.Tasks;

            while (true)
            {
                Console.WriteLine("app-v1 " + Lib.Get());
                await Task.Delay(1000);
            }

            class C { }
            """;

        var appV2 = """
            using System;
            using System.Threading.Tasks;

            while (true)
            {
                Console.WriteLine("app-v2 " + Lib.Get());
                await Task.Delay(1000);
            }

            class C { }
            """;

        var appV2WithAddedMethod = """
            using System;
            using System.Threading.Tasks;

            while (true)
            {
                Console.WriteLine("app-v2 " + Lib.Get());
                await Task.Delay(1000);
            }

            class C
            {
                public string Added() => "added";
            }
            """;

        File.WriteAllText(programPath, appV1);

        await using var w = CreateInProcWatcher(testAsset, [], testAsset.Path);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var applied = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);

        var sawAppV1 = w.WaitForOutputAsync("app-v1 lib-v1");
        w.Start();

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);
        await sawAppV1;

        // 1. App body edit — establish an App emit baseline.
        var sawAppV2 = w.WaitForOutputAsync("app-v2 lib-v1");
        UpdateSourceFile(programPath, appV2);
        await sawAppV2;
        await applied.WaitAsync(w.ShutdownSource.Token);

        // 2. Add a method on App.
        UpdateSourceFile(programPath, appV2WithAddedMethod);
        await applied.WaitAsync(w.ShutdownSource.Token);

        // 3. Lib-only edit — App compilation forks via the updated project reference
        // without replacing App's emit baseline.
        var sawLibV2 = w.WaitForOutputAsync("app-v2 lib-v2");
        UpdateSourceFile(libPath, src => src.Replace("lib-v1", "lib-v2"));
        await sawLibV2;
        await applied.WaitAsync(w.ShutdownSource.Token);

        // 4. Delete the method added in step 2. This currently throws
        // NullReferenceException in DefinitionMap.GetPreviousMethodHandle.
        UpdateSourceFile(programPath, appV2);
        await applied.WaitAsync(w.ShutdownSource.Token);
    }

    /// <summary>
    /// Unchanged project doesn't build. Wait for source change and rebuild.
    /// </summary>
    [TestMethod]
    public async Task BaselineCompilationError()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        var programPath = Path.Combine(testAsset.Path, "Program.cs");
        File.WriteAllText(programPath,
            """
            Console.Write
            """);

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.FixBuildError);

        UpdateSourceFile(programPath, """
            System.Console.WriteLine("<Updated>");
            """);

        await App.WaitUntilOutputContains("<Updated>");
    }

    // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
    [TestMethod]
    [MSTestFramework::Microsoft.NET.TestFramework.CoreMSBuildOnly]
    public async Task HandleTypeLoadFailure()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
            .WithSource();

        App.Start(testAsset, [], "App");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        var newSrc = """
            class DepSubType : Dep
            {
                int F() => 2;
            }

            class Printer
            {
                public static void Print()
                {
                    Console.WriteLine("Changed!");
                }
            }
            """;

        UpdateSourceFile(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

        await App.WaitUntilOutputContains("Updated types: Printer");
    }

    // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
    [TestMethod]
    [MSTestFramework::Microsoft.NET.TestFramework.CoreMSBuildOnly]
    public async Task HandleMissingAssemblyFailure()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMissingAssemblyFailure")
            .WithSource();

        App.Start(testAsset, [], "App");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        var newSrc = /* lang=c#-test */"""
            using System;

            public class DepType
            {
                int F() => 1;
            }

            public class Printer
            {
                public static void Print()
                    => Console.WriteLine("Updated!");
            }
            """;

        // Delete all files in testAsset.Path named Dep.dll
        foreach (var depDll in Directory.GetFiles(testAsset.Path, "Dep2.dll", SearchOption.AllDirectories))
        {
            File.Delete(depDll);
        }

        File.WriteAllText(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

        await App.WaitUntilOutputContains("Updated types: Printer");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RenameSourceFile(bool useMove)
    {
        if (useMove)
        {
            Assert.Inconclusive("https://github.com/dotnet/sdk/issues/43320");
        }

        Log("RenameSourceFile started");

        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
        var oldFilePath = Path.Combine(dependencyDir, "Foo.cs");
        var newFilePath = Path.Combine(dependencyDir, "Renamed.cs");

        var source = """
            using System;
            using System.IO;
            using System.Runtime.CompilerServices;

            public class Lib
            {
                public static void Print() => PrintFileName();

                public static void PrintFileName([CallerFilePathAttribute] string filePath = null)
                {
                    Console.WriteLine($"> {Path.GetFileName(filePath)}");
                }
            }
            """;

        File.WriteAllText(oldFilePath, source);

        App.Start(testAsset, [], "AppWithDeps", testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // rename the file:
        if (useMove)
        {
            File.Move(oldFilePath, newFilePath);
        }
        else
        {
            File.Delete(oldFilePath);
            File.WriteAllText(newFilePath, source);
        }

        Log($"Renamed '{oldFilePath}' to '{newFilePath}'.");

        // Roslyn may classify the delete+add as a rude edit (ENC0033) depending on file-watcher
        // event timing, in which case dotnet-watch shows an interactive restart prompt. Send 'a'
        // to always restart so the test makes progress regardless of how Roslyn classifies the
        // change. See https://github.com/dotnet/sdk/pull/54292 discussion.
        App.SendKey('a');

        await App.AssertOutputLineStartsWith("> Renamed.cs");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RenameDirectory(bool useMove)
    {
        Log("RenameSourceFile started");

        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps", identifier: useMove.ToString())
            .WithSource();

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
        var oldSubdir = Path.Combine(dependencyDir, "Subdir");
        var newSubdir = Path.Combine(dependencyDir, "NewSubdir");

        var source = """
            using System;
            using System.IO;
            using System.Runtime.CompilerServices;

            public class Lib
            {
                public static void Print() => PrintDirectoryName();

                public static void PrintDirectoryName([CallerFilePathAttribute] string filePath = null)
                {
                    Console.WriteLine($"> {Path.GetFileName(Path.GetDirectoryName(filePath))}");
                }
            }
            """;

        File.Delete(Path.Combine(dependencyDir, "Foo.cs"));
        Directory.CreateDirectory(oldSubdir);
        File.WriteAllText(Path.Combine(oldSubdir, "Foo.cs"), source);

        App.Start(testAsset, ["--non-interactive"], "AppWithDeps");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // rename the directory:
        if (useMove)
        {
            Directory.Move(oldSubdir, newSubdir);
        }
        else
        {
            Directory.Delete(oldSubdir, recursive: true);
            Directory.CreateDirectory(newSubdir);
            File.WriteAllText(Path.Combine(newSubdir, "Foo.cs"), source);
        }

        Log($"Renamed '{oldSubdir}' to '{newSubdir}'.");

        // dotnet-watch may observe the delete separately from the new file write.
        // If so, rude edit is reported, the app is auto-restarted and we should observe the final result.

        await App.WaitUntilOutputContains("> NewSubdir");
    }
}
