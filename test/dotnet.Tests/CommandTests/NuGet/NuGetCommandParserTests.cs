// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Extensions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class NuGetCommandParserTests
    {
        [TestMethod]
        [DataRow("--framework net472")]
        [DataRow("-f net472")]
        [DataRow("--framework net472 --framework net6.0")]
        [DataRow("-f net472 -f net6.0")]
        [DataRow("--framework net472 -f net6.0")]
        public void NuGetWhyCommandCanParseFrameworkOptions(string inputOptions)
        {
            var result = Parser.Parse($"dotnet nuget why C:\\path Fake.Package {inputOptions}");

            result.Errors.Should().BeEmpty();
            var parsedArguments = result.GetArguments();

            foreach (var token in inputOptions.Split())
            {
                Assert.Contains(token, parsedArguments);
            }
        }
    }
}
