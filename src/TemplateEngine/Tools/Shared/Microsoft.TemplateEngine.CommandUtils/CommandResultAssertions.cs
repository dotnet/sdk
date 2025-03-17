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
            Assert.Equal(expectedExitCode, _commandResult.ExitCode);
            return this;
        }

        internal CommandResultAssertions Pass()
        {
            Assert.Equal(0, _commandResult.ExitCode);
            return this;
        }

        internal CommandResultAssertions Fail()
        {
            Assert.NotEqual(0, _commandResult.ExitCode);
            return this;
        }

        internal CommandResultAssertions HaveStdOut()
        {
            Assert.False(string.IsNullOrEmpty(_commandResult.StdOut));
            return this;
        }

        internal CommandResultAssertions HaveStdOut(string expectedOutput)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.Equal(expectedOutput, _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.Contains(pattern, _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(Func<string, bool> predicate, string description = "")
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(predicate(_commandResult.StdOut));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.DoesNotContain(pattern, _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", string.Empty);
            Assert.Contains(pattern, commandResultNoSpaces);
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreCase(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.Contains(pattern, _commandResult.StdOut, StringComparison.OrdinalIgnoreCase);
            return this;
        }

        internal CommandResultAssertions HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.Matches(pattern, _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.DoesNotMatch(pattern, _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions HaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.False(string.IsNullOrEmpty(_commandResult.StdErr));
            return this;
        }

        internal CommandResultAssertions HaveStdErr(string expectedOutput)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.Equal(expectedOutput, _commandResult.StdErr);
            return this;
        }

        internal CommandResultAssertions HaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.Contains(pattern, _commandResult.StdErr);
            return this;
        }

        internal CommandResultAssertions NotHaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.DoesNotContain(pattern, _commandResult.StdErr);
            return this;
        }

        internal CommandResultAssertions HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.Matches(pattern, _commandResult.StdErr);
            return this;
        }

        internal CommandResultAssertions NotHaveStdOut()
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Assert.True(string.IsNullOrEmpty(_commandResult.StdOut));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Assert.True(string.IsNullOrEmpty(_commandResult.StdErr));
            return this;
        }

        internal CommandResultAssertions HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            Assert.Contains($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.", _commandResult.StdOut);
            return this;
        }

        internal CommandResultAssertions HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            Assert.Contains($"Project {compiledProject} ({frameworkFullName}) will be compiled", _commandResult.StdOut);
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
