// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAFrameworkDependentApp : SdkTest
    {
        private const string TestProjectName = "HelloWorld";

        public GivenThatWeWantToPublishAFrameworkDependentApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(null, "net6.0")]
        [InlineData("true", "net6.0")]
        [InlineData("false", "net6.0")]
        [InlineData(null, "net7.0")]
        [InlineData("true", "net7.0")]
        [InlineData("false", "net7.0")]
        [InlineData(null, ToolsetInfo.CurrentTargetFramework)]
        [InlineData("true", ToolsetInfo.CurrentTargetFramework)]
        [InlineData("false", ToolsetInfo.CurrentTargetFramework)]
        public void It_publishes_with_or_without_apphost(string useAppHost, string targetFramework)
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            var appHostName = $"{TestProjectName}{Constants.ExeSuffix}";

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName, $"It_publishes_with_or_without_apphost_{(useAppHost ?? "null")}_{targetFramework}")
                .WithSource()
                .WithTargetFramework(targetFramework);

            var msbuildArgs = new List<string>()
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}",
                "/p:SelfContained=false",
            };

            if (useAppHost != null)
            {
                msbuildArgs.Add($"/p:UseAppHost={useAppHost}");
            }

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(msbuildArgs.ToArray())
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            var expectedFiles = new List<string>()
            {
                $"{TestProjectName}.dll",
                $"{TestProjectName}.pdb",
                $"{TestProjectName}.deps.json",
                $"{TestProjectName}.runtimeconfig.json",
            };

            if (useAppHost != "false")
            {
                expectedFiles.Add(appHostName);
            }

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(expectedFiles);

            // Run the apphost if one was generated
            if (useAppHost != "false")
            {
                new RunExeCommand(Log, Path.Combine(publishDirectory.FullName, appHostName))
                    .WithEnvironmentVariable(
                        Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                        Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World!");
            }
        }

        [Fact]
        public void It_errors_when_using_app_host_with_older_target_framework()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource()
                .WithTargetFramework("netcoreapp2.0");

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(
                    "/p:SelfContained=false",
                    "/p:UseAppHost=true",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.FrameworkDependentAppHostRequiresVersion21.Replace("�", "\"").Replace("�", "\""));
        }
    }
}
