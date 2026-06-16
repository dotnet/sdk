// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    public class CodeStyleAnalyzerFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => AnalyzerFormatter.CodeStyleFormatter;

        public CodeStyleAnalyzerFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Fact]
        public async Task TestUseVarCodeStyle_AppliesWhenNotUsingVar()
        {
            var testCode = @"
using System.Collections.Generic;

class C
{
    void M()
    {
        object obj = new object();
        List<string> list = new List<string>();
        int count = 5;
    }
}";

            var expectedCode = @"
using System.Collections.Generic;

class C
{
    void M()
    {
        var obj = new object();
        var list = new List<string>();
        var count = 5;
    }
}";

            var editorConfig = new Dictionary<string, string>()
            {
                /// Prefer "var" everywhere
                ["dotnet_diagnostic.IDE0007.severity"] = "error",
                ["csharp_style_var_for_built_in_types"] = "true:error",
                ["csharp_style_var_when_type_is_apparent"] = "true:error",
                ["csharp_style_var_elsewhere"] = "true:error",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.CodeStyle);
        }

        [Fact]
        public async Task TestNonFixableCompilerDiagnostics_AreNotReported()
        {
            var testCode = @"
class C
{
    public int M()
    {
        return null; // Cannot convert null to 'int' because it is a non-nullable value type (CS0037)
    }
}";

            await AssertNoReportedFileChangesAsync(testCode, "root = true", fixCategory: FixCategory.CodeStyle, codeStyleSeverity: DiagnosticSeverity.Warning);
        }
    }
}
