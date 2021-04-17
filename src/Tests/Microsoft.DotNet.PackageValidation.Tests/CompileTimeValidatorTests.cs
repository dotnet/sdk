// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompileTimeValidatorTests : SdkTest
    {
        public CompileTimeValidatorTests(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void MissingRidLessAssetForFramework()
        {
            string[] filePaths = new []
            {
                @"ref/netcoreapp3.1/TestPackage.dll", 
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            var errors = new CompileTimeValidator(string.Empty, null, false).Validate(package);
            foreach (var item in errors.Differences)
            {
                Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset, item.DiagnosticId);
            }
        }

        [Fact]
        public void MissingAssetForFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            var errors = new CompileTimeValidator(string.Empty, null, false).Validate(package);
            foreach (var item in errors.Differences)
            {
                Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset, item.DiagnosticId);
            }
        }
    }
}
