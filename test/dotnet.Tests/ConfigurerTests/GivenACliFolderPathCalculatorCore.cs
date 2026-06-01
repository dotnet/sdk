// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenACliFolderPathCalculatorCore
    {
        [Fact]
        public void It_uses_DOTNET_CLI_HOME_when_set()
        {
            var calculator = new CliFolderPathCalculatorCore(name =>
                name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? "/custom/home" : null);

            calculator.GetDotnetHomePath().Should().Be("/custom/home");
        }

        [WindowsOnlyFact]
        public void It_falls_back_to_USERPROFILE_on_Windows_when_DOTNET_CLI_HOME_is_empty()
        {
            var calculator = new CliFolderPathCalculatorCore(name => name switch
            {
                CliFolderPathCalculatorCore.DotnetHomeVariableName => "",
                "USERPROFILE" => @"C:\Users\test",
                _ => null,
            });

            calculator.GetDotnetHomePath().Should().Be(@"C:\Users\test");
        }

        [UnixOnlyFact]
        public void It_falls_back_to_HOME_on_Unix_when_DOTNET_CLI_HOME_is_empty()
        {
            var calculator = new CliFolderPathCalculatorCore(name => name switch
            {
                CliFolderPathCalculatorCore.DotnetHomeVariableName => "",
                "HOME" => "/home/test",
                _ => null,
            });

            calculator.GetDotnetHomePath().Should().Be("/home/test");
        }

        [Fact]
        public void GetDotnetUserProfileFolderPath_appends_dotnet_directory()
        {
            var calculator = new CliFolderPathCalculatorCore(name =>
                name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? "/custom/home" : null);

            calculator.GetDotnetUserProfileFolderPath()
                .Should().Be(Path.Combine("/custom/home", CliFolderPathCalculatorCore.DotnetProfileDirectoryName));
        }
    }
}
