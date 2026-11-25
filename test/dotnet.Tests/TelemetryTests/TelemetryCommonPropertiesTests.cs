// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.NetworkInformation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Moq;

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
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DynamicData(nameof(CITelemetryTestCases))]
    public void CanDetectCIStatusForEnvVars(Dictionary<string, string> envVars, bool expected)
    {
        var originalValues = envVars.ToDictionary(
            pair => pair.Key,
            pair => Environment.GetEnvironmentVariable(pair.Key));

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
            foreach (var (key, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(LLMTelemetryTestCases))]
    public void CanDetectLLMStatusForEnvVars(Dictionary<string, string>? envVars, string? expected)
    {
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(provider => provider.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns((string name) => envVars?.GetValueOrDefault(name));

        new LLMEnvironmentDetectorForTelemetry(environmentProvider.Object)
            .GetLLMEnvironment()
            .Should()
            .Be(expected);
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

    [TestMethod]
    [DataRow("11111111-2222-4333-8444-555555555555", "11111111-2222-4333-8444-555555555555")]
    [DataRow("aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012", "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012")]
    [DataRow("11111111-2222-1333-8444-555555555555", "11111111-2222-1333-8444-555555555555")]
    [DataRow("11111111-2222-8333-B444-555555555555", "11111111-2222-8333-B444-555555555555")]
    [DataRow("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    [DataRow(null, "not_present")]
    [DataRow("", "not_present")]
    [DataRow("not_present", "edcc84b8e36c26b675c1b102b0e42f3507eee0afe5fbda7c203e9cbb758390e3")]
    [DataRow("build-investigation-42", "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2")]
    [DataRow("BUILD-INVESTIGATION-42", "8495a77b0853a7baf0eaccc4f2cb4b53bd36872da0f13744527a2f8c4142a30f")]
    [DataRow("11111111-2222-4333-8444-55555555555z", "da8570b51ec8f24bb7194a2999c1fd4f360aeb7f5c00dd67626a8219af4d8cf4")]
    [DataRow("11111111222243338444555555555555", "a57c45f998b7a947797b97e2a2f9f5193c3f3b0d25846743ffe0ce9d41b8d31a")]
    [DataRow("{11111111-2222-4333-8444-555555555555}", "317fed678c8ba3993d5679e3e6f9e8812c05a45b6f18f2436fccb6900ad1c8ed")]
    [DataRow(" 11111111-2222-4333-8444-555555555555", "509590cf9e16ef2e2c18fa3c850252cd38d7a5f933c9b3d9be955a375bc3b424")]
    [DataRow("11111111-2222-4333-8444-555555555555 ", "a55397b892e7d810f57cf4891c72eb2f4be672aa98ce3666cee9dceb683b2cb0")]
    [DataRow("11111111-2222-4333-8444-555555555555\n", "1ea86e0cda2cdfa70440c9d56e86fd20e61b6a51daed6a369d403d1e997da00d")]
    [DataRow("11111111-2222-0333-8444-555555555555", "8b777695ee7f9a53bb76f167e98a835c582ee2da5aeba14055efda55a786eb8d")]
    [DataRow("11111111-2222-9333-8444-555555555555", "b478764bfaad8e44fbec9869e9287cd58ccb738bb299477b8d77935ede317765")]
    [DataRow("11111111-2222-4333-7444-555555555555", "b16908ebd945a172e8201459e14ea359bf33417005810f1b28aef64b7d7e32b5")]
    [DataRow("11111111-2222-4333-c444-555555555555", "d9bb2db9d5fbd352725181efa0c6869e672ad1b9522ed249284b674b5e7c605d")]
    [DataRow("ffffffff-ffff-ffff-ffff-ffffffffffff", "13984ab878f915021670cd855c9399ebc339676b2581855e4efd60c685f67c8e")]
    [DataRow(" ", "36a9e7f1c95b82ffb99743e0c5c4ce95d83c9a430aac59f84ef3cbfab6145068")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "635361c48bb9eab14198e76ea8ab7f1a41685d6ad62aa9146d301d4f17eb0ae0")]
    [DataRow("r\u00e9union-\U0001F527", "4f0d2b876136e17123ad90cae429ad8a4672b8acd187746136640f98da7a515e")]
    public void TelemetryCommonPropertiesShouldNormalizeCopilotSessionId(string? sessionId, string expectedId)
    {
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.COPILOT_AGENT_SESSION_ID))
            .Returns(sessionId);
        var collector = new TelemetryCommonProperties(
            userLevelCacheWriter: new NothingCache(),
            environmentProvider: environmentProvider.Object);

        var properties = collector.GetTelemetryCommonProperties("sdk-correlation-test");

        properties.Should().ContainKey("CopilotAgentSessionId");
        properties["CopilotAgentSessionId"].Should().Be(expectedId);
        properties["SessionId"].Should().Be("sdk-correlation-test");
    }

    [TestMethod]
    public void TelemetryContextPropertiesShouldRefreshWithoutChangingEarlierSnapshots()
    {
        string? sessionId = "build-investigation-42";
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.COPILOT_AGENT_SESSION_ID))
            .Returns(() => sessionId);
        var collector = new TelemetryCommonProperties(environmentProvider: environmentProvider.Object);

        var first = collector.GetTelemetryContextProperties();
        sessionId = "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012";
        var second = collector.GetTelemetryContextProperties();
        sessionId = null;
        var absent = collector.GetTelemetryContextProperties();

        first["CopilotAgentSessionId"].Should().Be("a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2");
        second["CopilotAgentSessionId"].Should().Be("aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012");
        absent["CopilotAgentSessionId"].Should().Be("not_present");
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
        new object[] { new Dictionary<string, string> { { "OPENCODE", "1" } }, "opencode" },
        new object[] { new Dictionary<string, string> { { "OPENCODE", "0" } }, "opencode" },
        new object[] { new Dictionary<string, string> { { "OPENCODE", "" } }, null! },
        new object[] { new Dictionary<string, string> { { "OPENCODE_AI", "1" } }, null! },
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
