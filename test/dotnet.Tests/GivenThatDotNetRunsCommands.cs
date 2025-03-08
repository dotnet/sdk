﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.InteropServices;
using Microsoft.DotNet.Configurer;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatDotNetRunsCommands : SdkTest
    {
        public GivenThatDotNetRunsCommands(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void UnresolvedPlatformReferencesFailAsExpected()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithUnresolvedPlatformDependency", testAssetSubdirectory: "NonRestoredTestProjects")
                            .WithSource();

            new RestoreCommand(testInstance)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should()
                .Fail();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("crash")
                .Should().Fail()
                    .And.HaveStdErrContaining(LocalizableStrings.NoExecutableFoundMatchingCommandErrorMessage)
                    .And.HaveStdOutContaining(string.Format(LocalizableStrings.NoExecutableFoundMatchingCommand, "dotnet-crash"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GivenAMissingHomeVariableItExecutesHelpCommandSuccessfully(string value)
        {
            new DotnetCommand(Log)
                .WithEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME", value)
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, "")
                .Execute("--help")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(LocalizableStrings.DotNetSdkInfo);
        }

        [Fact]
        public void GivenASpecifiedDotnetCliHomeVariableItPrintsUsageMessage()
        {
            var home = _testAssetsManager.CreateTestDirectory(identifier: "DOTNET_HOME").Path;

            new DotnetCommand(Log)
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, home)
                .Execute("-d", "help")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        LocalizableStrings.DotnetCliHomeUsed,
                        home,
                        CliFolderPathCalculator.DotnetHomeVariableName));
        }
    }
}
