// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ApplyDeltaTests : DotNetWatchTestBase
    {
        public ApplyDeltaTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task ChangeFileInDependency()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            await App.StartWatcherAsync(testAsset, "AppWithDeps");

            await App.WaitForSessionStarted();

            var newSrc = """
                public class Lib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """;

            File.WriteAllText(Path.Combine(dependencyDir, "Foo.cs"), newSrc);
            await App.AssertOutputLineStartsWith("Changed!");
        }

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact]
        public async Task HandleTypeLoadFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
                .WithSource();

            await App.StartWatcherAsync(testAsset, "App");

            await App.WaitForSessionStarted();

            var newSrc = /* lang=c#-test */"""
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

            File.WriteAllText(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact]
        public async Task HandleMissingAssemblyFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppMissingAssemblyFailure")
                .WithSource();

            await App.StartWatcherAsync(testAsset, "App");

            await App.WaitForSessionStarted();

            var newSrc = /* lang=c#-test */"""
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

            // Delete all files in testAsset.Path named Dep.dll
            foreach (var depDll in Directory.GetFiles(testAsset.Path, "Dep.dll", SearchOption.AllDirectories))
            {
                File.Delete(depDll);
            }

            File.WriteAllText(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }
    }
}
