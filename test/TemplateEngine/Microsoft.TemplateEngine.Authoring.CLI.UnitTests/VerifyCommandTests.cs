// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.TemplateEngine.Authoring.CLI.UnitTests
{
    public class VerifyCommandTests
    {
        public static IEnumerable<object?[]> CanParseVerifyCommandArgsData =>
            new object?[][]
            {
                new object[]
                {
                    "someName -p path --template-args \" a b cc\" --disable-diff-tool",
                    new VerifyCommandArgs(
                        "someName",
                        " a b cc")
                    {
                        TemplatePath = "path",
                        DisableDiffTool = true,
                        DisableDefaultVerificationExcludePatterns = false,
                        VerificationExcludePatterns = Enumerable.Empty<string>(),
                        VerificationIncludePatterns = Enumerable.Empty<string>(),
                        VerifyCommandOutput = false,
                        IsCommandExpectedToFail = false,
                        UniqueFor = UniqueForOption.None,
                    }
                },
                new object[]
                {
                    "someName -p path --template-args \" a \'b cc\'\" --unique-for Runtime",
                    new VerifyCommandArgs(
                        "someName",
                        " a \"b cc\"")
                    {
                        TemplatePath = "path",
                        DisableDiffTool = false,
                        DisableDefaultVerificationExcludePatterns = false,
                        VerificationExcludePatterns = Enumerable.Empty<string>(),
                        VerificationIncludePatterns = Enumerable.Empty<string>(),
                        VerifyCommandOutput = false,
                        IsCommandExpectedToFail = false,
                        UniqueFor = UniqueForOption.Runtime,
                    }
                },

                new object[]
                {
                    "someName",
                    new VerifyCommandArgs(
                        "someName",
                        null)
                    {
                        DisableDiffTool = false,
                        DisableDefaultVerificationExcludePatterns = false,
                        VerificationExcludePatterns = Enumerable.Empty<string>(),
                        VerificationIncludePatterns = Enumerable.Empty<string>(),
                        VerifyCommandOutput = false,
                        IsCommandExpectedToFail = false,
                        UniqueFor = UniqueForOption.None,
                    }
                },
            };

        [Theory]
        [MemberData(nameof(CanParseVerifyCommandArgsData))]
        internal void CanParseVerifyCommandArgs(string command, VerifyCommandArgs expVerifyCommandArgs)
        {
            VerifyCommand verifyCommand = new VerifyCommand();

            ParseResult parseResult = verifyCommand.Parse(command);

            VerifyCommandArgs args = verifyCommand.ParseContext(parseResult);

            args.Should().BeEquivalentTo(expVerifyCommandArgs);
        }
    }
}
