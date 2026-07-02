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
