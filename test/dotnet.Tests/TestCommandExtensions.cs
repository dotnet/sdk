// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    public static class TestCommandExtensions
    {
        public static TestCommand WithUserProfileRoot(this TestCommand testCommand, string path)
        {
            return testCommand.WithEnvironmentVariable("DOTNET_CLI_HOME", path);
        }
    }
}
