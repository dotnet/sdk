﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompatibleFrameworksInPackageTests : SdkTest
    {
        private TestLogger _log;

        public CompatibleFrameworksInPackageTests(ITestOutputHelper log) : base(log)
        {
            _log = new TestLogger();
        }

        [Fact]
        public void CompatibleFrameworksInPackage()
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
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
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = NupkgParser.CreatePackage(packCommand.GetNuGetPackage(), null);
            new CompatibleFrameworkInPackageValidator(string.Empty, null, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            // TODO: add asserts for assembly and header metadata.
            Assert.Contains("CP0002 Member 'PackageValidationTests.First.test(string)' exists on the left but not on the right", _log.errors);
        }
        
        [Fact]
        public void MultipleCompatibleFrameworksInPackage()
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = "netstandard2.0;netcoreapp3.1;net5.0",
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
#if NETCOREAPP3_1
        public void test(bool test) { }
#endif
    }
}";

            testProject.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = NupkgParser.CreatePackage(packCommand.GetNuGetPackage(), null);
            new CompatibleFrameworkInPackageValidator(string.Empty, null, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            // TODO: add asserts for assembly and header metadata.
            Assert.Contains("CP0002 Member 'PackageValidationTests.First.test(string)' exists on the left but not on the right", _log.errors);
            Assert.Contains("CP0002 Member 'PackageValidationTests.First.test(bool)' exists on the left but not on the right", _log.errors);
        }
    }
}
