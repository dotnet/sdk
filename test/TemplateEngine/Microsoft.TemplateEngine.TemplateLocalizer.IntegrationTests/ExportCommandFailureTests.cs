// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.TemplateLocalizer.IntegrationTests
{
    public class ExportCommandFailureTests : IDisposable
    {
        private string _workingDirectory;

        private readonly ITestOutputHelper _log;

        public ExportCommandFailureTests(ITestOutputHelper log)
        {
            _log = log;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            _workingDirectory = Path.Combine(Path.GetTempPath(), "Microsoft.TemplateEngine.TemplateLocalizer.IntegrationTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_workingDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_workingDirectory, true);
        }

        [Fact]
        public async Task PostActionsShouldHaveIds()
        {
            string json = @"{
    ""postActions"": [
        {
        },
    ]
}";
            (await CreateTemplateAndExport(json))
                .Execute()
                .Should()
                .ExitWith(1)
                .And.HaveStdOutContaining("Generating localization files for a template.json has failed.")
                .And.HaveStdOutContaining("Json element \"postActions/0\" must have a member \"id\".");
        }

        [Fact]
        public async Task PostActionIdsAreUnique()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postAction1""
        },
        {
            ""id"": ""postAction1""
        }
    ]
}";
            (await CreateTemplateAndExport(json))
                .Execute()
                .Should()
                .ExitWith(1)
                .And.HaveStdOutContaining("Generating localization files for a template.json has failed.")
                .And.HaveStdOutContaining(@"Each child of ""//postActions"" should have a unique id. Currently, the id ""postActions/postAction1"" is shared by multiple children.");
        }

        [Fact]
        public async Task SingleManualInstructionDoesntNeedId()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""text"": ""some text""
                }
            ]
        }
    ]
}";
            (await CreateTemplateAndExport(json))
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Localization files were successfully generated");
        }

        [Fact]
        public async Task MultipleManualInstructionShouldHaveIds()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""text"": ""some text""
                },
                {
                    ""text"": ""some other text""
                }
            ]
        }
    ]
}";
            (await CreateTemplateAndExport(json))
                .Execute()
                .Should()
                .ExitWith(1)
                .And.HaveStdOutContaining("Generating localization files for a template.json has failed.")
                .And.HaveStdOutContaining(@"Json element ""manualInstructions/0"" must have a member ""id"".");
        }

        [Fact]
        public async Task ManualInstructionIdsAreUnique()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""id"": ""mi""
                },
                {
                    ""id"": ""mi""
                },
            ]
        }
    ]
}";
            (await CreateTemplateAndExport(json))
                .Execute()
                .Should()
                .ExitWith(1)
                .And.HaveStdOutContaining("Generating localization files for a template.json has failed.")
                .And.HaveStdOutContaining("Each child of \"//postActions/0/manualInstructions\" should have a unique id. Currently, the id \"manualInstructions/mi\" is shared by multiple children.");
        }

        private async Task<BasicCommand> CreateTemplateAndExport(string templateJsonContent)
        {
            string filePath = Path.Combine(_workingDirectory, Path.GetRandomFileName() + ".json");
            await File.WriteAllTextAsync(filePath, templateJsonContent);
            return new BasicCommand(_log, "Microsoft.TemplateEngine.TemplateLocalizer.exe", "export", filePath);
        }
    }
}
