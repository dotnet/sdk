// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Moq;

namespace Microsoft.TemplateEngine.Cli.UnitTests.PostActionTests;

[TestClass]
public class ProcessStartPostActionProcessorTests
{
    [TestMethod]
    public void PropagatesCancellationToCommand()
    {
        using EnvironmentSettingsHelper environmentSettingsHelper = new();
        IEngineEnvironmentSettings environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        var command = new Mock<ICommand>(MockBehavior.Strict);
        command.Setup(c => c.CaptureStdOut()).Returns(command.Object);
        command.Setup(c => c.CaptureStdErr()).Returns(command.Object);
        command
            .Setup(c => c.Execute(cancellationTokenSource.Token))
            .Throws(new OperationCanceledException(cancellationTokenSource.Token));
        ProcessStartInfo? processStartInfo = null;
        ProcessStartPostActionProcessor processor = new(process =>
        {
            processStartInfo = process.StartInfo;
            return command.Object;
        });
        var postAction = new MockPostAction(default, default, default, default, default!)
        {
            Args = new Dictionary<string, string>
            {
                ["executable"] = "test-command",
                ["args"] = "--test",
            },
        };

        Assert.ThrowsExactly<OperationCanceledException>(() => processor.Process(
            environmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            environmentSettings.GetTempVirtualizedPath(),
            cancellationTokenSource.Token));

        Assert.IsNotNull(processStartInfo);
        Assert.AreEqual("test-command", processStartInfo.FileName);
        Assert.AreEqual("--test", processStartInfo.Arguments);
        command.VerifyAll();
    }
}
