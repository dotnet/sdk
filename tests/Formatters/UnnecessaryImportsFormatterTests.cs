// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class UnnecessaryImportsFormatterTests : CSharpFormatterTests
    {
        private const string RemoveUnnecessaryImportDiagnosticKey =
            AnalyzerOptionsExtensions.DotnetDiagnosticPrefix + "." + UnnecessaryImportsFormatter.IDE0005 + "." + AnalyzerOptionsExtensions.SeveritySuffix;
        private const string RemoveUnnecessaryImportCategoryKey =
            AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticPrefix + "." + AnalyzerOptionsExtensions.CategoryPrefix + "-" + UnnecessaryImportsFormatter.Style + "." + AnalyzerOptionsExtensions.SeveritySuffix;

        private protected override ICodeFormatter Formatter => new UnnecessaryImportsFormatter();

        [Fact]
        public async Task WhenNotFixingCodeSyle_AndHasUnusedImports_NoChange()
        {
            var code =
@"using System;

class C
{
}";

            var editorConfig = new Dictionary<string, string>();

            await AssertCodeUnchangedAsync(code, editorConfig, fixCodeStyle: false, codeStyleSeverity: DiagnosticSeverity.Info);
        }

        [Fact]
        public async Task WhenIDE0005NotConfigured_AndHasUnusedImports_NoChange()
        {
            var code =
@"using System;

class C
{
}";

            var editorConfig = new Dictionary<string, string>();

            await AssertCodeUnchangedAsync(code, editorConfig, fixCodeStyle: true, codeStyleSeverity: DiagnosticSeverity.Info);
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, "warning")]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, "info")]
        [InlineData(RemoveUnnecessaryImportCategoryKey, "warning")]
        [InlineData(RemoveUnnecessaryImportCategoryKey, "info")]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, "warning")]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, "info")]
        public async Task WhenIDE0005SeverityLowerThanFixSeverity_AndHasUnusedImports_NoChange(string key, string severity)
        {
            var code =
@"using System;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeUnchangedAsync(code, editorConfig, fixCodeStyle: true, codeStyleSeverity: DiagnosticSeverity.Error);
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, "warning")]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, "error")]
        [InlineData(RemoveUnnecessaryImportCategoryKey, "warning")]
        [InlineData(RemoveUnnecessaryImportCategoryKey, "error")]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, "warning")]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, "error")]
        public async Task WhenIDE0005SeverityEqualOrGreaterThanFixSeverity_AndHasUnusedImports_ImportRemoved(string key, string severity)
        {
            var testCode =
@"using System;

class C
{
}";

            var expectedCode =
@"class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCodeStyle: true, codeStyleSeverity: DiagnosticSeverity.Warning);
        }
    }
}
