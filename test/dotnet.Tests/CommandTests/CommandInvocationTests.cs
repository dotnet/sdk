// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tests.Commands;

[TestClass]
[DoNotParallelize] // The test temporarily replaces the process-wide root command action.
public sealed class CommandInvocationTests : SdkTest
{
    [TestMethod]
    public void PassesCancellationTokenToCommandAction()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        bool cancellationObserved = false;
        var originalAction = Parser.RootCommand.Action;
        int exitCode;
        try
        {
            Parser.RootCommand.SetAction((_, cancellationToken) =>
            {
                cancellationTokenSource.Cancel();
                cancellationObserved = cancellationToken.IsCancellationRequested;
                return Task.FromResult(0);
            });

            exitCode = CommandInvocation.ExecuteInternalCommand(
                Parser.Parse([]),
                cancellationTokenSource.Token);
        }
        finally
        {
            Parser.RootCommand.Action = originalAction;
        }

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(cancellationObserved);
    }
}
