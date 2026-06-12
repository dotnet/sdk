// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Usage;
using Microsoft.NetCore.VisualBasic.Analyzers.Tasks;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    using VerifyCS = CSharpCodeFixVerifier<DoNotCompareSpanToNullAnalyzer, CSharpDoNotCompareSpanToNullFixer>;
    using VerifyVB = VisualBasicCodeFixVerifier<DoNotCompareSpanToNullAnalyzer, BasicDoNotCompareSpanToNullFixer>;

    public sealed class DoNotCompareSpanToNullTests
    {
        private const string CSharpClass = """
                                           using System;
                                           using System.Diagnostics;

                                           public class Test
                                           {{
                                               public void Run({0} span)
                                               {{
                                                   {1}
                                               }}
                                           }}
                                           """;

        private const string VbClass = """
                                       Imports System
                                       Imports System.Diagnostics

                                       Public Class Test
                                           <Obsolete>
                                           Public Sub Run(span As {0})
                                               {1}
                                           End Sub
                                       End Class
                                       """;

        private static readonly DiagnosticResult DoNotCompareToNullResult = new DiagnosticResult(DoNotCompareSpanToNullAnalyzer.DoNotCompareSpanToNullRule).WithLocation(0);
        private static readonly DiagnosticResult DoNotCompareToDefaultResult = new DiagnosticResult(DoNotCompareSpanToNullAnalyzer.DoNotCompareSpanToDefaultRule).WithLocation(0);

        [Fact]
        public async Task CheckInIf_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("if ({|#0:span == null|}) {}", "if (span.IsEmpty) {}");
            await VerifyCsharpCompareToDefaultAsync("if ({|#0:span == default|}) {}", "if (span.IsEmpty) {}");

            await VerifyVisualBasicAsync("If {|#0:span = Nothing|} Then\nEnd If", "If span.IsEmpty Then\nEnd If");
        }

        [Fact]
        public async Task NegatedCheckInIf_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("if ({|#0:span != null|}) {}", "if (!span.IsEmpty) {}");
            await VerifyCsharpCompareToDefaultAsync("if ({|#0:span != default|}) {}", "if (!span.IsEmpty) {}");

            await VerifyVisualBasicAsync("If {|#0:span <> Nothing|} Then\nEnd If", "If Not span.IsEmpty Then\nEnd If");
        }

        [Fact]
        public async Task BooleanDeclaration_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("var x = {|#0:span == null|};", "var x = span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:span == default|};", "var x = span.IsEmpty;");

            await VerifyVisualBasicAsync("Dim x = {|#0:span = Nothing|}", "Dim x = span.IsEmpty");
        }

        [Fact]
        public async Task WhenComparisonIsUsedAsArgument_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("Debug.Assert({|#0:span == null|});", "Debug.Assert(span.IsEmpty);");
            await VerifyCsharpCompareToDefaultAsync("Debug.Assert({|#0:span == default|});", "Debug.Assert(span.IsEmpty);");
            await VerifyCsharpCompareToNullAsync("Debug.Assert({|#0:span != null|});", "Debug.Assert(!span.IsEmpty);");
            await VerifyCsharpCompareToDefaultAsync("Debug.Assert({|#0:span != default|});", "Debug.Assert(!span.IsEmpty);");

            await VerifyVisualBasicAsync("Debug.Assert({|#0:span = Nothing|})", "Debug.Assert(span.IsEmpty)");
            await VerifyVisualBasicAsync("Debug.Assert({|#0:span <> Nothing|})", "Debug.Assert(Not span.IsEmpty)");
        }

        [Fact]
        public async Task CompareWithDefault_Diagnostic()
        {
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:span == default|};", "var x = span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:span == default(Span<int>)|};", "var x = span.IsEmpty;");
        }

        [Fact]
        public async Task IsEmpty_NoDiagnostic()
        {
            await VerifyNoDiagnosticCsharpAsync("var x = span.IsEmpty;");

            await VerifyNoDiagnosticVisualBasicAsync("Dim x = span.IsEmpty");
        }

        [Theory]
        [InlineData("Span<int>", "Span(Of Int32)")]
        [InlineData("ReadOnlySpan<int>", "ReadOnlySpan(Of Int32)")]
        public async Task CompareToOtherSpan_NoDiagnostic(string csType, string vbType)
        {
            var csharpCode = $"""
                              {csType} otherSpan = stackalloc int[0];
                              var x = span == otherSpan;
                              """;
            var vbCode = $"""
                          Dim otherSpan As {vbType} = Nothing
                          Dim x = span = otherSpan
                          """;
            await VerifyNoDiagnosticCsharpAsync(csharpCode);
            await VerifyNoDiagnosticVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task CheckOnLeftSide_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("var x = {|#0:null == span|};", "var x = span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:default == span|};", "var x = span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:default(Span<int>) == span|};", "var x = span.IsEmpty;");

            await VerifyVisualBasicAsync("Dim x = {|#0:Nothing = span|}", "Dim x = span.IsEmpty");
        }

        [Fact]
        public async Task NegatedCheckOnLeftSide_Diagnostic()
        {
            await VerifyCsharpCompareToNullAsync("var x = {|#0:null != span|};", "var x = !span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:default != span|};", "var x = !span.IsEmpty;");
            await VerifyCsharpCompareToDefaultAsync("var x = {|#0:default(Span<int>) != span|};", "var x = !span.IsEmpty;");

            await VerifyVisualBasicAsync("Dim x = {|#0:Nothing <> span|}", "Dim x = Not span.IsEmpty");
        }

        [Theory]
        [CombinatorialData]
        public Task NonSpanNullCheck_NoDiagnostic([CombinatorialValues("string", "HttpClient", "IDictionary<string, int>", "IEnumerable<string>")] string type, [CombinatorialValues("==", "!=", "is", "is not")] string comparison)
        {
            var code = $$"""
                         using System.Collections.Generic;
                         using System.Net.Http;

                         public class Test
                         {
                             public void Run({{type}} x)
                             {
                                 var y = x {{comparison}} null;
                             }
                         }
                         """;

            return new VerifyCS.Test
            {
                TestCode = code,
                LanguageVersion = LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public Task Vb_NonSpanNullCheck_NoDiagnostic([CombinatorialValues("String", "HttpClient", "IDictionary(Of String, Int32)", "IEnumerable(Of String)")] string type, [CombinatorialValues("Is", "IsNot")] string comparison)
        {
            var code = $"""
                       Imports System
                       Imports System.Collections.Generic
                       Imports System.Net.Http

                       Public Class Test
                           Public Sub Run(x As {type})
                               Dim y = x {comparison} Nothing
                           End Sub
                       End Class
                       """;

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        private static async Task VerifyNoDiagnosticCsharpAsync(string code)
        {
            var spanCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "Span<int>", code);
            var rosCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "ReadOnlySpan<int>", code);

            await VerifyCS.VerifyAnalyzerAsync(spanCode);
            await VerifyCS.VerifyAnalyzerAsync(rosCode);
        }

        private static async Task VerifyCsharpCompareToNullAsync(string code, string fixedCode)
        {
            var spanCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "Span<int>", code);
            var fixedSpanCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "Span<int>", fixedCode);

            var rosCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "ReadOnlySpan<int>", code);
            var fixedRosCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "ReadOnlySpan<int>", fixedCode);

            await new VerifyCS.Test
            {
                TestCode = spanCode,
                FixedCode = fixedSpanCode,
                ExpectedDiagnostics = { DoNotCompareToNullResult }
            }.RunAsync();

            await new VerifyCS.Test
            {
                TestCode = rosCode,
                FixedCode = fixedRosCode,
                ExpectedDiagnostics = { DoNotCompareToNullResult }
            }.RunAsync();
        }

        private static async Task VerifyCsharpCompareToDefaultAsync(string code, string fixedCode)
        {
            var spanCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "Span<int>", code);
            var fixedSpanCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "Span<int>", fixedCode);

            var rosCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "ReadOnlySpan<int>", code);
            var fixedRosCode = string.Format(CultureInfo.InvariantCulture, CSharpClass, "ReadOnlySpan<int>", fixedCode);

            await new VerifyCS.Test
            {
                TestCode = spanCode,
                FixedCode = fixedSpanCode,
                ExpectedDiagnostics = { DoNotCompareToDefaultResult }
            }.RunAsync();

            await new VerifyCS.Test
            {
                TestCode = rosCode,
                FixedCode = fixedRosCode,
                ExpectedDiagnostics = { DoNotCompareToDefaultResult }
            }.RunAsync();
        }

        private static async Task VerifyNoDiagnosticVisualBasicAsync(string code)
        {
            var spanCode = string.Format(CultureInfo.InvariantCulture, VbClass, "Span(Of Int32)", code);
            var rosCode = string.Format(CultureInfo.InvariantCulture, VbClass, "ReadOnlySpan(Of Int32)", code);

            await VerifyVB.VerifyAnalyzerAsync(spanCode);
            await VerifyVB.VerifyAnalyzerAsync(rosCode);
        }

        private static async Task VerifyVisualBasicAsync(string code, string fixedCode)
        {
            var spanCode = string.Format(CultureInfo.InvariantCulture, VbClass, "Span(Of Int32)", code);
            var fixedSpanCode = string.Format(CultureInfo.InvariantCulture, VbClass, "Span(Of Int32)", fixedCode);

            var rosCode = string.Format(CultureInfo.InvariantCulture, VbClass, "ReadOnlySpan(Of Int32)", code);
            var fixedRosCode = string.Format(CultureInfo.InvariantCulture, VbClass, "ReadOnlySpan(Of Int32)", fixedCode);

            await new VerifyVB.Test
            {
                TestCode = spanCode,
                FixedCode = fixedSpanCode,
                ExpectedDiagnostics = { DoNotCompareToNullResult }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = rosCode,
                FixedCode = fixedRosCode,
                ExpectedDiagnostics = { DoNotCompareToNullResult }
            }.RunAsync();
        }
    }
}