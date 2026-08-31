// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Assertions
{
    public class CommandResultAssertions
    {
        private CommandResult _commandResult;

        public CommandResultAssertions(CommandResult commandResult)
        {
            _commandResult = commandResult;
        }

        public AndConstraint<CommandResultAssertions> ExitWith(int expectedExitCode)
        {
            _commandResult.ExitCode.Should().Be(expectedExitCode, AppendDiagnosticsTo($"Expected command to exit with {expectedExitCode} but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> Pass()
        {
            _commandResult.ExitCode.Should().Be(0, AppendDiagnosticsTo("Expected command to pass but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> Fail()
        {
            _commandResult.ExitCode.Should().NotBe(0, AppendDiagnosticsTo("Expected command to fail but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOut()
        {
            _commandResult.StdOut.Should().NotBeNullOrEmpty(AppendDiagnosticsTo("Command did not output anything to stdout"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOut(string expectedOutput)
        {
            _commandResult.StdOut.Should().NotBeNull()
                .And.Be(expectedOutput, AppendDiagnosticsTo($"Command did not output with Expected Output. Expected: {expectedOutput}"), StringComparison.Ordinal);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOutContaining(string pattern)
        {
            _commandResult.StdOut.Should().NotBeNull()
                .And.Contain(pattern, AppendDiagnosticsTo($"The command output did not contain expected result: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOutContaining(Func<string?, bool> predicate, string description = "")
        {
            _commandResult.StdOut.Should().NotBeNull();
            predicate(_commandResult.StdOut).Should().BeTrue(AppendDiagnosticsTo($"The command output did not contain expected result: {description} {Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdOutContaining(string pattern, string[]? ignoredPatterns = null)
        {
            _commandResult.StdOut.Should().NotBeNull();

            string filteredStdOut = _commandResult.StdOut ?? string.Empty;
            if (ignoredPatterns != null && ignoredPatterns.Length > 0)
            {
                foreach (var ignoredPattern in ignoredPatterns)
                {
                    filteredStdOut = string.Join(Environment.NewLine, filteredStdOut
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                        .Where(line => !line.Contains(ignoredPattern)));
                }
            }

            // Perform the assertion on the filtered output
            filteredStdOut.Should().NotContain(pattern, AppendDiagnosticsTo($"The command output contained a result it should not have contained: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            _commandResult.StdOut.Should().NotBeNull();
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", "");
            commandResultNoSpaces.Should().Contain(pattern, AppendDiagnosticsTo($"The command output did not contain expected result: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOutContainingIgnoreCase(string pattern)
        {
            _commandResult.StdOut.Should().NotBeNull()
                .And.ContainEquivalentOf(pattern, AppendDiagnosticsTo($"The command output did not contain expected result (ignoring case): {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            _commandResult.StdOut.Should().NotBeNull();
            Regex.Match(_commandResult.StdOut ?? string.Empty, pattern, options).Success.Should().BeTrue(AppendDiagnosticsTo($"Matching the command output failed. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            _commandResult.StdOut.Should().NotBeNull();
            Regex.Match(_commandResult.StdOut ?? string.Empty, pattern, options).Success.Should().BeFalse(AppendDiagnosticsTo($"The command output matched a pattern it should not have. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErr()
        {
            _commandResult.StdErr.Should().NotBeNullOrEmpty(AppendDiagnosticsTo("Command did not output anything to StdErr."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErr(string expectedOutput)
        {
            _commandResult.StdErr.Should().NotBeNull()
                .And.Be(expectedOutput, AppendDiagnosticsTo($"Command did not output the expected output to StdErr.{Environment.NewLine}Expected: {expectedOutput}{Environment.NewLine}Actual:   {_commandResult.StdErr}"), StringComparison.Ordinal);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErrContaining(string pattern)
        {
            _commandResult.StdErr.Should().NotBeNull()
                .And.Contain(pattern, AppendDiagnosticsTo($"The command error output did not contain expected result: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErrContainingOnce(string pattern)
        {
            var lines = _commandResult.StdErr?.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var matchingLines = lines?.Where(line => line.Contains(pattern)).Count();
            matchingLines.Should().NotBe(0, AppendDiagnosticsTo($"The command error output did not contain expected result: {pattern}{Environment.NewLine}"))
                .And.Be(1, AppendDiagnosticsTo($"The command error output was expected to contain the pattern '{pattern}' once, but found it {matchingLines} times.{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }


        public AndConstraint<CommandResultAssertions> NotHaveStdErrContaining(string pattern)
        {
            _commandResult.StdErr.Should().NotBeNull()
                .And.NotContain(pattern, AppendDiagnosticsTo($"The command error output contained a result it should not have contained: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            _commandResult.StdErr.Should().NotBeNull();
            Regex.Match(_commandResult.StdErr ?? string.Empty, pattern, options).Success.Should().BeTrue(AppendDiagnosticsTo($"Matching the command error output failed. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdOut()
        {
            _commandResult.StdOut.Should().BeNullOrEmpty(AppendDiagnosticsTo($"Expected command to not output to stdout but it was not:"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdErr()
        {
            _commandResult.StdErr.Should().BeNullOrEmpty(AppendDiagnosticsTo("Expected command to not output to stderr but it was not:"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        private string AppendDiagnosticsTo(string s)
        {
            return s + $"{Environment.NewLine}" +
                       $"Working Directory: {_commandResult.StartInfo?.WorkingDirectory}{Environment.NewLine}" +
                       $"File Name: {_commandResult.StartInfo?.FileName}{Environment.NewLine}" +
                       $"Arguments: {_commandResult.StartInfo?.Arguments}{Environment.NewLine}" +
                       $"Exit Code: {_commandResult.ExitCode}{Environment.NewLine}" +
                       $"StdOut:{Environment.NewLine}{_commandResult.StdOut}{Environment.NewLine}" +
                       $"StdErr:{Environment.NewLine}{_commandResult.StdErr}{Environment.NewLine}"; ;
        }

        public AndConstraint<CommandResultAssertions> HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            _commandResult.StdOut.Should().Contain($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.");

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            _commandResult.StdOut.Should().Contain($"Project {compiledProject} ({frameworkFullName}) will be compiled");

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NuPkgContain(string nupkgPath, params string[] filePaths)
        {
            var unzipped = ReadNuPkg(nupkgPath, filePaths);

            foreach (var filePath in filePaths)
            {
                File.Exists(Path.Combine(unzipped, filePath)).Should().BeTrue(AppendDiagnosticsTo($"NuGet Package did not contain file {filePath}."));
            }

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NuPkgContainsPatterns(string nupkgPath, params string[] filePatterns)
        {
            var unzipped = ReadNuPkg(nupkgPath, []);

            foreach (var pattern in filePatterns)
            {
                var directory = Path.GetDirectoryName(pattern);
                var path = Path.Combine(unzipped, directory ?? string.Empty);
                var searchPattern = Path.GetFileName(pattern);

                var condition = Directory.GetFiles(path, searchPattern).Length < 1;
                condition.Should().BeFalse(AppendDiagnosticsTo($"NuGet Package did not contain file {pattern}."));
            }

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NuPkgDoesNotContainPatterns(string nupkgPath, params string[] filePatterns)
        {
            var unzipped = ReadNuPkg(nupkgPath, []);

            foreach (var pattern in filePatterns)
            {
                var directory = Path.GetDirectoryName(pattern);
                var path = Path.Combine(unzipped, directory ?? string.Empty);
                var searchPattern = Path.GetFileName(pattern);

                var condition = Directory.Exists(path) && Directory.GetFiles(path, searchPattern).Length > 0;
                condition.Should().BeFalse(AppendDiagnosticsTo($"NuGet Package contains file {pattern}."));
            }

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NuPkgDoesNotContain(string nupkgPath, params string[] filePaths)
        {
            var unzipped = ReadNuPkg(nupkgPath, filePaths);

            foreach (var filePath in filePaths)
            {
                File.Exists(Path.Combine(unzipped, filePath)).Should().BeFalse(AppendDiagnosticsTo($"NuGet Package contained file: {filePath}."));
            }

            return new AndConstraint<CommandResultAssertions>(this);

        }

        private string ReadNuPkg(string nupkgPath, params string[] filePaths)
        {
            if (nupkgPath == null)
            {
                throw new ArgumentNullException(nameof(nupkgPath));
            }

            if (filePaths == null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

            new FileInfo(nupkgPath).Should().Exist();

            var unzipped = Path.Combine(nupkgPath, "..", Path.GetFileNameWithoutExtension(nupkgPath));
            ZipFile.ExtractToDirectory(nupkgPath, unzipped);

            return unzipped;
        }

        public AndConstraint<CommandResultAssertions> NuSpecDoesNotContain(string nuspecPath, string expected)
        {
            if (nuspecPath == null)
            {
                throw new ArgumentNullException(nameof(nuspecPath));
            }

            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            new FileInfo(nuspecPath).Should().Exist();
            var content = File.ReadAllText(nuspecPath);

            content.Should().NotContain(expected, AppendDiagnosticsTo($"NuSpec contains string: {expected}."));

            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NuSpecContain(string nuspecPath, string expected)
        {
            if (nuspecPath == null)
            {
                throw new ArgumentNullException(nameof(nuspecPath));
            }

            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            new FileInfo(nuspecPath).Should().Exist();
            var content = File.ReadAllText(nuspecPath);

            content.Should().Contain(expected, AppendDiagnosticsTo($"NuSpec does not contain string: {expected}."));

            return new AndConstraint<CommandResultAssertions>(this);
        }
    }
}
