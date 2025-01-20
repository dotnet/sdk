// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal static class CliConstants
    {
        public const string DotnetRunCommand = "dotnet run";
        public const string HelpOptionKey = "--help";
        public const string ServerOptionKey = "--server";
        public const string DotNetTestPipeOptionKey = "--dotnet-test-pipe";
        public const string FrameworkOptionKey = "--framework";

        public const string ServerOptionValue = "dotnettestcli";

        public const string ParametersSeparator = "--";
        public const string SemiColon = ";";
        public const string Colon = ":";

        public const string VSTest = "VSTest";
        public const string MicrosoftTestingPlatform = "MicrosoftTestingPlatform";

        public const string TestSectionKey = "test";

        public const string RestoreCommand = "Restore";
        public const string BuildCommand = "Build";
        public const string Configuration = "Configuration";

        public static readonly string[] ProjectExtensions = { ".proj", ".csproj", ".vbproj", ".fsproj" };
        public static readonly string[] SolutionExtensions = { ".sln", ".slnx" };

        public const string ProjectExtensionPattern = "*.*proj";
        public const string SolutionExtensionPattern = "*.sln";
        public const string SolutionXExtensionPattern = "*.slnx";

        public const string BinLogFileName = "msbuild.binlog";

        public const string TestingPlatformVsTestBridgeRunSettingsFileEnvVar = "TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE";
        public const string DLLExtension = "dll";
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
    }

    internal static class ProtocolConstants
    {
        internal const string Version = "1.0.0";
    }

    internal static class ProjectProperties
    {
        internal const string IsTestingPlatformApplication = "IsTestingPlatformApplication";
        internal const string IsTestProject = "IsTestProject";
        internal const string TargetFramework = "TargetFramework";
        internal const string TargetFrameworks = "TargetFrameworks";
        internal const string TargetPath = "TargetPath";
        internal const string ProjectFullPath = "MSBuildProjectFullPath";
        internal const string RunSettingsFilePath = "RunSettingsFilePath";
    }
}
