// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class GivenDotnetTestOutput : SdkTest
{
    private static readonly string[] OutputEnvironmentVariables =
    [
        "TF_BUILD", "GITHUB_ACTIONS", "APPVEYOR", "CI", "TRAVIS", "CIRCLECI",
        "CODEBUILD_BUILD_ID", "AWS_REGION", "BUILD_ID", "BUILD_URL", "PROJECT_ID",
        "TEAMCITY_VERSION", "JB_SPACE_API_URL",
        "CLAUDE_CODE_IS_COWORK", "CLAUDECODE", "CLAUDE_CODE", "CLAUDE_CODE_ENTRYPOINT",
        "CURSOR_EDITOR", "CURSOR_AI", "CURSOR_TRACE_ID", "CURSOR_AGENT", "GEMINI_CLI",
        "GITHUB_COPILOT_CLI_MODE", "GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI",
        "COPILOT_MODEL", "COPILOT_ALLOW_ALL", "COPILOT_GITHUB_TOKEN", "AI_AGENT", "COPILOT_AGENT",
        "CODEX_CLI", "CODEX_SANDBOX", "CODEX_CI", "CODEX_THREAD_ID", "OR_APP_NAME", "AMP_HOME",
        "QWEN_CODE", "DROID_CLI", "OPENCODE", "ZED_ENVIRONMENT", "ZED_TERM", "KIMI_CLI",
        "GOOSE_TERMINAL", "GOOSE_PROVIDER", "CLINE_TASK_ID", "ROO_CODE_TASK_ID",
        "WINDSURF_SESSION", "REPL_ID", "AUGMENT_AGENT", "ANTIGRAVITY_AGENT", "AGENT_CLI",
    ];

    [TestMethod]
    [DataRow("TF_BUILD", "TRUE", null, false, false, true)]
    [DataRow("GITHUB_ACTIONS", "true", null, false, false, true)]
    [DataRow("APPVEYOR", "true", null, false, false, true)]
    [DataRow("CI", "true", null, false, false, true)]
    [DataRow("TRAVIS", "true", null, false, false, true)]
    [DataRow("CIRCLECI", "true", null, false, false, true)]
    [DataRow("CODEBUILD_BUILD_ID", "build", "AWS_REGION", false, false, true)]
    [DataRow("BUILD_ID", "build", "BUILD_URL", false, false, true)]
    [DataRow("BUILD_ID", "build", "PROJECT_ID", false, false, true)]
    [DataRow("TEAMCITY_VERSION", "version", null, false, false, true)]
    [DataRow("JB_SPACE_API_URL", "url", null, false, false, true)]
    [DataRow(null, "", null, false, false, false)]
    [DataRow("CI", "false", null, false, false, false)]
    [DataRow("CODEBUILD_BUILD_ID", "build", null, false, false, false)]
    [DataRow("BUILD_ID", "build", null, false, false, false)]
    [DataRow("CI", "true", null, true, false, false)]
    [DataRow("CI", "true", null, false, true, false)]
    public void RunWithCIEnvironment_ShouldSelectAnsiOutput(
        string? environmentVariable,
        string value,
        string? additionalEnvironmentVariable,
        bool noAnsi,
        bool isLLMEnvironment,
        bool expectAnsi)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();
        var command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path);

        // Isolate the child process from the CI or agent running this test.
        foreach (string variable in OutputEnvironmentVariables)
        {
            command.WithEnvironmentVariable(variable, "");
        }

        if (environmentVariable is not null)
        {
            command.WithEnvironmentVariable(environmentVariable, value);
        }

        if (additionalEnvironmentVariable is not null)
        {
            command.WithEnvironmentVariable(additionalEnvironmentVariable, "value");
        }

        if (isLLMEnvironment)
        {
            command.WithEnvironmentVariable("AI_AGENT", "github_copilot_app_agent");
        }

        List<string> arguments = ["--no-progress", "--tl:off"];
        if (noAnsi)
        {
            arguments.Add("--no-ansi");
        }

        CommandResult result = command.Execute([.. arguments]);
        result.Should().Pass();
        string output = Regex.Replace(result.StdOut!, "\u001b\\[[0-9;]*m", "");
        output.Should().Contain("TestProject.dll");
        output.Should().NotContain("\u001b");

        if (expectAnsi)
        {
            result.StdOut.Should().Contain("\u001b[");
        }
        else
        {
            result.StdOut.Should().NotContain("\u001b");
        }

        if (!SdkTestContext.IsLocalized())
        {
            output.Should().Contain("total: 2")
                .And.Contain("succeeded: 1")
                .And.Contain("failed: 0")
                .And.Contain("skipped: 1");
        }
    }
}
