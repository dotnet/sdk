// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class AuthHandshakeMessageHandlerTests
    {
        [Theory]
        [InlineData("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [InlineData("SDK_CONTAINER_PUSH_REGISTRY_UNAME", "SDK_CONTAINER_PUSH_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [InlineData("SDK_CONTAINER_PULL_REGISTRY_UNAME", "SDK_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.Pull)]
        [InlineData("SDK_CONTAINER_PULL_REGISTRY_UNAME", "SDK_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        [InlineData("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        public void GetDockerCredentialsFromEnvironment_ReturnsCorrectValues(string unameVarName, string pwordVarName, int mode)
        {
            string? originalUnameValue = Environment.GetEnvironmentVariable(unameVarName);
            string? originalPwordValue = Environment.GetEnvironmentVariable(pwordVarName);

            Environment.SetEnvironmentVariable(unameVarName, "uname");
            Environment.SetEnvironmentVariable(pwordVarName, "pword");

            (string? credU, string? credP) = AuthHandshakeMessageHandler.GetDockerCredentialsFromEnvironment((RegistryMode)mode);

            Assert.Equal("uname", credU);
            Assert.Equal("pword", credP);

            // restore env variable values
            Environment.SetEnvironmentVariable(unameVarName, originalUnameValue);
            Environment.SetEnvironmentVariable(pwordVarName, originalPwordValue);
        }
    }
}
