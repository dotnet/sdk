// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompatibleTfmValidatorTests : SdkTest
    {
        public CompatibleTfmValidatorTests(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void MissingRidLessAssetForFramework()
        {
            string[] filePaths = new []
            {
                @"ref/netcoreapp3.1/TestPackage.dll", 
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            var errors = new CompatibleTfmValidator(string.Empty, null, false).Validate(package);
            foreach (var packageValidationError in errors.Item1.Differences)
            {
                Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset, packageValidationError.DiagnosticId);
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
            var errors = new CompatibleTfmValidator(string.Empty, null, false).Validate(package);
            foreach (var packageValidationError in errors.Item1.Differences)
            {
                Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset, packageValidationError.DiagnosticId);
            }
        }

        [Fact]
        public void CompatibleFrameworksInPackage()
        {
            TestProject tp = new()
            {
                Name = "TestPackage",
                TargetFrameworks = "netstandard2.0;net5.0",
            };

            string sourceCode = @"
namespace PackageValidationTests
{
    public class First
    {
        public void test() { }
#if NETSTANDARD2_0
        public void test(string test) { }
#endif
    }
}";

            tp.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(tp, tp.Name);
            var result = new PackCommand(Log, Path.Combine(asset.TestRoot, tp.Name)).Execute();
            Assert.Equal(string.Empty, result.StdErr);
            string testPackagePath = Path.Combine(asset.TestRoot, tp.Name, "bin", "debug", tp.Name + ".1.0.0.nupkg");
            Package package = NupkgParser.CreatePackage(testPackagePath, null);
            var errors = new CompatibleFrameworkInPackageValidator(string.Empty, null).Validate(package);
            foreach (var error in errors)
            {
                foreach (var difference in error.Differences.Differences)
                {
                    Assert.Equal(ApiCompatibility.DiagnosticIds.MemberMustExist, difference.DiagnosticId);
                }
            }
        }
    }
}
