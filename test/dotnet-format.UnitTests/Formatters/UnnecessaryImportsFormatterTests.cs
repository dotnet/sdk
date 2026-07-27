// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    [TestClass]
    public class UnnecessaryImportsFormatterTests : CSharpFormatterTests
    {
        internal const string IDE0005 = nameof(IDE0005);
        internal const string Style = nameof(Style);

        private const string RemoveUnnecessaryImportDiagnosticKey =
            AnalyzerOptionsExtensions.DotnetDiagnosticPrefix + "." + IDE0005 + "." + AnalyzerOptionsExtensions.SeveritySuffix;
        private const string RemoveUnnecessaryImportCategoryKey =
            AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticPrefix + "." + AnalyzerOptionsExtensions.CategoryPrefix + "-" + Style + "." + AnalyzerOptionsExtensions.SeveritySuffix;

        private protected override ICodeFormatter Formatter => AnalyzerFormatter.CodeStyleFormatter;

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Info)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Info)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Info)]
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

        [TestMethod]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
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

        [TestMethod]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
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

        [TestMethod]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportDiagnosticKey, Severity.Error)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Warning)]
        [DataRow(RemoveUnnecessaryImportCategoryKey, Severity.Error)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Warning)]
        [DataRow(AnalyzerOptionsExtensions.DotnetAnalyzerDiagnosticSeverityKey, Severity.Error)]
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
