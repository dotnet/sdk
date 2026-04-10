// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Tests;

public class WorkloadUtilitiesTests : SdkTest
{
    public WorkloadUtilitiesTests(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void ShouldVerifySignatures_ParameterlessOverload_EquivalentToSkipFalse()
    {
        // The parameterless overload should return the same result as passing skipSignCheck: false.
        bool resultParameterless = WorkloadUtilities.ShouldVerifySignatures();
        bool resultExplicit = WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: false);

        resultParameterless.Should().Be(resultExplicit);
    }

    [WindowsOnlyFact]
    public void ShouldVerifySignatures_OnWindows_WithSkipTrue_ReturnsFalseOrThrows()
    {
        // On Windows, when skipSignCheck is true and no policy forces verification,
        // the method should return false (skip verification).
        // If the VerifySignatures registry policy is set, it should throw GracefulException.
        try
        {
            bool result = WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: true);
            // If dotnet is signed, result should be false (we're skipping).
            // If dotnet is not signed, result is always false regardless.
            result.Should().BeFalse();
        }
        catch (GracefulException)
        {
            // If the registry policy is set, trying to skip verification throws.
            // That's also valid behavior — the test passes either way.
        }
    }

    [Fact]
    public void ShouldVerifySignatures_WithSkipFalse_ReturnsBoolBasedOnPlatformAndSigningState()
    {
        // ShouldVerifySignatures(false) should return true only on Windows when dotnet is signed.
        // On non-Windows, it always returns false.
        bool result = WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: false);

        if (!OperatingSystem.IsWindows())
        {
            result.Should().BeFalse("Non-Windows platforms always return false");
        }
        // On Windows, the result depends on whether dotnet is signed (can be either true or false)
    }
}
