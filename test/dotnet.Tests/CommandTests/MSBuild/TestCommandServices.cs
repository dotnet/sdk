// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;
using Moq;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

internal static class TestCommandServices
{
    public static CommandServices CreateNonLLM()
    {
        var detector = new Mock<ILLMEnvironmentDetector>(MockBehavior.Strict);
        detector.Setup(d => d.IsLLMEnvironment()).Returns(false);
        return new CommandServices(detector.Object);
    }
}
