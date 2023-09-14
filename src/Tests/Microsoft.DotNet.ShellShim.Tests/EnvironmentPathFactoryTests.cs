// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class EnvironmentPathFactoryTests
    {
        [MacOsOnlyFact]
        public void GivenFollowingEnvironmentVariableValueItShouldReturnOsxEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is OsxEnvironmentPath).Should().BeTrue();
        }

        [WindowsOnlyFact]
        public void GivenWindowsItShouldReturnWindowsEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is WindowsEnvironmentPath).Should().BeTrue();
        }

        [LinuxOnlyFact]
        public void GivenLinuxItShouldReturnLinuxEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is LinuxEnvironmentPath).Should().BeTrue();
        }
    }
}
