// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class CliConstants
{
    public const string ServerOptionKey = "--server";
    public const string HelpOptionKey = "--help";
    public const string DotNetTestPipeOptionKey = "--dotnet-test-pipe";

    public const string ServerOptionValue = "dotnettestcli";

    public const string SemiColon = ";";

    public static readonly string[] SolutionExtensions = [".sln", ".slnx", ".slnf"];

    public const string ProjectExtensionPattern = "*.*proj";
    public const string SolutionExtensionPattern = "*.sln";
    public const string SolutionXExtensionPattern = "*.slnx";
    public const string SolutionFilterExtensionPattern = "*.slnf";

    public const string BinLogFileName = "msbuild.binlog";

    public const string DLLExtension = ".dll";

    public const string TestTraceLoggingEnvVar = "DOTNET_CLI_TEST_TRACEFILE";
}

internal static class TestStates
{
    internal const byte Discovered = 0;
    internal const byte Passed = 1;
    internal const byte Skipped = 2;
    internal const byte Failed = 3;
    internal const byte Error = 4;
    internal const byte Timeout = 5;
    internal const byte Cancelled = 6;
}

internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

internal static class DisplayMessageLevels
{
    // These values flow over IPC as a single byte and must stay stable.
    internal const byte Information = 0;
    internal const byte Warning = 1;
    internal const byte Error = 2;
}

internal static class HandshakeMessagePropertyNames
{
    internal const byte PID = 0;
    internal const byte Architecture = 1;
    internal const byte Framework = 2;
    internal const byte OS = 3;
    internal const byte SupportedProtocolVersions = 4;
    internal const byte HostType = 5;
    internal const byte ModulePath = 6;
    internal const byte ExecutionId = 7;
    internal const byte InstanceId = 8;
    internal const byte IsIDE = 9;

    // Reports which command-line execution mode the test host is running in,
    // so the SDK can detect mismatches such as a help/list-tests option leaking
    // from RunArguments or launchSettings.json into what the SDK thinks is a
    // normal run. Values come from HandshakeMessageExecutionModes.
    // Optional property — older Microsoft.Testing.Platform versions don't send
    // it, in which case the SDK falls back to its previous (no-validation) behavior.
    internal const byte ExecutionMode = 10;
}

internal static class HandshakeMessageExecutionModes
{
    // Standard test run.
    internal const string Run = "run";

    // The test host is going to print command-line help (e.g. --help, -?).
    internal const string Help = "help";

    // The test host is going to discover tests (e.g. --list-tests).
    internal const string Discover = "discover";
}

internal static class ProtocolConstants
{
    /// <summary>
    /// The protocol versions that are supported by the current SDK. Multiple versions can be present and be semicolon separated.
    /// </summary>
    // 1.2.0 adds AzureDevOpsLogMessage (serializer id 11) forwarding; 1.3.0 adds DisplayMessage (serializer id 12) forwarding.
    // NOTE: 1.4.0 (the reverse server-control pipe / server-initiated cancellation) is intentionally NOT advertised yet:
    // it is a separate, larger feature that is out of scope here.
    internal const string SupportedVersions = "1.0.0;1.1.0;1.2.0;1.3.0";
}

internal static class ProjectProperties
{
    internal const string IsTestingPlatformApplication = "IsTestingPlatformApplication";
    internal const string IsTestProject = "IsTestProject";
    internal const string TargetFramework = "TargetFramework";
    internal const string TargetFrameworks = "TargetFrameworks";
    internal const string Configuration = "Configuration";
    internal const string Platform = "Platform";
    internal const string TargetPath = "TargetPath";
    internal const string ProjectFullPath = "MSBuildProjectFullPath";
    internal const string RunCommand = "RunCommand";
    internal const string RunArguments = "RunArguments";
    internal const string RunWorkingDirectory = "RunWorkingDirectory";
    internal const string AppDesignerFolder = "AppDesignerFolder";
    internal const string TestTfmsInParallel = "TestTfmsInParallel";
    internal const string BuildInParallel = "BuildInParallel";
}
