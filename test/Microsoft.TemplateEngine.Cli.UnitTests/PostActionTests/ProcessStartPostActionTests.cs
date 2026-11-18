// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

using Moq;

namespace Microsoft.TemplateEngine.Cli.UnitTests.PostActionTests
{
    [TestClass]
    // These tests mutate the process-global Microsoft.DotNet.Cli.Utils.Reporter.
    [ResourceLock(nameof(Reporter))]
    public class ProcessStartPostActionTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        public async Task ReportsLargeRedirectedOutputOnFailure(bool redirectOutput, bool redirectError)
        {
            // Write more than a pipe buffer holds, so waiting for exit before reading would deadlock.
            string script;
            string executable;
            if (OperatingSystem.IsWindows())
            {
                executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                script = "-NoProfile -NonInteractive -Command \""
                    + (redirectOutput ? "[Console]::Out.Write('x' * 100000); [Console]::Out.WriteLine('final output'); " : "")
                    + (redirectError ? "[Console]::Error.Write('x' * 100000); [Console]::Error.WriteLine('final error'); " : "")
                    + "exit 7\"";
            }
            else
            {
                executable = "/bin/sh";
                script = "-c \""
                    + (redirectOutput ? "head -c 100000 /dev/zero | tr '\\0' x; echo final output; " : "")
                    + (redirectError ? "head -c 100000 /dev/zero | tr '\\0' x >&2; echo final error >&2; " : "")
                    + "exit 7\"";
            }

            using EnvironmentSettingsHelper settingsHelper = new();
            IEngineEnvironmentSettings environment = settingsHelper.CreateEnvironment(virtualize: false);
            MockPostAction postAction = new(default, default, default, default, default!)
            {
                Args = new Dictionary<string, string>
                {
                    ["executable"] = executable,
                    ["args"] = script,
                    ["redirectStandardOutput"] = redirectOutput.ToString(),
                    ["redirectStandardError"] = redirectError.ToString(),
                }
            };

            IReporter originalError = Reporter.Error;
            List<string> reported = [];
            Mock<IReporter> error = new();
            error.Setup(reporter => reporter.WriteLine(It.IsAny<string>())).Callback<string>(reported.Add);
            Reporter.SetError(error.Object);
            try
            {
                bool succeeded = await Task.Run(() => new ProcessStartPostActionProcessor().Process(
                    environment, postAction, new MockCreationEffects(), new MockCreationResult(), Path.GetTempPath()))
                    .WaitAsync(TimeSpan.FromSeconds(60), TestContext.CancellationToken);

                Assert.IsFalse(succeeded);
                Assert.AreEqual(redirectOutput, reported.Any(line => line.Contains("final output")));
                Assert.AreEqual(redirectError, reported.Any(line => line.Contains("final error")));
            }
            finally
            {
                Reporter.SetError(originalError);
            }
        }
    }
}
