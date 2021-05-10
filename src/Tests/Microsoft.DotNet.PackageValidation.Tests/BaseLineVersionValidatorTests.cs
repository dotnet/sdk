﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class BaseLineVersionValidatorTests : SdkTest
    {
        private TestLogger _log;

        public BaseLineVersionValidatorTests(ITestOutputHelper log) : base(log)
        {
            _log = new();
        }

        [Fact]
        public void TfmDroppedInLatestVersion()
        {
            string[] previousFilePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"ref/netstandard2.0/TestPackage.dll"
            };

            Package previousPackage = new("TestPackage", "1.0.0", previousFilePaths, null, null);

            string[] currentFilePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "2.0.0", currentFilePaths, null, null);
            new BaselinePackageValidator(previousPackage, string.Empty, null, false, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV006 TargetFramework .NETStandard,Version=v2.0 is no longer supported in the latest version.", _log.errors);
        }
    }
}
