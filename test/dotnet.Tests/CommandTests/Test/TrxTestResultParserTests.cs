// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
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

    // A verbatim TRX produced by a real browser-wasm MSTest run (net10.0|browser-wasm, MSTest 4.4.0
    // with the Microsoft.Testing.Extensions.TrxReport synchronous-on-wasm streaming store). This is the
    // exact shape the SDK reads back from a standalone wasm test host once WasmReportTrxSupported is
    // enabled, so it guards the real MTP/MSTest schema — full TestDefinitions/TestEntries/TestLists,
    // executionId/testType attributes the parser must ignore, and a NotExecuted result whose duration is
    // present-but-zero. Volatile GUID/timestamp values are irrelevant to the parser and left as captured.
    private const string RealBrowserWasmTrx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun id="d0c7e570-5fbb-4dd2-ad72-f2aa4751691d" name="@localhost 2026-07-21 01:58:05.2890000" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Times creation="2026-07-21T01:58:05.226Z" queuing="2026-07-21T01:58:05.226Z" start="2026-07-21T01:58:05.226Z" finish="2026-07-21T01:58:05.289Z" />
          <TestSettings name="default" id="4871e377-f7f5-4f12-a3e6-ad49097c11d2">
            <Deployment runDeploymentRoot="_localhost_2026-07-21_01_58_05.2890000" />
          </TestSettings>
          <Results>
            <UnitTestResult executionId="665dabec-8d94-41db-be3e-2a981e676d7e" testId="17e4c638-a4df-85a5-b266-6eedc4b8ca6b" testName="Passing" computerName="localhost" duration="00:00:00.0019845" startTime="2026-07-21T01:58:05.2640000+00:00" endTime="2026-07-21T01:58:05.2750000+00:00" testType="13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B" outcome="Passed" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" relativeResultsDirectory="665dabec-8d94-41db-be3e-2a981e676d7e" />
            <UnitTestResult executionId="8616215e-6b70-4edc-ae80-360781f19be7" testId="17abf7bd-da20-8281-8836-7b7705c0d362" testName="AnotherPassing" computerName="localhost" duration="00:00:00.0002051" startTime="2026-07-21T01:58:05.2790000+00:00" endTime="2026-07-21T01:58:05.2800000+00:00" testType="13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B" outcome="Passed" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" relativeResultsDirectory="8616215e-6b70-4edc-ae80-360781f19be7" />
            <UnitTestResult executionId="28cccab9-53b2-43ac-a0aa-b8e263a15685" testId="1b1d234c-8165-8069-9afd-885083345e54" testName="SkippedTest" computerName="localhost" duration="00:00:00.0000000" startTime="2026-07-21T01:58:05.2800000+00:00" endTime="2026-07-21T01:58:05.2810000+00:00" testType="13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B" outcome="NotExecuted" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" relativeResultsDirectory="28cccab9-53b2-43ac-a0aa-b8e263a15685" />
          </Results>
          <TestDefinitions>
            <UnitTest name="Passing" storage="wasmtests.dll" id="17e4c638-a4df-85a5-b266-6eedc4b8ca6b">
              <Execution id="665dabec-8d94-41db-be3e-2a981e676d7e" />
              <TestMethod codeBase="WasmTests.dll" adapterTypeName="executor://MSTestExtension/4.4.0-dev" className="WasmTests.SampleTests" name="Passing" />
            </UnitTest>
            <UnitTest name="AnotherPassing" storage="wasmtests.dll" id="17abf7bd-da20-8281-8836-7b7705c0d362">
              <Execution id="8616215e-6b70-4edc-ae80-360781f19be7" />
              <TestMethod codeBase="WasmTests.dll" adapterTypeName="executor://MSTestExtension/4.4.0-dev" className="WasmTests.SampleTests" name="AnotherPassing" />
            </UnitTest>
            <UnitTest name="SkippedTest" storage="wasmtests.dll" id="1b1d234c-8165-8069-9afd-885083345e54">
              <Execution id="28cccab9-53b2-43ac-a0aa-b8e263a15685" />
              <TestMethod codeBase="WasmTests.dll" adapterTypeName="executor://MSTestExtension/4.4.0-dev" className="WasmTests.SampleTests" name="SkippedTest" />
            </UnitTest>
          </TestDefinitions>
          <TestEntries>
            <TestEntry testId="17e4c638-a4df-85a5-b266-6eedc4b8ca6b" executionId="665dabec-8d94-41db-be3e-2a981e676d7e" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" />
            <TestEntry testId="17abf7bd-da20-8281-8836-7b7705c0d362" executionId="8616215e-6b70-4edc-ae80-360781f19be7" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" />
            <TestEntry testId="1b1d234c-8165-8069-9afd-885083345e54" executionId="28cccab9-53b2-43ac-a0aa-b8e263a15685" testListId="8C84FA94-04C1-424b-9868-57A2D4851A1D" />
          </TestEntries>
          <TestLists>
            <TestList name="Results Not in a List" id="8C84FA94-04C1-424b-9868-57A2D4851A1D" />
            <TestList name="All Loaded Results" id="19431567-8539-422a-85d7-44ee4e166bda" />
          </TestLists>
          <ResultSummary outcome="Completed">
            <Counters total="3" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="1" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
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

    // Guards the real MSTest-on-wasm TRX shape end-to-end: the SDK reads this exact schema back from a
    // standalone browser-wasm test host (via the Gateway/test-host POST) once WasmReportTrxSupported is
    // enabled. Written with a UTF-8 BOM to match what the TrxReport writer emits. The parser must ignore
    // the surrounding TestDefinitions/TestEntries/TestLists and the extra UnitTestResult attributes and
    // still surface the three per-test results and the run outcome.
    [TestMethod]
    public void TryParse_ReadsRealBrowserWasmMSTestTrx()
    {
        string path = WriteTempTrx(RealBrowserWasmTrx, withBom: true);
        try
        {
            var report = TrxTestResultParser.TryParse(path);

            report.Should().NotBeNull();
            report!.RunOutcome.Should().Be("Completed");
            report.Results.Should().HaveCount(3);

            var passing = report.Results[0];
            passing.DisplayName.Should().Be("Passing");
            passing.Uid.Should().Be("17e4c638-a4df-85a5-b266-6eedc4b8ca6b");
            passing.Outcome.Should().Be("Passed");
            passing.Duration.Should().Be(TimeSpan.Parse("00:00:00.0019845"));

            report.Results[1].DisplayName.Should().Be("AnotherPassing");
            report.Results[1].Outcome.Should().Be("Passed");

            var skipped = report.Results[2];
            skipped.DisplayName.Should().Be("SkippedTest");
            skipped.Outcome.Should().Be("NotExecuted");
            // A real wasm skip emits duration="00:00:00.0000000" (present-but-zero), not an absent attribute.
            skipped.Duration.Should().Be(TimeSpan.Zero);

            report.Results.Select(r => TestApplicationHandler.ToOutcome(r.Outcome).ToString())
                .Should().Equal("Passed", "Passed", "Skipped");
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

    private static string WriteTempTrx(string content, bool withBom = false)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom));
        return path;
    }
}
