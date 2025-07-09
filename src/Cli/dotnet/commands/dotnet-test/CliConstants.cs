// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal static class CliConstants
    {
        public const string HelpOptionKey = "--help";
        public const string MSBuildOptionKey = "--msbuild-params";
        public const string NoBuildOptionKey = "--no-build";
        public const string ServerOptionKey = "--server";
        public const string DotNetTestPipeOptionKey = "--dotnet-test-pipe";
        public const string DegreeOfParallelismOptionKey = "--degree-of-parallelism";
        public const string DOPOptionKey = "--dop";

        public const string ServerOptionValue = "dotnettestcli";

        public const string MSBuildExeName = "MSBuild.dll";
    }

    internal static class TestStates
    {
        internal const string Passed = "Passed";

        internal const string Skipped = "Skipped";

        internal const string Failed = "Failed";

        internal const string Error = "Error";

        internal const string Timeout = "Timeout";

        internal const string Cancelled = "Cancelled";
    }

    internal static class SessionEventTypes
    {
        internal const string TestSessionStart = "TestSessionStart";
        internal const string TestSessionEnd = "TestSessionEnd";
    }

    internal static class HandshakeInfoPropertyNames
    {
        internal const string PID = "PID";
        internal const string Architecture = "Architecture";
        internal const string Framework = "Framework";
        internal const string OS = "OS";
        internal const string ProtocolVersion = "ProtocolVersion";
        internal const string HostType = "HostType";
        internal const string ModulePath = "ModulePath";
    }

    internal static class ProtocolConstants
    {
        internal const string Version = "1.0.0";
    }
}
