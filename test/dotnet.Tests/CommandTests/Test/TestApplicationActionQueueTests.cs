// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationActionQueueTests
{
    [TestMethod]
    public void NormalizeExitCode_ZeroTestsWithDisposeFailure_ReturnsGenericFailure()
    {
        int result = TestApplicationActionQueue.NormalizeExitCode(
            TestExitCode.ZeroTests,
            hasFailureDuringDispose: true);

        result.Should().Be(TestExitCode.GenericFailure);
    }
}
