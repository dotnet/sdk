// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Microsoft.TemplateEngine.CommandUtils
{
    internal class CommandResultAssertions
    {
        private readonly CommandResult _commandResult;

        internal CommandResultAssertions(CommandResult commandResult)
        {
            _commandResult = commandResult;
        }

        internal CommandResultAssertions And => this;

        internal CommandResultAssertions ExitWith(int expectedExitCode)
        {
            AssertTrue(expectedExitCode == _commandResult.ExitCode, AppendDiagnosticsTo($"Expected command to exit with {expectedExitCode}, but it did not."));
            return this;
        }

        internal CommandResultAssertions Pass()
        {
            AssertTrue(_commandResult.ExitCode == 0, AppendDiagnosticsTo("Expected command to pass, but it did not."));
            return this;
        }

        internal CommandResultAssertions Fail()
        {
            AssertFalse(_commandResult.ExitCode == 0, AppendDiagnosticsTo("Expected command to fail, but it passed."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut()
        {
            AssertFalse(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to have standard output, but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut(string expectedOutput)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(expectedOutput == _commandResult.StdOut, AppendDiagnosticsTo($"Expected standard output to be '{expectedOutput}', but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(string pattern)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(Func<string, bool> predicate, string description = "")
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(predicate(_commandResult.StdOut), $"The command output did not contain expected result: {description} {Environment.NewLine}");
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutContaining(string pattern)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(!_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to not contain '{pattern}', but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            AssertNotNull(_commandResult.StdOut);
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", string.Empty);
            AssertTrue(commandResultNoSpaces.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreCase(string pattern)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(_commandResult.StdOut.Contains(pattern, StringComparison.OrdinalIgnoreCase), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to match pattern '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(!Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to not match pattern '{pattern}', but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr()
        {
            AssertNotNull(_commandResult.StdErr);
            AssertFalse(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to have standard error, but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr(string expectedOutput)
        {
            AssertNotNull(_commandResult.StdErr);
            AssertTrue(expectedOutput == _commandResult.StdErr, AppendDiagnosticsTo($"Expected standard error to be '{expectedOutput}', but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrContaining(string pattern)
        {
            AssertNotNull(_commandResult.StdErr);
            AssertTrue(_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to contain '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErrContaining(string pattern)
        {
            AssertNotNull(_commandResult.StdErr);
            AssertTrue(!_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to not contain '{pattern}', but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            AssertNotNull(_commandResult.StdErr);
            AssertTrue(Regex.Match(_commandResult.StdErr, pattern, options).Success, AppendDiagnosticsTo($"Expected standard error to match pattern '{pattern}', but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOut()
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to not have standard output, but it did."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErr()
        {
            AssertNotNull(_commandResult.StdErr);
            AssertTrue(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to not have standard error, but it did."));
            return this;
        }

        internal CommandResultAssertions HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(_commandResult.StdOut.Contains($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.', but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            AssertNotNull(_commandResult.StdOut);
            AssertTrue(_commandResult.StdOut.Contains($"Project {compiledProject} ({frameworkFullName}) will be compiled", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {compiledProject} ({frameworkFullName}) will be compiled', but it did not."));
            return this;
        }

        private string AppendDiagnosticsTo(string s)
        {
            return (s + $"{Environment.NewLine}" +
                       $"File Name: {_commandResult.StartInfo.FileName}{Environment.NewLine}" +
                       $"Arguments: {_commandResult.StartInfo.Arguments}{Environment.NewLine}" +
                       $"Exit Code: {_commandResult.ExitCode}{Environment.NewLine}" +
                       $"StdOut:{Environment.NewLine}{_commandResult.StdOut}{Environment.NewLine}" +
                       $"StdErr:{Environment.NewLine}{_commandResult.StdErr}{Environment.NewLine}")
                       //escape curly braces for String.Format
                       .Replace("{", "{{").Replace("}", "}}");
        }

        private static void AssertTrue([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new CommandResultAssertionException(message);
            }
        }

        private static void AssertFalse([DoesNotReturnIf(true)] bool condition, string message)
        {
            if (condition)
            {
                throw new CommandResultAssertionException(message);
            }
        }

        private static void AssertNotNull([NotNull] object? value)
        {
            if (value is null)
            {
                throw new CommandResultAssertionException("Expected value to not be null, but it was.");
            }
        }
    }

    internal sealed class CommandResultAssertionException : Exception
    {
        public CommandResultAssertionException(string message)
            : base(message)
        {
        }
    }
}
