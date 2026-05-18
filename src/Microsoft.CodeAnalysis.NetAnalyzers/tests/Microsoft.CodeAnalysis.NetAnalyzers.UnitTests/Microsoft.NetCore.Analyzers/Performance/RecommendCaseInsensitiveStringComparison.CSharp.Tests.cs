// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.RecommendCaseInsensitiveStringComparisonAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpRecommendCaseInsensitiveStringComparisonFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class RecommendCaseInsensitiveStringComparison_CSharp_Tests : RecommendCaseInsensitiveStringComparison_Base_Tests
    {
        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedInvertedNamedData))]
        public async Task Diagnostic_Assign(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    void M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        var result = [|{diagnosedLine}|];
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    void M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        var result = {fixedLine};
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedInvertedNamedData))]
        public async Task Diagnostic_Return(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    object M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        return [|{diagnosedLine}|];
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    object M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        return {fixedLine};
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedImplicitBooleanData))]
        [MemberData(nameof(DiagnosedAndFixedWithAppendedMethodData))]
        [MemberData(nameof(DiagnosedAndFixedWithAppendedMethodInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedWithAppendedMethodNamedData))]
        public async Task Diagnostic_If(string diagnosedLine, string fixedLine, string appendedMethod)
        {
            string originalCode = $@"using System;
class C
{{
    int M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        bool myBoolean = false;
        if ([|{diagnosedLine}|]{appendedMethod})
        {{
            return 5;
        }}
        return 4;
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    int M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        bool myBoolean = false;
        if ({fixedLine}{appendedMethod})
        {{
            return 5;
        }}
        return 4;
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedInvertedNamedData))]
        public async Task Diagnostic_IgnoreResult(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    void M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        [|{diagnosedLine}|];
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    void M()
    {{
        string a = ""aBc"";
        string b = ""bc"";
        {fixedLine};
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedStringLiteralsData))]
        [MemberData(nameof(DiagnosedAndFixedStringLiteralsInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedStringLiteralsNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedStringLiteralsInvertedNamedData))]
        public async Task Diagnostic_StringLiterals_ReturnExpressionBody(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    object M() => [|{diagnosedLine}|];
}}";
            string fixedCode = $@"using System;
class C
{{
    object M() => {fixedLine};
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedStringReturningMethodsData))]
        [MemberData(nameof(DiagnosedAndFixedStringReturningMethodsInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedStringReturningMethodsNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedStringReturningMethodsInvertedNamedData))]
        public async Task Diagnostic_StringReturningMethods_Discard(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    public string GetStringA() => ""aBc"";
    public string GetStringB() => ""CdE"";
    void M()
    {{
        _ = [|{diagnosedLine}|];
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    public string GetStringA() => ""aBc"";
    public string GetStringB() => ""CdE"";
    void M()
    {{
        _ = {fixedLine};
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedParenthesizedData))]
        [MemberData(nameof(DiagnosedAndFixedParenthesizedInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedParenthesizedNamedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedParenthesizedNamedInvertedData))]
        [MemberData(nameof(CSharpDiagnosedAndFixedParenthesizedComplexCasesData))]
        public async Task Diagnostic_Parenthesized_ReturnCastedToString(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    string GetString() => ""aBc"";
    string M()
    {{
        string a = ""aBc"";
        string b = ""cDe"";
        return ([|{diagnosedLine}|]).ToString();
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    string GetString() => ""aBc"";
    string M()
    {{
        string a = ""aBc"";
        string b = ""cDe"";
        return ({fixedLine}).ToString();
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpDiagnosedAndFixedEqualityToEqualsData))]
        public async Task Diagnostic_Equality_To_Equals(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"using System;
class C
{{
    string GetString() => ""cde"";
    bool M(string a, string b)
    {{
        bool result = [|{diagnosedLine}|];
        if ([|{diagnosedLine}|]) return result;
        return [|{diagnosedLine}|];
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    string GetString() => ""cde"";
    bool M(string a, string b)
    {{
        bool result = {fixedLine};
        if ({fixedLine}) return result;
        return {fixedLine};
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_Equality_To_Equals_Trivia()
        {
            string originalCode = $@"using System;
class C
{{
    bool M(string a, string b)
    {{
        // Trivia
        bool /* Trivia */ result = /* Trivia */ [|a.ToLowerInvariant() // Trivia
            == /* Trivia */ b.ToLowerInvariant()|] /* Trivia */; // Trivia
        if (/* Trivia */ [|a.ToLower() /* Trivia */ != /* Trivia */ b.ToLower()|] /* Trivia */) // Trivia
            return /* Trivia */ [|b /* Trivia */ != /* Trivia */ a.ToLowerInvariant()|] /* Trivia */; // Trivia
        return // Trivia
            [|""abc"" /* Trivia */ == /* Trivia */ a.ToUpperInvariant()|] /* Trivia */; // Trivia
        // Trivia
    }}
}}";
            string fixedCode = $@"using System;
class C
{{
    bool M(string a, string b)
    {{
        // Trivia
        bool /* Trivia */ result = /* Trivia */ a.Equals(b, StringComparison.InvariantCultureIgnoreCase) /* Trivia */; // Trivia
        if (/* Trivia */ !a.Equals(b, StringComparison.CurrentCultureIgnoreCase) /* Trivia */) // Trivia
            return /* Trivia */ !b /* Trivia */ .Equals /* Trivia */ (a, StringComparison.InvariantCultureIgnoreCase) /* Trivia */; // Trivia
        return // Trivia
            ""abc"" /* Trivia */ .Equals /* Trivia */ (a, StringComparison.InvariantCultureIgnoreCase) /* Trivia */; // Trivia
        // Trivia
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(NoDiagnosticData))]
        [InlineData("\"aBc\".CompareTo(null)")]
        [InlineData("\"aBc\".ToUpperInvariant().CompareTo((object)null)")]
        [InlineData("\"aBc\".CompareTo(value: (object)\"cDe\")")]
        [InlineData("\"aBc\".CompareTo(strB: \"cDe\")")]
        public async Task NoDiagnostic_All(string ignoredLine)
        {
            string originalCode = $@"using System;
class C
{{
    object M()
    {{
        char ch = 'c';
        object obj = 3;
        return {ignoredLine};
    }}
}}";

            await VerifyNoDiagnosticCSharpAsync(originalCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosticNoFixStartsWithContainsIndexOfData))]
        public async Task Diagnostic_NoFix_StartsWithContainsIndexOf(string diagnosedLine)
        {
            string originalCode = $@"using System;
class C
{{
    string GetStringA() => ""aBc"";
    string GetStringB() => ""cDe"";
    object M()
    {{
        string a = ""AbC"";
        string b = ""CdE"";
        return [|{diagnosedLine}|];
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, originalCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosticNoFixCompareToData))]
        [MemberData(nameof(DiagnosticNoFixCompareToInvertedData))]
        [MemberData(nameof(CSharpDiagnosticNoFixCompareToNamedData))]
        [MemberData(nameof(CSharpDiagnosticNoFixCompareToInvertedNamedData))]
        public async Task Diagnostic_NoFix_CompareTo(string diagnosedLine)
        {
            string originalCode = $@"using System;
class C
{{
    string GetStringA() => ""aBc"";
    string GetStringB() => ""cDe"";
    int M()
    {{
        string a = ""AbC"";
        string b = ""CdE"";
        return [|{diagnosedLine}|];
    }}
}}";
            await VerifyFixCSharpAsync(originalCode, originalCode);
        }

        [Theory]
        [MemberData(nameof(CSharpDiagnosticNoFixEqualsData))]
        public async Task Diagnostic_NoFix_Equals(string diagnosedLine)
        {
            string originalCode = $@"using System;
class C
{{
    string GetString() => string.Empty;
    bool M()
    {{
        string a = ""aBc"";
        string b = ""dEf"";
        return [|{diagnosedLine}|];
    }}
}}";

            await VerifyFixCSharpAsync(originalCode, originalCode);
        }

        [Fact, WorkItem(7053, "https://github.com/dotnet/roslyn-analyzers/issues/7053")]
        public Task Net48_Contains_NoDiagnostic()
        {
            const string code = """
                                using System;

                                class C
                                {
                                    void M(string s)
                                    {
                                        s.ToUpperInvariant().Contains("ABC");
                                    }
                                }
                                """;

            return new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net48.Default,
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        [Theory, WorkItem(7053, "https://github.com/dotnet/roslyn-analyzers/issues/7053")]
        [InlineData("StartsWith")]
        [InlineData("IndexOf")]
        public Task Net48_Diagnostic(string method)
        {
            var code = $$"""
                         using System;

                         class C
                         {
                             void M(string s)
                             {
                                 [|s.ToUpperInvariant().{{method}}("ABC")|];
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;

                              class C
                              {
                                  void M(string s)
                                  {
                                      s.{{method}}("ABC", StringComparison.InvariantCultureIgnoreCase);
                                  }
                              }
                              """;

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net48.Default,
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        private async Task VerifyNoDiagnosticCSharpAsync(string originalSource)
        {
            VerifyCS.Test test = new()
            {
                TestCode = originalSource,
                FixedCode = originalSource
            };

            await test.RunAsync();
        }

        private async Task VerifyFixCSharpAsync(string originalSource, string fixedSource)
        {
            VerifyCS.Test test = new()
            {
                TestCode = originalSource,
                FixedCode = fixedSource,
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            };

            await test.RunAsync();
        }
    }
}