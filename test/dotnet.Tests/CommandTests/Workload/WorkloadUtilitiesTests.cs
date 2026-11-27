// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Tests;

[TestClass]
public class WorkloadUtilitiesTests : SdkTest
{
    public WorkloadUtilitiesTests()
    {
    }

    [TestMethod]
    public void ShouldVerifySignatures_ParameterlessOverload_EquivalentToSkipFalse()
    {
        // The parameterless overload should return the same result as passing skipSignCheck: false.
        bool resultParameterless = WorkloadUtilities.ShouldVerifySignatures();
        bool resultExplicit = WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: false);

        resultParameterless.Should().Be(resultExplicit);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
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

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ShouldVerifySignatures_OnNonWindows_UsesOfficialBuildDefault()
    {
        // Linux only requests workload verification in Microsoft official builds. macOS requests
        // verification so NuGetPackageDownloader can apply its opt-in default.
#if MICROSOFT_OFFICIAL_BUILD
        bool expectedOnLinux = true;
#else
        bool expectedOnLinux = false;
#endif
        bool expected = OperatingSystem.IsLinux() ? expectedOnLinux : true;

        WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: false).Should().Be(expected);
        WorkloadUtilities.ShouldVerifySignatures(skipSignCheck: true).Should().BeFalse();
    }
}
