// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    internal static class TestingConstants
    {
        public const string Debug = "Debug";
        public const string Release = "Release";

        public const string Failed = "failed";
        public const string Passed = "passed";
    }

    internal static class ExitCode
    {
        public const int Success = 0;
        public const int GenericFailure = 1;
        public const int AtLeastOneTestFailed = 2;
        public const int ZeroTests = 8;
        public const int MinimumExpectedTestsPolicyViolation = 9;
    }
}
