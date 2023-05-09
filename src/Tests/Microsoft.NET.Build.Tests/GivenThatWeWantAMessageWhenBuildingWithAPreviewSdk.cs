// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantAMessageWhenBuildingWithAPreviewSdk : SdkTest
    {
        public GivenThatWeWantAMessageWhenBuildingWithAPreviewSdk(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_displays_a_preview_message_when_using_a_preview_Sdk()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:_NETCoreSdkIsPreview=true")
                .Should()
                .Pass()
                .And.HaveStdOutContaining(Strings.UsingPreviewSdk_Info);
        }

        [Fact]
        public void It_does_not_display_a_preview_message_when_using_a_release_Sdk()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:_NETCoreSdkIsPreview=")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining(Strings.UsingPreviewSdk_Info);
        }
    }
}
