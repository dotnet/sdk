// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    [TestClass]
    public class ZshDetectorTests
    {
        [TestMethod]
        [DataRow("/bin/zsh")]
        [DataRow("/other-place/zsh")]
        public void GivenFollowingEnvironmentVariableValueItCanDetectZsh(string environmentVariableValue)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(environmentVariableValue);

            ZshDetector.IsZshTheUsersShell(provider.Object).Should().BeTrue();
        }

        [TestMethod]
        [DataRow("/bin/bash")]
        [DataRow("/other/value")]
        [DataRow(null)]
        public void GivenFollowingEnvironmentVariableValueItCanDetectItIsNotZsh(string environmentVariableValue)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(environmentVariableValue);

            ZshDetector.IsZshTheUsersShell(provider.Object).Should().BeFalse();
        }
    }
}
