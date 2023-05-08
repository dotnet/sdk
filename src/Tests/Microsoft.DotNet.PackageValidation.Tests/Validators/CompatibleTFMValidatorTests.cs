﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.PackageValidation.Tests;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;

namespace Microsoft.DotNet.PackageValidation.Validators.Tests
{
    public class CompatibleTFMValidatorTests
    {
        private (SuppressableTestLog, CompatibleTfmValidator) CreateLoggerAndValidator()
        {
            SuppressableTestLog log = new();
            CompatibleTfmValidator validator = new(log,
                Mock.Of<IApiCompatRunner>());

            return (log, validator);
        }

        [Fact]
        public void MissingRidLessAssetForFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.Single(log.errors);
            Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETCoreApp,Version=v3.1"), log.errors[0]);
        }

        [Fact]
        public void MissingAssetForFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETStandard,Version=v2.0"), log.errors);
        }

        [Fact]
        public void MissingRidSpecificAssetForFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"ref/netcoreapp2.0/TestPackage.dll",
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETCoreApp,Version=v2.0"), log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidSpecificAsset + " " + string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset, ".NETCoreApp,Version=v2.0", "win"), log.errors);
        }

        [Fact]
        public void OnlyRuntimeAssembly()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"runtimes/win/lib/netstandard2.0/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " + string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), log.errors);
        }

        [Fact]
        public void LibAndRuntimeAssembly()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll",
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.Empty(log.errors);
        }

        [Fact]
        public void NoCompileTimeAssetForSpecificFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                $@"ref/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " + string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), log.errors);
        }

        [Fact]
        public void NoRuntimeAssetForSpecificFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                $@"ref/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ToolsetInfo.CurrentTargetFramework), log.errors);
        }

        [Fact]
        public void NoRuntimeSpecificAssetForSpecificFramework()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"lib/netstandard2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/unix/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.Empty(log.errors);
        }

        [Fact]
        public void CompatibleLibAsset()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"ref/netcoreapp2.0/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " + string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), log.errors);
        }

        [Fact]
        public void CompatibleRidSpecificAsset()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"lib/netcoreapp2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.Empty(log.errors);
        }

        [Fact]
        public void CompatibleFrameworksWithDifferentAssets()
        {
            (SuppressableTestLog log, CompatibleTfmValidator validator) = CreateLoggerAndValidator();
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null);

            validator.Validate(new PackageValidatorOption(package, enqueueApiCompatWorkItems: false));

            Assert.Empty(log.errors);
        }
    }
}
