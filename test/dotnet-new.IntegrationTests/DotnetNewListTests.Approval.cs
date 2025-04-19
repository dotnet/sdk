// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewListTests
    {
        [Theory]
        [InlineData("-l")]
        [InlineData("--list")]
        public Task BasicTest_WhenLegacyCommandIsUsed(string commandName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, commandName)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix()
                .AddScrubber(ScrubData);
        }

        [Fact]
        public Task BasicTest_WhenListCommandIsUsed()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .AddScrubber(ScrubData);
        }

        // We're listing the built in templates which we don't control so this fails often
        // By scrubbing out the last three columns and then taking only the unique first words
        // we can ensure that the list is showing columns and they haven't significantly changed.
        // That's the best we can do for validating an output we don't control
        private static void ScrubData(StringBuilder input)
        {
            var lines = input.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var scrubbedLines = new HashSet<string>();
            bool isTable = false;

            foreach (var line in lines)
            {
                // start trimming whitespace and anything but the first word with the start of the table
                if (line.StartsWith("Template Name", StringComparison.Ordinal))
                {
                    isTable = true;
                }

                // We don't want to have to count how many dashes are in the table separator as this can change based on the width of the column
                if (line.StartsWith("-----", StringComparison.Ordinal))
                {
                    continue;
                }

                if (isTable)
                {
                    var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 0)
                    {
                        scrubbedLines.Add(columns[0]);
                    }
                }
                else
                {
                    scrubbedLines.Add(line);
                }
            }

            input.Clear();
            foreach (var scrubbedLine in scrubbedLines)
            {
                input.AppendLine(scrubbedLine);
            }
        }

        [Fact]
        public Task Constraints_CanShowMessageIfTemplateGroupIsRestricted()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task Constraints_CanIgnoreConstraints()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate", "--ignore-constraints")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }
    }
}
