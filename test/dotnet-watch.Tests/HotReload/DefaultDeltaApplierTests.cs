// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using Moq;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class DefaultDeltaApplierTests
    {
        [Fact]
        public void Initialize_ConfiguresEnvironmentVariables()
        {
            var applier = new DefaultDeltaApplier(Mock.Of<IReporter>()) { SuppressNamedPipeForTests = true };
            var process = new ProcessSpec();

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = process,
            };

            var projectInfo = new ProjectInfo(
                "myproject.csproj",
                IsNetCoreApp: true,
                TargetFrameworkVersion: null,
                RuntimeIdentifier: "",
                DefaultAppHostRuntimeIdentifier: "",
                RunCommand: "",
                RunArguments: "",
                RunWorkingDirectory: "");

            applier.Initialize(state, projectInfo, CancellationToken.None);

            Assert.Equal("debug", process.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"]);
            Assert.NotEmpty(process.EnvironmentVariables["DOTNET_HOTRELOAD_NAMEDPIPE_NAME"]);
            Assert.NotEmpty(process.EnvironmentVariables.DotNetStartupHooks);
        }
    }
}
