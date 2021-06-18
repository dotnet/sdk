// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class DefaultDeltaApplierTest
    {
        [Fact]
        public async Task InitializeAsync_ConfiguresEnvironmentVariables()
        {
            // Arrange
            var applier = new DefaultDeltaApplier(Mock.Of<IReporter>()) { SuppressNamedPipeForTests = true };
            var process = new ProcessSpec();
            var fileSet = new FileSet(null, new[]
            {
                new FileItem {  FilePath = "Test.cs" },
            });
            var context = new DotNetWatchContext { ProcessSpec = process, FileSet = fileSet, Iteration = 0 };

            // Act
            await applier.InitializeAsync(context, default);

            // Assert
            Assert.Equal("debug", process.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"]);
            Assert.NotEmpty(process.EnvironmentVariables["DOTNET_HOTRELOAD_NAMEDPIPE_NAME"]);
            Assert.NotEmpty(process.EnvironmentVariables.DotNetStartupHooks);
        }

        [Fact]
        public async Task InitializeAsync_ResetsRefreshBrowserAfterFileChange_ForBlazorProjects()
        {
            // Arrange
            var applier = new DefaultDeltaApplier(Mock.Of<IReporter>()) { SuppressNamedPipeForTests = true };
            var process = new ProcessSpec();
            var fileSet = new FileSet(null, new[]
            {
                new FileItem {  FilePath = "Index.razor" },
                new FileItem {  FilePath = "_Host.cshtml", }
            });
            var context = new DotNetWatchContext { ProcessSpec = process, FileSet = fileSet, Iteration = 0 };

            // Act
            await applier.InitializeAsync(context, default);

            // Assert
            Assert.False(applier.RefreshBrowserAfterFileChange);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        public async Task InitializeAsync_SetsRefreshBrowserAfterFileChange_ForProjectsWithoutRazorFiles(int iteration)
        {
            // Arrange
            var applier = new DefaultDeltaApplier(Mock.Of<IReporter>()) { SuppressNamedPipeForTests = true };
            var process = new ProcessSpec();
            var fileSet = new FileSet(null, new[]
            {
                new FileItem {  FilePath = "HomeController.cs", },
                new FileItem {  FilePath = "Index.cshtml", }
            });
            var context = new DotNetWatchContext { ProcessSpec = process, FileSet = fileSet, Iteration = iteration };

            // Act
            await applier.InitializeAsync(context, default);

            // Assert
            Assert.True(applier.RefreshBrowserAfterFileChange);
        }
    }
}
