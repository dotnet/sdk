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
        internal const byte Passed = 0;
        internal const byte Skipped = 1;
        internal const byte Failed = 2;
        internal const byte Error = 3;
        internal const byte Timeout = 4;
        internal const byte Cancelled = 5;
    }

    internal static class SessionEventTypes
    {
        internal const byte TestSessionStart = 0;
        internal const byte TestSessionEnd = 1;
    }

    internal static class HandshakeInfoPropertyNames
    {
        internal const byte PID = 0;
        internal const byte Architecture = 1;
        internal const byte Framework = 2;
        internal const byte OS = 3;
        internal const byte ProtocolVersion = 4;
        internal const byte HostType = 5;
        internal const byte ModulePath = 6;
        internal const byte ExecutionId = 7;
    }

    internal static class ProtocolConstants
    {
        internal const string Version = "1.0.0";
    }
}
