// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

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
                .And.HaveStdOutContaining(Strings.UsingPreviewSdk);
        }

        [Fact]
        public void It_does_not_display_preview_message_with_explicit_opt_out()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:_NETCoreSdkIsPreview=true", "/p:SuppressNETCoreSdkPreviewMessage=true")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining(Strings.UsingPreviewSdk);
        }

        [Fact]
        public void It_does_not_display_preview_message_with_nowarn_opt_out()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:_NETCoreSdkIsPreview=true", "/p:NoWarn=NETSDK1057")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining(Strings.UsingPreviewSdk);
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
                .And.NotHaveStdOutContaining(Strings.UsingPreviewSdk);
        }
    }
}
