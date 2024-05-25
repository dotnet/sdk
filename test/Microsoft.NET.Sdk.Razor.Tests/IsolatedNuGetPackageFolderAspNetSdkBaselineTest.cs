// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class IsolatedNuGetPackageFolderAspNetSdkBaselineTest : AspNetSdkBaselineTest
    {
        public IsolatedNuGetPackageFolderAspNetSdkBaselineTest(ITestOutputHelper log, string restoreNugetPackagePath) : base(log)
        {
            TestContext.Current.NuGetCachePath = Path.GetFullPath(Path.Combine(_testAssetsManager.TestAssetsRoot, restoreNugetPackagePath));
        }
    }
}

