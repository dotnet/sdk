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
    public class PackageValidationTests : SdkTest
    {
        public TestLogger _logger;

        public PackageValidationTests(ITestOutputHelper log) : base(log) 
        {
            _logger = new();
        }

        [Fact]
        public void MissingRidLessAssetForFramework()
        {
            string[] filePaths = new []
            {
                @"ref/netcoreapp3.1/TestPackage.dll", 
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _logger).Validate(package);
            Assert.Single(_logger.errors);
            Assert.Equal("PKV004 There is no compatible runtime asset for target framework .NETCoreApp,Version=v3.1 in the package.", _logger.errors[0]);
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
            new CompatibleTfmValidator(string.Empty, null, false, _logger).Validate(package);
            Assert.NotEmpty(_logger.errors);
            Assert.Contains("PKV004 There is no compatible runtime asset for target framework .NETStandard,Version=v2.0 in the package.", _logger.errors);
        }

        [Fact]
        public void CompatibleFrameworksInPackage()
        {
            TestProject testProject = new()
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

            testProject.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            var result = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name)).Execute();
            Assert.Equal(string.Empty, result.StdErr);
            string testPackagePath = Path.Combine(asset.TestRoot, testProject.Name, "bin", "debug", testProject.Name + ".1.0.0.nupkg");
            Package package = NupkgParser.CreatePackage(testPackagePath, null);
            new CompatibleFrameworkInPackageValidator(string.Empty, null, _logger).Validate(package);
            Assert.NotEmpty(_logger.errors);
            Assert.Contains("CP0002 : Member 'PackageValidationTests.First.test(string)' exists on the left but not on the right", _logger.errors);
        }
    }
}
