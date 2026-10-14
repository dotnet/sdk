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

    [TestMethod]
    public void ShouldFailOnAllSkippedTests_UsesStrictPoliciesThatDoNotIgnoreZeroTests()
    {
        TestApplicationActionQueue.ShouldFailOnAllSkippedTests(
        [
            new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            new(FailOnAllSkippedTests: true, IgnoredExitCodes: "8"),
        ]).Should().BeFalse();

        TestApplicationActionQueue.ShouldFailOnAllSkippedTests(
        [
            new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            new(FailOnAllSkippedTests: true, IgnoredExitCodes: null),
        ]).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyExitCodeIgnorePolicy_RequiresEveryModuleToIgnoreTheExitCode()
    {
        TestApplicationActionQueue.ApplyExitCodeIgnorePolicy(
            TestExitCode.MinimumExpectedTestsPolicyViolation,
            [
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "9"),
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "8;9"),
            ]).Should().Be(TestExitCode.Success);

        TestApplicationActionQueue.ApplyExitCodeIgnorePolicy(
            TestExitCode.MinimumExpectedTestsPolicyViolation,
            [
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "9"),
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            ]).Should().Be(TestExitCode.MinimumExpectedTestsPolicyViolation);
    }
}
