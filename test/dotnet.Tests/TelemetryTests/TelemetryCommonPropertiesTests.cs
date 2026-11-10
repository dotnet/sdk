// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.NetworkInformation;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class TelemetryCommonPropertiesTests : SdkTest
{
    public TelemetryCommonPropertiesTests()
    {
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldContainIfItIsInDockerOrNot()
    {
        var unitUnderTest = new TelemetryCommonProperties(userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId").Should().ContainKey("Docker Container");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnHashedPath()
    {
        var unitUnderTest = new TelemetryCommonProperties(() => "ADirectory", userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Current Path Hash"].Should().NotBe("ADirectory");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnHashedMachineId()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => "plaintext", userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID"].Should().NotBe("plaintext");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnDevDeviceId()
    {
        var unitUnderTest = new TelemetryCommonProperties(getDeviceId: () => "plaintext", userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["devdeviceid"].Should().Be("plaintext");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnNewGuidWhenCannotGetMacAddress()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID"];

        Guid.TryParse((string?)assignedMachineId, out var _).Should().BeTrue("it should be a guid");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnNewGuidWhenGettingMacAddressThrowsNetworkInformationException()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => throw new NetworkInformationException(), userLevelCacheWriter: new NothingCache());
        var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID"];
        var assignedMachineIdOld = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID Old"];

        Guid.TryParse((string?)assignedMachineId, out var _).Should().BeTrue("it should be a guid");
        Guid.TryParse((string?)assignedMachineIdOld, out var _).Should().BeTrue("it should be a guid");
        assignedMachineId.Should().NotBe(assignedMachineIdOld, "it should generate a new fallback guid each time");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldEnsureDevDeviceIDIsCached()
    {
        var unitUnderTest = new TelemetryCommonProperties(userLevelCacheWriter: new NothingCache());
        var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["devdeviceid"];

        Guid.TryParse((string?)assignedMachineId, out var _).Should().BeTrue("it should be a guid");
        var secondAssignedMachineId = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["devdeviceid"];

        Guid.TryParse((string?)secondAssignedMachineId, out var _).Should().BeTrue("it should be a guid");
        secondAssignedMachineId.Should().Be(assignedMachineId, "it should match the previously assigned guid");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnHashedMachineIdOld()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => "plaintext", userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID Old"].Should().NotBe("plaintext");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnNewGuidWhenCannotGetMacAddressOld()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        var assignedMachineId = unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Machine ID Old"];

        Guid.TryParse((string?)assignedMachineId, out var _).Should().BeTrue("it should be a guid");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnIsOutputRedirected()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Output Redirected"].Should().BeOneOf("True", "False");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnIsCIDetection()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Continuous Integration"].Should().BeOneOf("True", "False");
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldContainKernelVersion()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Kernel Version"].Should().Be(RuntimeInformation.OSDescription);
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldContainArchitectureInformation()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["OS Architecture"].Should().Be(RuntimeInformation.OSArchitecture.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TelemetryCommonPropertiesShouldContainWindowsInstallType()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Installation Type"].Should().NotBeEmpty();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TelemetryCommonPropertiesShouldContainEmptyWindowsInstallType()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Installation Type"].Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TelemetryCommonPropertiesShouldContainWindowsProductType()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Product Type"].Should().NotBeEmpty();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TelemetryCommonPropertiesShouldContainEmptyWindowsProductType()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Product Type"].Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TelemetryCommonPropertiesShouldContainEmptyLibcReleaseAndVersion()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Release"].Should().BeEmpty();
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Version"].Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void TelemetryCommonPropertiesShouldContainEmptyLibcReleaseAndVersion2()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Release"].Should().BeEmpty();
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Version"].Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void TelemetryCommonPropertiesShouldContainLibcReleaseAndVersion()
    {
        if (!RuntimeInformation.RuntimeIdentifier.Contains("alpine", StringComparison.OrdinalIgnoreCase))
        {
            var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
            unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Release"].Should().NotBeEmpty();
            unitUnderTest.GetTelemetryCommonProperties("dummySessionId")["Libc Version"].Should().NotBeEmpty();
        }
    }

    [TestMethod]
    public void TelemetryCommonPropertiesShouldReturnIsLLMDetection()
    {
        var unitUnderTest = new TelemetryCommonProperties(getMACAddress: () => null, userLevelCacheWriter: new NothingCache());
        // The "llm" property reflects whichever LLM-agent env vars are set in the
        // current process. When tests run locally (no agent) the value is null; inside
        // an LLM environment (e.g. Claude Code, Copilot CLI, Copilot App) it will
        // contain the matching comma-separated labels. Accept any non-failing value.
        unitUnderTest.GetTelemetryCommonProperties("dummySessionId").Should().ContainKey("llm");
    }

    [TestMethod]
    [DynamicData(nameof(CITelemetryTestCases))]
    public void CanDetectCIStatusForEnvVars(Dictionary<string, string> envVars, bool expected)
    {
        try
        {
            foreach (var (key, value) in envVars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
            new CIEnvironmentDetectorForTelemetry().IsCIEnvironment().Should().Be(expected);
        }
        finally
        {
            foreach (var (key, value) in envVars)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }

    // All environment variables checked by LLMEnvironmentDetectorForTelemetry.
    // Tests must clear these before each run so that ambient env vars (e.g. when
    // tests execute inside the Copilot CLI desktop app) don't pollute results.
    private static readonly string[] _allLLMEnvVars = [
        "CLAUDE_CODE_IS_COWORK", "CLAUDECODE", "CLAUDE_CODE", "CLAUDE_CODE_ENTRYPOINT",
        "CURSOR_EDITOR", "CURSOR_AI", "CURSOR_TRACE_ID", "CURSOR_AGENT",
        "GEMINI_CLI",
        "GITHUB_COPILOT_CLI_MODE", "GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI", "COPILOT_MODEL", "COPILOT_ALLOW_ALL", "COPILOT_GITHUB_TOKEN",
        "AI_AGENT", "COPILOT_AGENT",
        "CODEX_CLI", "CODEX_SANDBOX", "CODEX_CI", "CODEX_THREAD_ID",
        "OR_APP_NAME",
        "AMP_HOME",
        "QWEN_CODE",
        "DROID_CLI",
        "OPENCODE_AI",
        "ZED_ENVIRONMENT", "ZED_TERM",
        "KIMI_CLI",
        "GOOSE_TERMINAL", "GOOSE_PROVIDER",
        "CLINE_TASK_ID",
        "ROO_CODE_TASK_ID",
        "WINDSURF_SESSION",
        "REPL_ID",
        "AUGMENT_AGENT",
        "ANTIGRAVITY_AGENT",
        "AGENT_CLI",
    ];

    [TestMethod]
    [DynamicData(nameof(LLMTelemetryTestCases))]
    public void CanDetectLLMStatusForEnvVars(Dictionary<string, string>? envVars, string? expected)
    {
        // Save and clear all LLM env vars so ambient values don't affect the test.
        var savedEnvVars = _allLLMEnvVars
            .Select(key => (key, value: Environment.GetEnvironmentVariable(key)))
            .Where(pair => pair.value is not null)
            .ToArray();

        foreach (var key in _allLLMEnvVars)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        try
        {
            if (envVars is not null)
            {
                foreach (var (key, value) in envVars)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            new LLMEnvironmentDetectorForTelemetry().GetLLMEnvironment().Should().Be(expected);
        }
        finally
        {
            // Clean up test-set vars
            if (envVars is not null)
            {
                foreach (var (key, value) in envVars)
                {
                    Environment.SetEnvironmentVariable(key, null);
                }
            }
            // Restore original ambient vars
            foreach (var (key, value) in savedEnvVars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    [TestMethod]
    [DataRow("dummySessionId")]
    [DataRow(null)]
    public void TelemetryCommonPropertiesShouldContainSessionId(string? sessionId)
    {
        var unitUnderTest = new TelemetryCommonProperties(userLevelCacheWriter: new NothingCache());
        var commonProperties = unitUnderTest.GetTelemetryCommonProperties(sessionId);

        commonProperties.Should().ContainKey("SessionId");
        commonProperties["SessionId"].Should().Be(sessionId);
    }

    public static IEnumerable<object[]> LLMTelemetryTestCases => new List<object[]>()
    {
        new object[] { new Dictionary<string, string> { {"CLAUDECODE", "1" } }, "claude" },
        new object[] { new Dictionary<string, string> { {"CLAUDE_CODE", "1" } }, "claude" },
        new object[] { new Dictionary<string, string> { {"CLAUDE_CODE_ENTRYPOINT", "some_value" } }, "claude" },
        new object[] { new Dictionary<string, string> { {"CLAUDE_CODE_IS_COWORK", "1" } }, "cowork" },
        new object[] { new Dictionary<string, string> { { "CURSOR_EDITOR", "1" } }, "cursor" },
        new object[] { new Dictionary<string, string> { { "CURSOR_AI", "1" } }, "cursor" },
        new object[] { new Dictionary<string, string> { { "CURSOR_TRACE_ID", "abc" } }, "cursor" },
        new object[] { new Dictionary<string, string> { { "CURSOR_AGENT", "1" } }, "cursor" },
        new object[] { new Dictionary<string, string> { { "GEMINI_CLI", "true" } }, "gemini" },
        // Existence-based: any non-empty value now matches, even "0"
        new object[] { new Dictionary<string, string> { { "GEMINI_CLI", "0" } }, "gemini" },
        new object[] { new Dictionary<string, string> { { "GITHUB_COPILOT_CLI_MODE", "true" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "GH_COPILOT_WORKING_DIRECTORY", "/repo" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "COPILOT_CLI", "1" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "COPILOT_MODEL", "gpt" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "COPILOT_ALLOW_ALL", "1" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "COPILOT_GITHUB_TOKEN", "token" } }, "copilot-cli" },
        // GitHub Copilot app (desktop application)
        new object[] { new Dictionary<string, string> { { "AI_AGENT", "github_copilot_app_agent" } }, "copilot-app" },
        // GitHub Copilot agent mode in VS Code
        new object[] { new Dictionary<string, string> { { "COPILOT_AGENT", "1" } }, "copilot-vscode" },
        new object[] { new Dictionary<string, string> { { "AI_AGENT", "github_copilot_vscode_agent" } }, "copilot-vscode" },
        new object[] { new Dictionary<string, string> { { "CODEX_CLI", "1" } }, "codex" },
        new object[] { new Dictionary<string, string> { { "CODEX_SANDBOX", "1" } }, "codex" },
        new object[] { new Dictionary<string, string> { { "CODEX_CI", "1" } }, "codex" },
        new object[] { new Dictionary<string, string> { { "CODEX_THREAD_ID", "thread1" } }, "codex" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "Aider" } }, "aider" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "aider" } }, "aider" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "plandex" } }, "plandex" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "Plandex" } }, "plandex" },
        new object[] { new Dictionary<string, string> { { "AMP_HOME", "/path/to/amp" } }, "amp" },
        new object[] { new Dictionary<string, string> { { "QWEN_CODE", "1" } }, "qwen" },
        new object[] { new Dictionary<string, string> { { "DROID_CLI", "true" } }, "droid" },
        new object[] { new Dictionary<string, string> { { "OPENCODE_AI", "1" } }, "opencode" },
        new object[] { new Dictionary<string, string> { { "ZED_ENVIRONMENT", "1" } }, "zed" },
        new object[] { new Dictionary<string, string> { { "ZED_TERM", "1" } }, "zed" },
        new object[] { new Dictionary<string, string> { { "KIMI_CLI", "true" } }, "kimi" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "OpenHands" } }, "openhands" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "openhands" } }, "openhands" },
        new object[] { new Dictionary<string, string> { { "GOOSE_TERMINAL", "1" } }, "goose" },
        new object[] { new Dictionary<string, string> { { "GOOSE_PROVIDER", "openai" } }, "goose" },
        new object[] { new Dictionary<string, string> { { "CLINE_TASK_ID", "task123" } }, "cline" },
        new object[] { new Dictionary<string, string> { { "ROO_CODE_TASK_ID", "task456" } }, "roo" },
        new object[] { new Dictionary<string, string> { { "WINDSURF_SESSION", "session789" } }, "windsurf" },
        new object[] { new Dictionary<string, string> { { "REPL_ID", "repl1" } }, "replit" },
        new object[] { new Dictionary<string, string> { { "AUGMENT_AGENT", "1" } }, "augment" },
        new object[] { new Dictionary<string, string> { { "ANTIGRAVITY_AGENT", "1" } }, "antigravity" },
        new object[] { new Dictionary<string, string> { { "AGENT_CLI", "true" } }, "generic_agent" },
        // Test combinations of older tools
        new object[] { new Dictionary<string, string> { { "CLAUDECODE", "1" }, { "CURSOR_EDITOR", "1" } }, "claude, cursor" },
        new object[] { new Dictionary<string, string> { { "GEMINI_CLI", "true" }, { "GITHUB_COPILOT_CLI_MODE", "true" } }, "gemini, copilot-cli" },
        new object[] { new Dictionary<string, string> { { "CLAUDECODE", "1" }, { "GEMINI_CLI", "true" }, { "AGENT_CLI", "true" } }, "claude, gemini, generic_agent" },
        new object[] { new Dictionary<string, string> { { "CLAUDECODE", "1" }, { "CURSOR_EDITOR", "1" }, { "GEMINI_CLI", "true" }, { "GITHUB_COPILOT_CLI_MODE", "true" }, { "AGENT_CLI", "true" } }, "claude, cursor, gemini, copilot-cli, generic_agent" },
        // Test combinations of newer tools
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "Aider" }, { "CLINE_TASK_ID", "task123" } }, "aider, cline" },
        new object[] { new Dictionary<string, string> { { "CODEX_CLI", "1" }, { "WINDSURF_SESSION", "session789" } }, "codex, windsurf" },
        new object[] { new Dictionary<string, string> { { "GOOSE_TERMINAL", "1" }, { "ROO_CODE_TASK_ID", "task456" } }, "goose, roo" },
        // Copilot app sets both COPILOT_CLI and AI_AGENT=github_copilot_app_agent; both rules fire
        new object[] { new Dictionary<string, string> { { "COPILOT_CLI", "1" }, { "AI_AGENT", "github_copilot_app_agent" } }, "copilot-cli, copilot-app" },
        // Existence-based loosened vars now match regardless of value (e.g. "false" is still a non-empty value)
        new object[] { new Dictionary<string, string> { { "GEMINI_CLI", "false" } }, "gemini" },
        new object[] { new Dictionary<string, string> { { "GITHUB_COPILOT_CLI_MODE", "false" } }, "copilot-cli" },
        new object[] { new Dictionary<string, string> { { "AGENT_CLI", "false" } }, "generic_agent" },
        new object[] { new Dictionary<string, string> { { "DROID_CLI", "false" } }, "droid" },
        new object[] { new Dictionary<string, string> { { "KIMI_CLI", "false" } }, "kimi" },
        // Cowork is distinct from claude and reported independently
        new object[] { new Dictionary<string, string> { { "CLAUDE_CODE_IS_COWORK", "1" }, { "CLAUDE_CODE", "1" } }, "cowork, claude" },
        new object[] { new Dictionary<string, string> { { "OR_APP_NAME", "SomeOtherApp" } }, null! },
        new object[] { new Dictionary<string, string>(), null! },
    };

    public static IEnumerable<object[]> CITelemetryTestCases => new List<object[]>()
    {
        new object[] { new Dictionary<string, string> { { "TF_BUILD", "true" } }, true },
        new object[] { new Dictionary<string, string> { { "GITHUB_ACTIONS", "true" } }, true },
        new object[] { new Dictionary<string, string> { { "APPVEYOR", "true"} }, true },
        new object[] { new Dictionary<string, string> { { "CI", "true"} }, true },
        new object[] { new Dictionary<string, string> { { "TRAVIS", "true"} }, true },
        new object[] { new Dictionary<string, string> { { "CIRCLECI", "true"} }, true },
        new object[] { new Dictionary<string, string> { { "CODEBUILD_BUILD_ID", "hi" }, { "AWS_REGION", "hi" } }, true },
        new object[] { new Dictionary<string, string> { { "CODEBUILD_BUILD_ID", "hi" } }, false },
        new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" }, { "BUILD_URL", "hi" } }, true },
        new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" } }, false },
        new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" }, { "PROJECT_ID", "hi" } }, true },
        new object[] { new Dictionary<string, string> { { "BUILD_ID", "hi" } }, false },
        new object[] { new Dictionary<string, string> { { "TEAMCITY_VERSION", "hi" } }, true },
        new object[] { new Dictionary<string, string> { { "TEAMCITY_VERSION", "" } }, false },
        new object[] { new Dictionary<string, string> { { "JB_SPACE_API_URL", "hi" } }, true },
        new object[] { new Dictionary<string, string> { { "JB_SPACE_API_URL", "" } }, false },
        new object[] { new Dictionary<string, string> { { "SomethingElse", "hi" } }, false },
    };

    private class NothingCache : IUserLevelCacheWriter
    {
        public string RunWithCache(string cacheKey, Func<string> getValueToCache)
        {
            return getValueToCache();
        }

        public string RunWithCacheInFilePath(string cacheFilepath, Func<string> getValueToCache)
        {
            return getValueToCache();
        }
    }
}
