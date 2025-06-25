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
            // Create a temporary project file to ensure file validation passes
            var tempDir = Path.GetTempPath();
            var projectPath = Path.Combine(tempDir, "foo.csproj");
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            
            try
            {
                var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath, "--", "foo" });
                runCommand.Args.Single().Should().Be("foo");
            }
            finally
            {
                if (File.Exists(projectPath))
                {
                    File.Delete(projectPath);
                }
            }
        }
    }
}
