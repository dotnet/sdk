// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TestTemplates.Acceptance.Tests;

public record ExecutionResult(string Arguments, string StandardOutput, string StandardError, int ExitCode);

public static class ExecutionResultExtensions
{
    // this output is specific to the version of TP included with the runtime you are using to run the tests
    // if you see all tests failed, chances are that the output changed after you upgraded to latest version of dotnet
    private const string TestSummaryStatusMessageFormat = "Test Run Successful. Total tests: {0} Passed: {1}";

    /// <summary>
    /// Validate if the overall test count and results are matching.
    /// </summary>
    /// <param name="passedTestsCount">Passed test count</param>
    public static void ValidateSummaryStatus(this ExecutionResult executionResult, bool isTestingPlatform, int passedTestsCount)
    {
        if (isTestingPlatform)
        {
            ValidateTestingPlatformSummaryStatus(executionResult);
        }
        else
        {
            ValidateVSTestSummaryStatus(executionResult, passedTestsCount);
        }
    }

    private static void ValidateVSTestSummaryStatus(this ExecutionResult executionResult, int passedTestsCount)
    {
        var summaryStatus = string.Format(TestSummaryStatusMessageFormat, passedTestsCount, passedTestsCount);
        Assert.Contains(
            summaryStatus,
            executionResult.StandardOutput ?? string.Empty
        );
    }

    private static void ValidateTestingPlatformSummaryStatus(this ExecutionResult executionResult)
    {
        Assert.Contains(
            "Tests succeeded",
            executionResult.StandardOutput ?? string.Empty
        );
    }
}
