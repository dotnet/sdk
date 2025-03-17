// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            Assert.True(expectedExitCode == _commandResult.ExitCode, AppendDiagnosticsTo($"Expected command to exit with {expectedExitCode} but it did not."));
            return this;
        }

        internal CommandResultAssertions Pass()
        {
            Assert.True(_commandResult.ExitCode == 0, AppendDiagnosticsTo("Expected command to pass but it did not."));
            return this;
        }

        internal CommandResultAssertions Fail()
        {
            Assert.False(_commandResult.ExitCode == 0, AppendDiagnosticsTo("Expected command to fail but it passed."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut()
        {
            Assert.False(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to have standard output but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut(string expectedOutput)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(expectedOutput == _commandResult.StdOut, AppendDiagnosticsTo($"Expected standard output to be '{expectedOutput}' but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(Func<string, bool> predicate, string description = "")
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(predicate(_commandResult.StdOut), $"The command output did not contain expected result: {description} {Environment.NewLine}");
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(!_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to not contain '{pattern}' but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", string.Empty);
            Assert.True(commandResultNoSpaces.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreCase(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(_commandResult.StdOut.Contains(pattern, StringComparison.OrdinalIgnoreCase), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to match pattern '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(!Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to not match pattern '{pattern}' but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.False(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to have standard error but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr(string expectedOutput)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(expectedOutput == _commandResult.StdErr, AppendDiagnosticsTo($"Expected standard error to be '{expectedOutput}' but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(!_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(Regex.Match(_commandResult.StdErr, pattern, options).Success, AppendDiagnosticsTo($"Expected standard error to match pattern '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOut()
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to not have standard output but it did."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to not have standard error but it did."));
            return this;
        }

        internal CommandResultAssertions HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(_commandResult.StdOut.Contains($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(_commandResult.StdOut.Contains($"Project {compiledProject} ({frameworkFullName}) will be compiled", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {compiledProject} ({frameworkFullName}) will be compiled' but it did not."));
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
    }
}
