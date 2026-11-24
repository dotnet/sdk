// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.UnitTests;

[TestClass]
public class TemplateInvokerTests
{
    [TestMethod]
    public async Task PropagatesCancellationAndReportsUnsuccessfulTelemetry()
    {
        bool? telemetrySuccess = null;

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => TemplateInvoker.InvokeTemplateAsync(
                () => Task.FromException<NewCommandStatus>(new OperationCanceledException()),
                success => telemetrySuccess = success));

        Assert.IsFalse(telemetrySuccess);
    }
}
