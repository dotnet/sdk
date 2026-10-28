// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// MSTest-friendly mirror of the shared
// src\TemplateEngine\Tools\Shared\Microsoft.TemplateEngine.CommandUtils\CommandResultAssertions.cs.
//
// The shared file uses Xunit's Assert.True / Assert.False / Assert.NotNull APIs. When this
// project runs under MSTest.Sdk those names resolve to MSTest's Assert (via its implicit
// global using of Microsoft.VisualStudio.TestTools.UnitTesting) which does not expose those
// methods. Rather than dragging the Xunit global using in (which would conflict with MSTest's
// Assert), we exclude the shared file in the csproj and provide this local copy that uses the
// equivalent MSTest APIs: Assert.IsTrue / Assert.IsFalse / Assert.IsNotNull.

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
            Assert.AreEqual(expectedExitCode, _commandResult.ExitCode, AppendDiagnosticsTo($"Expected command to exit with {expectedExitCode} but it did not."));
            return this;
        }

        internal CommandResultAssertions Pass()
        {
            Assert.AreEqual(0, _commandResult.ExitCode, AppendDiagnosticsTo("Expected command to pass but it did not."));
            return this;
        }

        internal CommandResultAssertions Fail()
        {
            Assert.AreNotEqual(0, _commandResult.ExitCode, AppendDiagnosticsTo("Expected command to fail but it passed."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut()
        {
            Assert.IsFalse(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to have standard output but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOut(string expectedOutput)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.AreEqual(expectedOutput, _commandResult.StdOut, AppendDiagnosticsTo($"Expected standard output to be '{expectedOutput}' but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContaining(Func<string, bool> predicate, string description = "")
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(predicate(_commandResult.StdOut), $"The command output did not contain expected result: {description} {Environment.NewLine}");
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutContaining(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsFalse(_commandResult.StdOut.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to not contain '{pattern}' but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", string.Empty);
            Assert.IsTrue(commandResultNoSpaces.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutContainingIgnoreCase(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(_commandResult.StdOut.Contains(pattern, StringComparison.OrdinalIgnoreCase), AppendDiagnosticsTo($"Expected standard output to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to match pattern '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsFalse(Regex.Match(_commandResult.StdOut, pattern, options).Success, AppendDiagnosticsTo($"Expected standard output to not match pattern '{pattern}' but it did."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr()
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.IsFalse(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to have standard error but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErr(string expectedOutput)
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.AreEqual(expectedOutput, _commandResult.StdErr, AppendDiagnosticsTo($"Expected standard error to be '{expectedOutput}' but it was not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrContaining(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.IsTrue(_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErrContaining(string pattern)
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.IsFalse(_commandResult.StdErr.Contains(pattern, StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard error to contain '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.IsTrue(Regex.Match(_commandResult.StdErr, pattern, options).Success, AppendDiagnosticsTo($"Expected standard error to match pattern '{pattern}' but it did not."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdOut()
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(string.IsNullOrEmpty(_commandResult.StdOut), AppendDiagnosticsTo("Expected command to not have standard output but it did."));
            return this;
        }

        internal CommandResultAssertions NotHaveStdErr()
        {
            Assert.IsNotNull(_commandResult.StdErr);
            Assert.IsTrue(string.IsNullOrEmpty(_commandResult.StdErr), AppendDiagnosticsTo("Expected command to not have standard error but it did."));
            return this;
        }

        internal CommandResultAssertions HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(_commandResult.StdOut.Contains($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.' but it did not."));
            return this;
        }

        internal CommandResultAssertions HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            Assert.IsNotNull(_commandResult.StdOut);
            Assert.IsTrue(_commandResult.StdOut.Contains($"Project {compiledProject} ({frameworkFullName}) will be compiled", StringComparison.Ordinal), AppendDiagnosticsTo($"Expected standard output to contain 'Project {compiledProject} ({frameworkFullName}) will be compiled' but it did not."));
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
