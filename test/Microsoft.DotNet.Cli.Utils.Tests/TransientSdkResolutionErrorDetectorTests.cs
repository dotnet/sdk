// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class TransientSdkResolutionErrorDetectorTests
    {
        [TestMethod]
        public void TransientInBoxSdkResolutionFailureIsDetected()
        {
            string input =
                "/datadisks/disk1/work/99FC086F/p/d/sdk/11.0.100-ci/Sdks/Microsoft.NET.Sdk.Razor/Sdk/Sdk.props(20,3): " +
                "error : Could not resolve SDK \"Microsoft.NET.Sdk.StaticWebAssets\". Exactly one of the probing " +
                "messages below indicates why we could not resolve the SDK.\r\n" +
                "Sdk.props(20,3): error :   SDK resolver \"Microsoft.DotNet.MSBuildWorkloadSdkResolver\" returned null.\r\n" +
                "Sdk.props(20,3): error :   The NuGetSdkResolver did not resolve this SDK because there was no version " +
                "specified in the project or global.json.\r\n" +
                "Sdk.props(20,11): error MSB4236: The SDK 'Microsoft.NET.Sdk.StaticWebAssets' specified could not be " +
                "found. [/work/InstantiatePr.csproj]";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeTrue();
        }

        [TestMethod]
        public void NullInputIsNotTransient()
        {
            TransientSdkResolutionErrorDetector.IsTransientError(null).Should().BeFalse();
        }

        [TestMethod]
        public void SuccessfulBuildIsNotTransient()
        {
            string input =
                "  Determining projects to restore...\r\n" +
                "  Restored /work/InstantiatePr.csproj (in 131 ms).\r\n" +
                "  InstantiatePr -> /work/bin/Debug/net11.0/InstantiatePr.dll\r\n" +
                "Build succeeded.\r\n    0 Warning(s)\r\n    0 Error(s)";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeFalse();
        }

        [TestMethod]
        public void MissingVersionedSdkWithoutResolverNullIsNotTransient()
        {
            // A genuinely missing, version-specified SDK is a deterministic failure (no workload resolver
            // "returned null" deferral), so it must not be treated as transient.
            string input =
                "error MSB4236: The SDK 'Contoso.Custom.Sdk' specified could not be found. [/work/project.csproj]";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeFalse();
        }

        [TestMethod]
        public void OtherResolverReturningNullIsNotTransient()
        {
            // A "returned null" message from a different resolver must not trigger a retry: only the in-box
            // workload resolver deferral (Microsoft.DotNet.MSBuildWorkloadSdkResolver) is the transient flake.
            string input =
                "Sdk.props(20,3): error :   SDK resolver \"Contoso.CustomSdkResolver\" returned null.\r\n" +
                "Sdk.props(20,11): error MSB4236: The SDK 'Microsoft.NET.Sdk.StaticWebAssets' specified could not be " +
                "found. [/work/InstantiatePr.csproj]";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeFalse();
        }

        [TestMethod]
        public void DefaultResolverDirectoryFlakeIsDetected()
        {
            // MSB4276 is raised when the default SDK resolver fails to find the Sdks directory under
            // heavy parallel I/O – the directory is present on disk but the probe races with other processes.
            string input =
                "project.csproj : error : Could not resolve SDK \"Microsoft.NET.Sdk\". Exactly one of the probing " +
                "messages below indicates why we could not resolve the SDK.\r\n" +
                "project.csproj : error :   MSB4276: The default SDK resolver failed to resolve SDK " +
                "\"Microsoft.NET.Sdk\" because directory " +
                "\"C:\\Program Files\\dotnet\\sdk\\11.0.100\\Sdks\\Microsoft.NET.Sdk\\Sdk\" did not exist.\r\n" +
                "project.csproj : error MSB4236: The SDK 'Microsoft.NET.Sdk' specified could not be found. [/work/project.csproj]";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeTrue();
        }

        [TestMethod]
        public void CustomSdkDirectoryNotExistIsNotTransient()
        {
            // MSB4276 for a non-Microsoft.NET.Sdk should not be treated as transient.
            string input =
                "project.csproj : error :   MSB4276: The default SDK resolver failed to resolve SDK " +
                "\"Contoso.Custom.Sdk\" because directory " +
                "\"/usr/share/dotnet/sdk/11.0.100/Sdks/Contoso.Custom.Sdk/Sdk\" did not exist.";
            TransientSdkResolutionErrorDetector.IsTransientError(input).Should().BeFalse();
        }
    }
}
