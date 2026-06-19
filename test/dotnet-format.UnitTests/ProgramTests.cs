// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.CodeAnalysis.Tools.Commands;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using ProductionDotNetHelper = Microsoft.CodeAnalysis.Tools.Utilities.DotNetHelper;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void ExitCodeIsOneWithCheckAndAnyFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 1, fileCount: 0, exitCode: 0);
            var exitCode = FormatCommandCommon.GetExitCode(formatResult, check: true);

            Assert.AreEqual(FormatCommandCommon.CheckFailedExitCode, exitCode);
        }

        [TestMethod]
        public void ExitCodeIsZeroWithCheckAndNoFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
            var exitCode = FormatCommandCommon.GetExitCode(formatResult, check: true);

            Assert.AreEqual(0, exitCode);
        }

        [TestMethod]
        public void ExitCodeIsSameWithoutCheck()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
            var exitCode = FormatCommandCommon.GetExitCode(formatResult, check: false);

            Assert.AreEqual(formatResult.ExitCode, exitCode);
        }

        [TestMethod]
        public void CommandLine_OptionsAreParsedCorrectly()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] {
                "--no-restore",
                "--include", "include1", "include2",
                "--exclude", "exclude1", "exclude2",
                "--verify-no-changes",
                "--binarylog", "binary-log-path",
                "--report", "report",
                "--verbosity", "detailed",
                "--include-generated"});

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.IsEmpty(result.UnmatchedTokens);
            Assert.IsEmpty(result.UnmatchedTokens);
            result.GetValue(FormatCommandCommon.NoRestoreOption);
            var includeValues = result.GetValue(FormatCommandCommon.IncludeOption).ToArray();
            Assert.HasCount(2, includeValues);
            Assert.AreEqual("include1", includeValues[0]);
            Assert.AreEqual("include2", includeValues[1]);
            var excludeValues = result.GetValue(FormatCommandCommon.ExcludeOption).ToArray();
            Assert.HasCount(2, excludeValues);
            Assert.AreEqual("exclude1", excludeValues[0]);
            Assert.AreEqual("exclude2", excludeValues[1]);
            Assert.IsTrue(result.GetValue(FormatCommandCommon.VerifyNoChanges));
            Assert.AreEqual("binary-log-path", result.GetValue(FormatCommandCommon.BinarylogOption));
            Assert.AreEqual("report", result.GetValue(FormatCommandCommon.ReportOption));
            Assert.AreEqual("detailed", result.GetValue(FormatCommandCommon.VerbosityOption));
            Assert.IsTrue(result.GetValue(FormatCommandCommon.IncludeGeneratedOption));
        }

        [TestMethod]
        public void CommandLine_ProjectArgument_Simple()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "workspaceValue" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("workspaceValue", result.GetValue(FormatCommandCommon.SlnOrProjectArgument));
        }

        [TestMethod]
        public void CommandLine_ProjectArgument_WithOption_AfterArgument()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "workspaceValue", "--verbosity", "detailed" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("workspaceValue", result.GetValue(FormatCommandCommon.SlnOrProjectArgument));
            Assert.AreEqual("detailed", result.GetValue(FormatCommandCommon.VerbosityOption));
        }

        [TestMethod]
        public void CommandLine_ProjectArgument_WithOption_BeforeArgument()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--verbosity", "detailed", "workspaceValue" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("workspaceValue", result.GetValue(FormatCommandCommon.SlnOrProjectArgument));
            Assert.AreEqual("detailed", result.GetValue(FormatCommandCommon.VerbosityOption));
        }

        [TestMethod]
        public void CommandLine_ProjectArgument_FailsIfSpecifiedTwice()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "workspaceValue1", "workspaceValue2" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_FolderValidation_FailsIfFixAnalyzersSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--folder", "--fix-analyzers" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_FolderValidation_FailsIfFixStyleSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--folder", "--fix-style" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_FolderValidation_FailsIfNoRestoreSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "whitespace", "--folder", "--no-restore" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_BinaryLog_DoesNotFailIfPathNotSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--binarylog" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.IsNotNull(result.GetResult(FormatCommandCommon.BinarylogOption));
        }

        [TestMethod]
        public void CommandLine_BinaryLog_DoesNotFailIfPathIsSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--binarylog", "log" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.IsNotNull(result.GetResult(FormatCommandCommon.BinarylogOption));
        }

        [TestMethod]
        public void CommandLine_BinaryLog_FailsIfFolderIsSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "whitespace", "--folder", "--binarylog" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_Diagnostics_FailsIfDiagnosticNoSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--diagnostics" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void CommandLine_Diagnostics_DoesNotFailIfDiagnosticIsSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--diagnostics", "RS0016" });

            // Assert
            Assert.IsEmpty(result.Errors);
        }

        [TestMethod]
        public void CommandLine_Diagnostics_DoesNotFailIfMultipleDiagnosticAreSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--diagnostics", "RS0016", "RS0017", "RS0018" });

            // Assert
            Assert.IsEmpty(result.Errors);
        }

        [TestMethod]
        public void CommandLine_FrameworkOption_IsParsedCorrectly()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "--framework", "net8.0" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("net8.0", result.GetValue(FormatCommandCommon.FrameworkOption));
        }

        [TestMethod]
        public void CommandLine_FrameworkOption_ShortAlias_IsParsedCorrectly()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "-f", "net8.0" });

            // Assert
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("net8.0", result.GetValue(FormatCommandCommon.FrameworkOption));
        }

        [TestMethod]
        public void CommandLine_FolderValidation_FailsIfFrameworkSpecified()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();

            // Act
            var result = sut.Parse(new[] { "whitespace", "--folder", "--framework", "net8.0" });

            // Assert
            Assert.ContainsSingle(result.Errors);
        }

        [TestMethod]
        public void ParseCommonOptions_FrameworkOption_SetsTargetFramework()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();
            var result = sut.Parse(new[] { "--framework", "net8.0" });
            var logger = new TestLogger();

            // Act
            var formatOptions = result.ParseCommonOptions(FormatOptions.Instance, logger);

            // Assert
            Assert.AreEqual("net8.0", formatOptions.TargetFramework);
        }

        [TestMethod]
        public void ParseCommonOptions_NoFrameworkOption_LeavesTargetFrameworkNull()
        {
            // Arrange
            var sut = RootFormatCommand.GetCommand();
            var result = sut.Parse(Array.Empty<string>());
            var logger = new TestLogger();

            // Act
            var formatOptions = result.ParseCommonOptions(FormatOptions.Instance, logger);

            // Assert
            Assert.IsNull(formatOptions.TargetFramework);
        }
    }
}
