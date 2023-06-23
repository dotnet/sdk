// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.RecommendCaseInsensitiveStringComparisonAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicRecommendCaseInsensitiveStringComparisonFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class RecommendCaseInsensitiveStringComparison_VisualBasic_Tests : RecommendCaseInsensitiveStringComparison_Base_Tests
    {
        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedNamedData))]
        public async Task Diagnostic_Assign(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        Dim r As Integer = [|{diagnosedLine}|]
        Return r
    End Function
End Class
";
            string fixedCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        Dim r As Integer = {fixedLine}
        Return r
    End Function
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedNamedData))]
        public async Task Diagnostic_Return(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        Return [|{diagnosedLine}|]
    End Function
End Class
";
            string fixedCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        Return {fixedLine}
    End Function
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedWithEqualsToData))]
        [MemberData(nameof(DiagnosedAndFixedWithEqualsToInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedWithEqualsToNamedData))]
        public async Task Diagnostic_If(string diagnosedLine, string fixedLine, string equalsTo)
        {
            if (equalsTo == " == -1")
            {
                equalsTo = " = -1"; // VB syntax
            }

            string originalCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        If [|{diagnosedLine}|]{equalsTo} Then
            Return 5
        End If
        Return 4
    End Function
End Class
";
            string fixedCode = $@"Imports System
Class C
    Private Function M() As Integer
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        If {fixedLine}{equalsTo} Then
            Return 5
        End If
        Return 4
    End Function
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedData))]
        [MemberData(nameof(DiagnosedAndFixedInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedNamedData))]
        public async Task Diagnostic_IgnoreResult(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Private Sub M()
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        [|{diagnosedLine}|]
    End Sub
End Class
";
            string fixedCode = $@"Imports System
Class C
    Private Sub M()
        Dim a As String = ""aBc""
        Dim b As String = ""bc""
        {fixedLine}
    End Sub
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedStringLiteralsData))]
        [MemberData(nameof(DiagnosedAndFixedStringLiteralsInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedStringLiteralsNamedData))]
        public async Task Diagnostic_StringLiterals_Return(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Private Function M() As Integer
        Return [|{diagnosedLine}|]
    End Function
End Class
";
            string fixedCode = $@"Imports System
Class C
    Private Function M() As Integer
        Return {fixedLine}
    End Function
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedStringReturningMethodsData))]
        [MemberData(nameof(DiagnosedAndFixedStringReturningMethodsInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedStringReturningMethodsNamedData))]
        public async Task Diagnostic_StringReturningMethods_Discard(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Public Function GetStringA() As String
        Return ""aBc""
    End Function
    Public Function GetStringB() As String
        Return ""DeF""
    End Function
    Public Sub M()
        [|{diagnosedLine}|]
    End Sub
End Class
";
            string fixedCode = $@"Imports System
Class C
    Public Function GetStringA() As String
        Return ""aBc""
    End Function
    Public Function GetStringB() As String
        Return ""DeF""
    End Function
    Public Sub M()
        {fixedLine}
    End Sub
End Class
";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosedAndFixedParenthesizedData))]
        [MemberData(nameof(DiagnosedAndFixedParenthesizedInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedParenthesizedNamedData))]
        [MemberData(nameof(VisualBasicDiagnosedAndFixedParenthesizedNamedInvertedData))]
        public async Task Diagnostic_Parenthesized_ReturnCastedToString(string diagnosedLine, string fixedLine)
        {
            string originalCode = $@"Imports System
Class C
    Public Function GetString() As String
        Return ""AbC""
    End Function
    Public Function M() As Object
        Return ([|{diagnosedLine}|]).ToString()
    End Function
End Class";
            string fixedCode = $@"Imports System
Class C
    Public Function GetString() As String
        Return ""AbC""
    End Function
    Public Function M() As Object
        Return ({fixedLine}).ToString()
    End Function
End Class";
            await VerifyFixVisualBasicAsync(originalCode, fixedCode);
        }

        [Theory]
        [MemberData(nameof(NoDiagnosticData))]
        [InlineData("\"aBc\".CompareTo(Nothing)")]
        [InlineData("\"aBc\".ToUpperInvariant().CompareTo(CObj(Nothing))")]
        [InlineData("\"aBc\".CompareTo(value:=CObj(1))")]
        [InlineData("\"aBc\".CompareTo(strB:=\"cDe\")")]
        public async Task NoDiagnostic_All(string ignoredLine)
        {
            string originalCode = $@"Imports System
Class C
    Public Function M() As Object
        Dim ch As Char = ""c""c
        Dim obj As Object = 3
        Return {ignoredLine}
    End Function
End Class";

            await VerifyNoDiagnosticVisualBasicAsync(originalCode);
        }

        [Theory]
        [MemberData(nameof(DiagnosticNoFixCompareToData))]
        [MemberData(nameof(DiagnosticNoFixCompareToInvertedData))]
        [MemberData(nameof(VisualBasicDiagnosticNoFixCompareToNamedData))]
        public async Task Diagnostic_NoFix_CompareTo(string diagnosedLine)
        {
            string originalCode = $@"Imports System
Class C
    Public Function GetStringA() As String
        Return ""aBc""
    End Function
    Public Function GetStringB() As String
        Return ""cDe""
    End Function
    Public Function M() As Integer
        Dim a As String = ""AbC""
        Dim b As String = ""CdE""
        Return [|{diagnosedLine}|]
    End Function
End Class";
            await VerifyDiagnosticOnlyVisualBasicAsync(originalCode);
        }

        private async Task VerifyNoDiagnosticVisualBasicAsync(string originalSource)
        {
            VerifyVB.Test test = new()
            {
                TestCode = originalSource,
                FixedCode = originalSource
            };

            await test.RunAsync();
        }

        private async Task VerifyDiagnosticOnlyVisualBasicAsync(string originalSource)
        {
            VerifyVB.Test test = new()
            {
                TestCode = originalSource,
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            };

            await test.RunAsync();
        }

        private async Task VerifyFixVisualBasicAsync(string originalSource, string fixedSource)
        {
            VerifyVB.Test test = new()
            {
                TestCode = originalSource,
                FixedCode = fixedSource,
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            };

            await test.RunAsync();
        }
    }
}