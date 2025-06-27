// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class RunParserTests
    {
        public RunParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private readonly ITestOutputHelper output;

        [Fact]
        public void RunParserCanGetArgumentFromDoubleDash()
        {
            var tam = new TestAssetsManager(output);
            var oldWorkingDirectory = Directory.GetCurrentDirectory();
            var newWorkingDir = tam.CopyTestAsset("HelloWorld").Path;

            try
            {
                Directory.SetCurrentDirectory(newWorkingDir);
                var projectPath = Path.Combine(newWorkingDir, "HelloWorld.csproj");
                
                var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath, "--", "foo" });
                runCommand.Args.Single().Should().Be("foo");
            }
            finally
            {
                Directory.SetCurrentDirectory(oldWorkingDirectory);
            }
        }
    }
}
