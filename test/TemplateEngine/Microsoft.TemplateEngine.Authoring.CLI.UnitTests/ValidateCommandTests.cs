// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.CLI.UnitTests
{
    public class ValidateCommandTests : TestBase
    {
        [Fact]
        public async Task ValidateCommand_BasicTest_InvalidTemplate()
        {
            CliRootCommand root = new()
            {
                new ValidateCommand()
            };

            int result = await root.Parse(new[] { "validate", Path.Combine(TestTemplatesLocation, "Invalid") }).InvokeAsync();

            //there are some invalid templates in location "Invalid"
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task ValidateCommand_BasicTest_ValidTemplate()
        {
            CliRootCommand root = new()
            {
                new ValidateCommand()
            };

            int result = await root.Parse(new[] { "validate", Path.Combine(TestTemplatesLocation, "TemplateWithSourceName") }).InvokeAsync();

            Assert.Equal(0, result);
        }
    }
}
