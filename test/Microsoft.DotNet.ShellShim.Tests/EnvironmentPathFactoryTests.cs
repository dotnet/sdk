// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class EnvironmentPathFactoryTests
    {
        [MacOSOnlyTheory]
        [InlineData("/bin/bash")]
        [InlineData("/bin/zsh")]
        public void GivenFollowingEnvironmentVariableValueItShouldReturnOsxBashEnvironmentPath(string shell)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is MacOSEnvironmentPath).Should().BeTrue();
        }

        [WindowsOnlyFact]
        public void GivenWindowsItShouldReturnOsxBashEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is WindowsEnvironmentPath).Should().BeTrue();
        }

        [LinuxOnlyFact]
        public void GivenLinuxItShouldReturnOsxBashEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is LinuxEnvironmentPath).Should().BeTrue();
        }
    }
}
