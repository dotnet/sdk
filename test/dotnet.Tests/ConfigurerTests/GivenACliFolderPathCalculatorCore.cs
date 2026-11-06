// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer.UnitTests
{
    [TestClass]
    public class GivenACliFolderPathCalculatorCore
    {
        [TestMethod]
        public void UsesDotnetCliHomeWhenSet()
        {
            var calculator = new CliFolderPathCalculatorCore(name =>
                name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? "/custom/home" : null);

            calculator.GetDotnetHomePath().Should().Be("/custom/home");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void FallsBackToUserProfileOnWindowsWhenDotnetCliHomeIsEmpty()
        {
            var calculator = new CliFolderPathCalculatorCore(name => name switch
            {
                CliFolderPathCalculatorCore.DotnetHomeVariableName => "",
                "USERPROFILE" => @"C:\Users\test",
                _ => null,
            });

            calculator.GetDotnetHomePath().Should().Be(@"C:\Users\test");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void FallsBackToHomeOnUnixWhenDotnetCliHomeIsEmpty()
        {
            var calculator = new CliFolderPathCalculatorCore(name => name switch
            {
                CliFolderPathCalculatorCore.DotnetHomeVariableName => "",
                "HOME" => "/home/test",
                _ => null,
            });

            calculator.GetDotnetHomePath().Should().Be("/home/test");
        }

        [TestMethod]
        public void GetDotnetUserProfileFolderPathAppendsDotnetDirectory()
        {
            var calculator = new CliFolderPathCalculatorCore(name =>
                name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? "/custom/home" : null);

            calculator.GetDotnetUserProfileFolderPath()
                .Should().Be(Path.Combine("/custom/home", CliFolderPathCalculatorCore.DotnetProfileDirectoryName));
        }
    }
}
