// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

// IMPORTANT: The exit codes must match MTP:
// https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Helpers/ExitCodes.cs
// They are also documented in https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-exit-codes
internal static class ExitCode
{
    // Values here should align with: https://aka.ms/testingplatform/exitcodes.
    public const int Success = 0;
    public const int GenericFailure = 1;
    public const int AtLeastOneTestFailed = 2;
    public const int TestSessionAborted = 3;
    public const int InvalidPlatformSetup = 4;
    public const int InvalidCommandLine = 5;
    public const int TestHostProcessExitedNonGracefully = 7;
    public const int ZeroTests = 8;
    public const int MinimumExpectedTestsPolicyViolation = 9;
    public const int TestAdapterTestSessionFailure = 10;
    public const int DependentProcessExited = 11;
    public const int IncompatibleProtocolVersion = 12;
    public const int TestExecutionStoppedForMaxFailedTests = 13;
    public const int CoverageThresholdFailed = 14;
    public const int TestExecutionStoppedAtDeadline = 15;
}
