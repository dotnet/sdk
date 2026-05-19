// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    public class GivenParserDirectives : SdkTest
    {
        public GivenParserDirectives(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItCanAcceptResponseFiles()
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "response.rsp"), "build");
            string[] args = new[] { @"@response.rsp", "-h" };
            new DotnetCommand(Log, args)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(@"dotnet build [<PROJECT | SOLUTION | FILE>...] [options]");
        }
    }
}
