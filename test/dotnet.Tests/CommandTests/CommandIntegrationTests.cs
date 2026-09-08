// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests.Commands
{
    [TestClass]
    public class CommandIntegrationTests : SdkTest
    {
        public CommandIntegrationTests() { }

        [TestMethod]
        public void GivenNoArgumentsProvided()
        {
            var cmd = new DotnetCommand(Log).Execute(string.Empty);
            cmd.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        public void GivenOnlyArgumentProvidedIsDiagnosticsFlag()
        {
            var cmd = new DotnetCommand(Log).Execute("-d");
            cmd.ExitCode.Should().Be(0);
            cmd.StdErr.Should().BeEmpty();
        }
    }
}
