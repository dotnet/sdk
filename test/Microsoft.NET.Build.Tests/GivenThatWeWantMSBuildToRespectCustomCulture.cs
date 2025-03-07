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

        [InlineData("net7.0")]
        [InlineData("net6.0")]
        // Until MSBuild 17.13 is merged into FullFW MSBuild in sdk tests - the WarnOnCultureOverwritten
        //  is not supported - and the build will fail copy (more details in the last test).
        [CoreMSBuildOnlyTheory]
        public void SupportRespectAlreadyAssignedItemCulture_IsNotSupported_BuildShouldWarn(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("MSBuildCultureResourceGeneration", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            // Custom culture is allowed, but if set explicitly and overwritten - a warning is issued.
            // However the warning is explicit opt-in.
            buildCommand.Execute("/p:WarnOnCultureOverwritten=true").Should().Pass().And
                // warning MSB3002: Explicitly set culture "test-1" for item "Resources.test-1.resx" was overwritten with inferred culture "", because 'RespectAlreadyAssignedItemCulture' property was not set.
                .HaveStdOutContaining("warning MSB3002:");
        }

        [Theory]
        [InlineData("net7.0")]
        [InlineData("net6.0")]
        // Is this Failing? Check if WarnOnCultureOverwritten hasn't been made a default opt-in in sdk or MSBuild.
        //
        // Without explicit opt-in via WarnOnCultureOverwritten - the test will fail, as
        //  proper recognition of custom cultures in RAR is not supported and hence the build will fail during copy:
        //
        // Microsoft.Common.CurrentVersion.targets(4959,5): error MSB3030: Could not copy the file "obj\Debug\net7.0\test-1\MSBuildCultureResourceGeneration.resources.dll" because it was not found.
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
