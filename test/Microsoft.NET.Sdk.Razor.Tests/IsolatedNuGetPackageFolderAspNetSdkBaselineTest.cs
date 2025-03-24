// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.NET.Sdk.Razor.Tests
{
    [Trait("AspNetCore", "NugetIsolation")]
    [Trait("AspNetCore", "BaselineTest")]
    public class IsolatedNuGetPackageFolderAspNetSdkBaselineTest : AspNetSdkBaselineTest
    {
        private readonly string _cachePath;

        public IsolatedNuGetPackageFolderAspNetSdkBaselineTest(ITestOutputHelper log, string restoreNugetPackagePath) : base(log)
        {
            _cachePath = Path.GetFullPath(Path.Combine(TestContext.Current.TestExecutionDirectory, Shorten(restoreNugetPackagePath)));
        }

        private static string Shorten(string restoreNugetPackagePath) =>
            restoreNugetPackagePath
                .Replace("IntegrationTest", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Tests", string.Empty, StringComparison.OrdinalIgnoreCase);

        protected override string GetNuGetCachePath() => _cachePath;
    }
}

