// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantMSBuildToRespectCustomCulture : SdkTest
    {

        public GivenThatWeWantMSBuildToRespectCustomCulture(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void SupportRespectAlreadyAssignedItemCulture_ByDefault_ForDotnet9(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("MSBuildCultureResourceGeneration", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();
            var outputDirectory = buildCommand.GetOutputDirectory().FullName;

            new FileInfo(Path.Combine(outputDirectory, "test-1", "MSBuildCultureResourceGeneration.resources.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputDirectory, "test-2", "MSBuildCultureResourceGeneration.resources.dll")).Should().Exist();
        }

        [Theory]
        [InlineData("net7.0")]
        [InlineData("net6.0")]
        public void SupportRespectAlreadyAssignedItemCulture_IsNotSupported_BuildShouldFail(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("MSBuildCultureResourceGeneration", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Fail();
        }
    }
}
