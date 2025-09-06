// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class CliConstants
{
    public const string ServerOptionKey = "--server";
    public const string HelpOptionKey = "--help";
    public const string DotNetTestPipeOptionKey = "--dotnet-test-pipe";

    public const string ServerOptionValue = "dotnettestcli";

    public const string SemiColon = ";";

    public const string VSTest = "VSTest";
    public const string MicrosoftTestingPlatform = "Microsoft.Testing.Platform";

    public static readonly string[] SolutionExtensions = [".sln", ".slnx", ".slnf"];

    public const string ProjectExtensionPattern = "*.*proj";
    public const string SolutionExtensionPattern = "*.sln";
    public const string SolutionXExtensionPattern = "*.slnx";
    public const string SolutionFilterExtensionPattern = "*.slnf";

    public const string BinLogFileName = "msbuild.binlog";

    public const string DLLExtension = ".dll";

    public const string MTPTarget = "_MTPBuild";

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
}

internal static class ProtocolConstants
{
    /// <summary>
    /// The protocol versions that are supported by the current SDK. Multiple versions can be present and be semicolon separated.
    /// </summary>
    internal const string SupportedVersions = "1.0.0";
}

internal static class ProjectProperties
{
    internal const string IsTestingPlatformApplication = "IsTestingPlatformApplication";
    internal const string IsTestProject = "IsTestProject";
    internal const string TargetFramework = "TargetFramework";
    internal const string TargetFrameworks = "TargetFrameworks";
    internal const string TargetPath = "TargetPath";
    internal const string ProjectFullPath = "MSBuildProjectFullPath";
    internal const string RunCommand = "RunCommand";
    internal const string RunArguments = "RunArguments";
    internal const string RunWorkingDirectory = "RunWorkingDirectory";
    internal const string AppDesignerFolder = "AppDesignerFolder";
    internal const string TestTfmsInParallel = "TestTfmsInParallel";
    internal const string BuildInParallel = "BuildInParallel";
}
