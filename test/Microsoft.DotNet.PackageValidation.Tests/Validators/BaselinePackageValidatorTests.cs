// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.PackageValidation.Filtering;
using Microsoft.DotNet.PackageValidation.Tests;
using Moq;

namespace Microsoft.DotNet.PackageValidation.Validators.Tests
{
    public class BaselinePackageValidatorTests
    {
        private (SuppressibleTestLog, BaselinePackageValidator) CreateLoggerAndValidator()
        {
            SuppressibleTestLog log = new();
            BaselinePackageValidator validator = new(log,
                Mock.Of<IApiCompatRunner>());

            return (log, validator);
        }

        [Fact]
        public void TfmDroppedInLatestVersion()
        {
            Package baselinePackage = new(string.Empty, "TestPackage", "1.0.0",
            [
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll"
            ]);
            Package package = new(string.Empty, "TestPackage", "2.0.0", [ @"lib/netcoreapp3.1/TestPackage.dll" ]);

            (SuppressibleTestLog log, BaselinePackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package,
                enableStrictMode: false,
                enqueueApiCompatWorkItems: false,
                baselinePackage: baselinePackage));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.TargetFrameworkDropped + " " + string.Format(Resources.MissingTargetFramework, ".NETStandard,Version=v2.0"), log.errors);
        }

        [Fact]
        public void BaselineFrameworksExcluded()
        {
            Package baselinePackage = new(string.Empty, "TestPackage", "1.0.0",
            [
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll"
            ]);
            Package package = new(string.Empty, "TestPackage", "2.0.0", [ @"lib/netstandard2.0/TestPackage.dll" ]);

            (SuppressibleTestLog log, BaselinePackageValidator validator) = CreateLoggerAndValidator();
            TargetFrameworkFilter targetFrameworkRegexFilter = new("netcoreapp3.1");

            validator.Validate(new PackageValidatorOption(package,
                enableStrictMode: false,
                enqueueApiCompatWorkItems: false,
                baselinePackage: baselinePackage,
                targetFrameworkFilter: targetFrameworkRegexFilter));

            Assert.Contains(string.Format(Resources.BaselineTargetFrameworksIgnored, "netcoreapp3.1"), log.info);
            Assert.Empty(log.warnings);
            Assert.Empty(log.errors);
        }

        [Fact]
        public void BaselineFrameworkIgnoredButPresentInCurrentPackage()
        {
            Package baselinePackage = new(string.Empty, "TestPackage", "1.0.0",
            [
                @"lib/portable-net45+win8+wp8+wpa81/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll"
            ]);
            Package package = new(string.Empty, "TestPackage", "2.0.0",
            [
                @"lib/portable-net45+win8+wp8+wpa81/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
            ]);

            (SuppressibleTestLog log, BaselinePackageValidator validator) = CreateLoggerAndValidator();
            TargetFrameworkFilter targetFrameworkRegexFilter = new("portable-*");

            validator.Validate(new PackageValidatorOption(package,
                enableStrictMode: false,
                enqueueApiCompatWorkItems: false,
                baselinePackage: baselinePackage,
                targetFrameworkFilter: targetFrameworkRegexFilter));

            Assert.Contains(string.Format(Resources.BaselineTargetFrameworksIgnored, "portable-net45+win8+wp8+wpa81"), log.info);
            Assert.Contains(DiagnosticIds.BaselineTargetFrameworkIgnoredButPresentInCurrentPackage + " " +
                string.Format(Resources.BaselineTargetFrameworkIgnoredButPresentInCurrentPackage, "portable-net45+win8+wp8+wpa81"),
                log.warnings);
            Assert.Empty(log.errors);
        }
    }
}
