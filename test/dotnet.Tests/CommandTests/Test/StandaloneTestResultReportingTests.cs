// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class StandaloneTestResultReportingTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private const string OneFailingTestTrx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testId="22222222-2222-2222-2222-222222222222" testName="MyTests.FailingTest" outcome="Failed" duration="00:00:00.0050000">
              <Output>
                <ErrorInfo>
                  <Message>Assert.AreEqual failed. Expected:&lt;4&gt;. Actual:&lt;5&gt;.</Message>
                  <StackTrace>at MyTests.FailingTest()</StackTrace>
                </ErrorInfo>
              </Output>
            </UnitTestResult>
          </Results>
          <ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="0" failed="1" /></ResultSummary>
        </TestRun>
        """;

    [TestMethod]
    public void ReportStandaloneResults_WithTrx_RendersResultsNotHandshakeFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler();

        string trxPath = WriteTempTrx(OneFailingTestTrx);
        try
        {
            handler.ReportStandaloneResults(exitCode: 1, trxPath, outputData: "std-out", errorData: "std-err");
        }
        finally
        {
            File.Delete(trxPath);
        }

        // Results were replayed into the reporter, so this must NOT be treated as a handshake failure.
        reporter.HasHandshakeFailure.Should().BeFalse();

        string rendered = console.GetOutput();
        rendered.Should().Contain("MyTests.FailingTest");
        rendered.Should().Contain("Expected:<4>");
    }

    [TestMethod]
    public void ReportStandaloneResults_MissingTrx_RoutesToHandshakeFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler();

        // A missing TRX means the host crashed before finishing; surface it as a handshake failure.
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx");
        handler.ReportStandaloneResults(exitCode: 139, missing, outputData: "crash-out", errorData: "crash-err");

        reporter.HasHandshakeFailure.Should().BeTrue();
        console.GetOutput().Should().Contain("crash-out");
    }

    private (TestApplicationHandler, TerminalTestReporter, CapturingConsole) CreateHandler()
    {
        var console = new CapturingConsole();
        var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        });
        _disposables.Add(reporter);

        const string targetPath = "/repo/bin/Debug/net11.0-browser/MyTest.dll";
        var module = new TestModule(
            RunProperties: new RunProperties("dotnet", targetPath, "/repo", "browser-wasm", string.Empty, string.Empty),
            ProjectFullPath: "/repo/MyTest.csproj",
            TargetFramework: "net11.0-browser",
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());

        var handler = new TestApplicationHandler(reporter, module, new TestOptions(IsHelp: false, IsDiscovery: false, ListTestsFormat: TestListFormat.Text));
        return (handler, reporter, console);
    }

    private static string WriteTempTrx(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, content);
        return path;
    }
}
