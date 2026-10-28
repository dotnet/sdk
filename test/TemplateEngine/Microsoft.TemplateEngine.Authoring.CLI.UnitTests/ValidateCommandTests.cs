// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.CLI.UnitTests
{
    [TestClass]
    public class ValidateCommandTests : TestBase
    {
        [TestMethod]
        public async Task ValidateCommand_BasicTest_InvalidTemplate()
        {
            RootCommand root = new()
            {
                new ValidateCommand()
            };

            int result = await root.Parse(new[] { "validate", Path.Combine(TestTemplatesLocation, "Invalid") }).InvokeAsync(null, TestContext.Current!.CancellationToken);

            //there are some invalid templates in location "Invalid"
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task ValidateCommand_BasicTest_ValidTemplate()
        {
            RootCommand root = new()
            {
                new ValidateCommand()
            };

            int result = await root.Parse(new[] { "validate", Path.Combine(TestTemplatesLocation, "TemplateWithSourceName") }).InvokeAsync(null, TestContext.Current!.CancellationToken);

            Assert.AreEqual(0, result);
        }
    }
}
