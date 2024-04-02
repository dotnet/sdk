// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests
{
    public class AppWithDepsTests : DotNetWatchTestBase
    {
        public AppWithDepsTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task ChangeFileInDependency()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource()
                .Path;

            var projectDir = Path.Combine(testAsset, "AppWithDeps");
            var dependencyDir = Path.Combine(testAsset, "Dependency");

            await App.StartWatcherAsync(projectDir);
            await App.AssertOutputLineStartsWith("Hello!");

            var newSrc = """
                public class Lib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """;

            var tcs = new TaskCompletionSource<bool>();

            // Create a FileSystemWatcher to monitor file changes
            using (var watcher = new FileSystemWatcher(dependencyDir))
            {
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Filter = "*.cs";
                watcher.EnableRaisingEvents = true;

                // Define the event handler for changed files
                watcher.Changed += (sender, e) =>
                {
                    if (e.FullPath == Path.Combine(dependencyDir, "Foo.cs"))
                    {
                        watcher.EnableRaisingEvents = false;
                        tcs.SetResult(true);
                    }
                };

                File.WriteAllText(Path.Combine(dependencyDir, "Foo.cs"), newSrc);

                // Wait for the Changed event to be triggered or for the timeout to expire
                if (await Task.WhenAny(tcs.Task, Task.Delay(15000)) != tcs.Task)
                {
                    watcher.EnableRaisingEvents = false;
                    tcs.SetResult(true);
                }
            }

            await App.AssertOutputLineStartsWith("Changed!");
        }
    }
}
