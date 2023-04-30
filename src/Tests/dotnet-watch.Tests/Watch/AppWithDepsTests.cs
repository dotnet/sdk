// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

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

            var fileToChange = Path.Combine(dependencyDir, "Foo.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await App.AssertRestarted();
        }
    }
}
