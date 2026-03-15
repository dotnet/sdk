// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    /// <summary>
    /// NuGet.Packaging.dll locates trusted root certificate bundles using AppContext.BaseDirectory
    /// (see FallbackCertificateBundleX509ChainFactory in NuGet.Packaging). This test ensures that
    /// NuGet.Packaging.dll and the trustedroots bundle directory remain co-located in the SDK layout.
    /// Do not move either without coordinating with the NuGet team (https://github.com/NuGet/Home).
    /// </summary>
    public class GivenNuGetPackagingDllAndBundleLayout : SdkTest
    {
        private const string ContractViolationMessage =
            "NuGet.Packaging.dll resolves trusted root certificate bundles relative to AppContext.BaseDirectory " +
            "(see FallbackCertificateBundleX509ChainFactory in NuGet.Packaging). " +
            "Do not move NuGet.Packaging.dll or the trustedroots directory without coordinating with the NuGet team.";

        private readonly string _sdkFolder;

        public GivenNuGetPackagingDllAndBundleLayout(ITestOutputHelper log)
            : base(log)
        {
            _sdkFolder = SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest;
        }

        [Fact]
        public void NuGetPackagingDllIsCoLocatedWithTrustedRootsBundles()
        {
            string nugetPackagingDll = Path.Combine(_sdkFolder, "NuGet.Packaging.dll");
            string codeSigningBundle = Path.Combine(_sdkFolder, "trustedroots", "codesignctl.pem");
            string timestampingBundle = Path.Combine(_sdkFolder, "trustedroots", "timestampctl.pem");

            File.Exists(nugetPackagingDll)
                .Should()
                .BeTrue($"NuGet.Packaging.dll must exist in the SDK folder ('{_sdkFolder}'). {ContractViolationMessage}");

            File.Exists(codeSigningBundle)
                .Should()
                .BeTrue($"trustedroots/codesignctl.pem must exist in the SDK folder ('{_sdkFolder}'). {ContractViolationMessage}");

            File.Exists(timestampingBundle)
                .Should()
                .BeTrue($"trustedroots/timestampctl.pem must exist in the SDK folder ('{_sdkFolder}'). {ContractViolationMessage}");
        }
    }
}
