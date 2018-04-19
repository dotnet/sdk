// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using NuGet.Common;
using NuGet.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Clean.Tests
{
    public class GivenThatWeWantToCleanAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToCleanAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_cleans_without_logging_assets_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "CleanHelloWorld")
                .WithSource()
                .Restore(Log);

            var lockFilePath = Path.Combine(testAsset.TestRoot, "obj", "project.assets.json");
            LockFile lockFile = LockFileUtilities.GetLockFile(lockFilePath, NullLogger.Instance);

            lockFile.LogMessages.Add(
                new AssetsLogMessage(
                    LogLevel.Warning,
                    NuGetLogCode.NU1500,
                    "a test warning",
                    null));

            new LockFileFormat().Write(lockFilePath, lockFile);

            var cleanCommand = new CleanCommand(Log, testAsset.TestRoot);

            cleanCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning");
        }
    }
}
