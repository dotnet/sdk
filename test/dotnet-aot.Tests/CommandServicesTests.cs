// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class CommandServicesTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ForwardingUsesCommandServices(bool isLLMEnvironment)
    {
        var services = new CommandServices(new LLMEnvironmentDetector(isLLMEnvironment));
        var command = new MSBuildForwardingApp(MSBuildArgs.FromOtherArgs(), services: services);

        Assert.AreSame(services, command.Services);
        Assert.AreEqual(isLLMEnvironment, command.MSBuildArguments.Contains(Constants.TerminalLogger_DisableNodeDisplay));
        Assert.AreEqual(isLLMEnvironment, command.GetProcessStartInfo().Arguments.Contains(Constants.TerminalLogger_DisableNodeDisplay));
    }

    private sealed class LLMEnvironmentDetector(bool isLLMEnvironment) : ILLMEnvironmentDetector
    {
        public string? GetLLMEnvironment() => isLLMEnvironment ? "test" : null;

        public bool IsLLMEnvironment() => isLLMEnvironment;
    }
}
