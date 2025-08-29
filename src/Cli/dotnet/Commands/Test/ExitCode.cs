// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test;

// IMPORTANT: The exit codes must match MTP:
// https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Helpers/ExitCodes.cs
// They are also documented in https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-exit-codes
internal static class ExitCode
{
    // Values here should align with: https://aka.ms/testingplatform/exitcodes.
    public const int Success = 0;
    public const int GenericFailure = 1;
    public const int ZeroTests = 8;
    public const int MinimumExpectedTestsPolicyViolation = 9;
}
