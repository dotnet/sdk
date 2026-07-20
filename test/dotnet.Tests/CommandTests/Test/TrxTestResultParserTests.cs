// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TrxTestResultParserTests
{
    private const string SampleTrx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testId="11111111-1111-1111-1111-111111111111" testName="MyTests.PassingTest" outcome="Passed" duration="00:00:00.0120000">
              <Output><StdOut>hello from test</StdOut></Output>
            </UnitTestResult>
            <UnitTestResult testId="22222222-2222-2222-2222-222222222222" testName="MyTests.FailingTest" outcome="Failed" duration="00:00:00.0050000">
              <Output>
                <ErrorInfo>
                  <Message>Assert.AreEqual failed. Expected:&lt;4&gt;. Actual:&lt;5&gt;.</Message>
                  <StackTrace>at MyTests.FailingTest()</StackTrace>
                </ErrorInfo>
              </Output>
            </UnitTestResult>
            <UnitTestResult testId="33333333-3333-3333-3333-333333333333" testName="MyTests.SkippedTest" outcome="NotExecuted" />
          </Results>
          <ResultSummary outcome="Completed">
            <Counters total="3" executed="2" passed="1" failed="1" notExecuted="1" />
          </ResultSummary>
        </TestRun>
        """;

    [TestMethod]
    public void TryParse_ReadsResultsSummaryAndPerTestDetails()
    {
        string path = WriteTempTrx(SampleTrx);
        try
        {
            var report = TrxTestResultParser.TryParse(path);

            report.Should().NotBeNull();
            report!.RunOutcome.Should().Be("Completed");
            report.Results.Should().HaveCount(3);

            var passing = report.Results[0];
            passing.DisplayName.Should().Be("MyTests.PassingTest");
            passing.Uid.Should().Be("11111111-1111-1111-1111-111111111111");
            passing.Outcome.Should().Be("Passed");
            passing.Duration.Should().Be(TimeSpan.FromMilliseconds(12));
            passing.StandardOutput.Should().Be("hello from test");
            passing.ErrorMessage.Should().BeNull();

            var failing = report.Results[1];
            failing.Outcome.Should().Be("Failed");
            failing.ErrorMessage.Should().Contain("Expected:<4>. Actual:<5>.");
            failing.StackTrace.Should().Be("at MyTests.FailingTest()");

            var skipped = report.Results[2];
            skipped.Outcome.Should().Be("NotExecuted");
            skipped.Duration.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void TryParse_ReturnsNullWhenFileMissing()
    {
        TrxTestResultParser.TryParse(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx"))
            .Should().BeNull();
    }

    [TestMethod]
    public void TryParse_ReturnsNullForMalformedXml()
    {
        string path = WriteTempTrx("<TestRun><not-closed>");
        try
        {
            TrxTestResultParser.TryParse(path).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // MTP's TRX only emits Passed/Failed/NotExecuted; unknown/other values must map to a failure so
    // problems are never hidden. "Completed" is a run-level outcome and never a per-test @outcome.
    // Compared on the enum name because TestOutcome is internal to the CLI assembly.
    [TestMethod]
    [DataRow("Passed", "Passed")]
    [DataRow("Failed", "Fail")]
    [DataRow("NotExecuted", "Skipped")]
    [DataRow("Timeout", "Timeout")]
    [DataRow("Aborted", "Canceled")]
    [DataRow("Error", "Error")]
    [DataRow("SomethingUnknown", "Fail")]
    [DataRow("", "Fail")]
    public void ToOutcome_MapsTrxOutcomeStrings(string trxOutcome, string expected)
    {
        TestApplicationHandler.ToOutcome(trxOutcome).ToString().Should().Be(expected);
    }

    private static string WriteTempTrx(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, content);
        return path;
    }
}
