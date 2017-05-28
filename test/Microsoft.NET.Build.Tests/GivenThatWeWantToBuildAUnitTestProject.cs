// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAUnitTestProject : SdkTest
    {
        public GivenThatWeWantToBuildAUnitTestProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_generates_runtime_config()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("XUnitTestProject")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp2.0");
            outputDirectory.Should().HaveFile(@"XUnitTestProject.runtimeconfig.json");
        }
    }
}