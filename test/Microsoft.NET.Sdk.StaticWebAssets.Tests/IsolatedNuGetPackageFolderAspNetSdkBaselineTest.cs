// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;


namespace Microsoft.NET.Sdk.StaticWebAssets.Tests

{

    [TestCategory("NugetIsolation")]
    [TestCategory("BaselineTest")]
    [TestProperty("AspNetCore", "NugetIsolation")]

    [TestProperty("AspNetCore", "BaselineTest")]

    public abstract class IsolatedNuGetPackageFolderAspNetSdkBaselineTest : AspNetSdkBaselineTest

    {

        protected abstract string RestoreNugetPackagePath { get; }



        private string? _cachePath;



        protected override string GetNuGetCachePath() =>

            _cachePath ??= Path.GetFullPath(Path.Combine(SdkTestContext.Current.TestExecutionDirectory, Shorten(RestoreNugetPackagePath)));



        private static string Shorten(string restoreNugetPackagePath) =>

            restoreNugetPackagePath

                .Replace("IntegrationTest", string.Empty, StringComparison.OrdinalIgnoreCase)

                .Replace("Tests", string.Empty, StringComparison.OrdinalIgnoreCase);

    }

}
