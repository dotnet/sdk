// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class VSTestArtifactsDirectoryTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ItFailsTheRunWithAnOverrideHintWhenLocalApplicationDataCannotBeResolved(string localApplicationData)
        {
            Action resolve = () => VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(localApplicationData);

            resolve.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>()
                .WithMessage($"*{VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName}*");
        }

        [Fact]
        public void ItUsesAResolvedLocalApplicationDataAsIs()
        {
            VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(@"C:\Users\someone\AppData\Local")
                .Should().Be(@"C:\Users\someone\AppData\Local");
        }

        [Fact]
        public void ItDoesNotSwallowAFailureToProduceABaseDirectory()
        {
            Action create = () => VSTestArtifactsDirectory.Create(
                () => throw new Microsoft.DotNet.Cli.Utils.GracefulException("no base directory"));

            create.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>().WithMessage("no base directory");
        }
    }
}
