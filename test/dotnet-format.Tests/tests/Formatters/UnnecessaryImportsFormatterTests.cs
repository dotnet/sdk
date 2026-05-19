// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class UnnecessaryImportsFormatterTests : CSharpFormatterTests
    {
        internal const string IDE0005 = nameof(IDE0005);
        internal const string Style = nameof(Style);

        private const string RemoveUnnecessaryImportDiagnosticKey =
            AnalyzerOptionsExtensions.DotnetDiagnosticPrefix + "." + IDE0005 + "." + AnalyzerOptionsExtensions.SeveritySuffix;
        private const string RemoveUnnecessaryImportCategoryKey =
            AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticPrefix + "." + AnalyzerOptionsExtensions.CategoryPrefix + "-" + Style + "." + AnalyzerOptionsExtensions.SeveritySuffix;

        private protected override ICodeFormatter Formatter => AnalyzerFormatter.CodeStyleFormatter;

        public UnnecessaryImportsFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Fact]
        public async Task WhenNotFixingCodeSyle_AndHasUnusedImports_NoChange()
        {
            var code =
@"using System;

internal class C
{
}";

            var editorConfig = new Dictionary<string, string>();

            await AssertCodeUnchangedAsync(code, editorConfig, fixCategory: FixCategory.Whitespace, codeStyleSeverity: DiagnosticSeverity.Info);
        }

        [Fact]
        public async Task WhenIDE0005NotConfigured_AndHasUnusedImports_NoChange()
        {
            var code =
@"using System;

internal class C
{
}";

            var editorConfig = new Dictionary<string, string>();

            await AssertCodeUnchangedAsync(code, editorConfig, fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Info);
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Info)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Info)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Info)]
        public async Task WhenIDE0005SeverityLowerThanFixSeverity_AndHasUnusedImports_NoChange(string key, string severity)
        {
            var code =
@"using System;

internal class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeUnchangedAsync(code, editorConfig, fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Error);
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
        public async Task WhenIDE0005SeverityEqualOrGreaterThanFixSeverity_AndHasUnusedImports_ImportRemoved(string key, string severity)
        {
            var testCode =
@"using System;

internal class C
{
}";

            var expectedCode =
@"internal class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Warning);
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
        public async Task WhenIDE0005SeverityEqualOrGreaterThanFixSeverity_AndHasUnusedImports_AndIncludedInDiagnosticsList_ImportRemoved(string key, string severity)
        {
            var testCode =
@"using System;

internal class C
{
}";

            var expectedCode =
@"internal class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Warning, diagnostics: new[] { IDE0005 });
        }

        [Theory]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [InlineData(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [InlineData(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
        public async Task WhenIDE0005SeverityEqualOrGreaterThanFixSeverity_AndHasUnusedImports_AndNotIncludedInDiagnosticsList_ImportNotRemoved(string key, string severity)
        {
            var testCode =
@"using System;

internal class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                [key] = severity
            };

            await AssertCodeUnchangedAsync(testCode, editorConfig, fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Warning, diagnostics: new[] { "IDE0073" });
        }
    }
}
