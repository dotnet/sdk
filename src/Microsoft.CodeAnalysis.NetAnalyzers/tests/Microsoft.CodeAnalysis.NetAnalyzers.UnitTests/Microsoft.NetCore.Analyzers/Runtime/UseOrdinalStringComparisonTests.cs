// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseOrdinalStringComparisonAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicUseOrdinalStringComparisonAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseOrdinalStringComparisonTests
    {
        #region Helper methods

        private DiagnosticResult CSharpResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private DiagnosticResult BasicResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        #endregion

        #region Diagnostic tests

        [Fact]
        public async Task CA1309CompareOverloadTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

class C
{
    void Method()
    {
        string a = null, b = null;
        // wrong overload
        string.Compare(a, b);
        string.Compare(a, b, true);
        string.Compare(a, b, true, default(CultureInfo));
        string.Compare(a, b, default(CultureInfo), default(CompareOptions));
        string.Compare(a, 0, b, 0, 0);
        string.Compare(a, 0, b, 0, 0, true);
        string.Compare(a, 0, b, 0, 0, true, default(CultureInfo));
        string.Compare(a, 0, b, 0, 0, default(CultureInfo), default(CompareOptions));
        System.String.Compare(a, b);
        // right overload, wrong value
        string.Compare(a, b, StringComparison.CurrentCulture);
        string.Compare(a, 0, b, 0, 0, StringComparison.CurrentCulture);
        // right overload, right value
        string.Compare(a, b, StringComparison.Ordinal);
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        string.Compare(a, 0, b, 0, 0, StringComparison.Ordinal);
        string.Compare(a, 0, b, 0, 0, StringComparison.OrdinalIgnoreCase);
    }
}
",
                CSharpResult(11, 16),
                CSharpResult(12, 16),
                CSharpResult(13, 16),
                CSharpResult(14, 16),
                CSharpResult(15, 16),
                CSharpResult(16, 16),
                CSharpResult(17, 16),
                CSharpResult(18, 16),
                CSharpResult(19, 23),
                CSharpResult(21, 30),
                CSharpResult(22, 39));
        }

        [Fact]
        public async Task CA1309CompareOverloadTestBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Class C
    Sub Method()
        Dim a As String
        Dim b As String
        Dim ci As CultureInfo
        Dim co As CompareOptions
        ' wrong overload
        String.Compare(a, b)
        String.Compare(a, b, True)
        String.Compare(a, b, True, ci)
        String.Compare(a, b, ci, co)
        String.Compare(a, 0, b, 0, 0)
        String.Compare(a, 0, b, 0, 0, True)
        String.Compare(a, 0, b, 0, 0, True, ci)
        String.Compare(a, 0, b, 0, 0, ci, co)
        System.String.Compare(a, b)
        ' right overload, wrong value
        String.Compare(a, b, StringComparison.CurrentCulture)
        String.Compare(a, 0, b, 0, 0, StringComparison.CurrentCulture)
        ' right overload, right value
        String.Compare(a, b, StringComparison.Ordinal)
        String.Compare(a, b, StringComparison.OrdinalIgnoreCase)
        String.Compare(a, 0, b, 0, 0, StringComparison.Ordinal)
        String.Compare(a, 0, b, 0, 0, StringComparison.OrdinalIgnoreCase)
    End Sub
End Class
",
                BasicResult(12, 16),
                BasicResult(13, 16),
                BasicResult(14, 16),
                BasicResult(15, 16),
                BasicResult(16, 16),
                BasicResult(17, 16),
                BasicResult(18, 16),
                BasicResult(19, 16),
                BasicResult(20, 23),
                BasicResult(22, 30),
                BasicResult(23, 39));
        }

        [Fact]
        public async Task CA1309EqualsOverloadTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null, b = null;
        // wrong overload
        string.Equals(a, b); // (string, string) is bad
        // right overload, wrong value
        string.Equals(a, b, StringComparison.CurrentCulture);
        // right overload, right value
        string.Equals(a, b, StringComparison.Ordinal);
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        string.Equals(a, 15); // this is the (object, object) overload
    }
}
",
                CSharpResult(10, 16),
                CSharpResult(12, 29));
        }

        [Fact]
        public async Task CA1309EqualsOverloadTestBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
    Sub Method()
        Dim a As String, b As String
        ' wrong overload
        String.Equals(a, b) ' (String, String) is bad
        ' right overload, wrong value
        String.Equals(a, b, StringComparison.CurrentCulture)
        ' right overload, right value
        String.Equals(a, b, StringComparison.Ordinal)
        String.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        String.Equals(a, 15) ' this is the (Object, Object) overload
    End Sub
End Class
",
                BasicResult(8, 16),
                BasicResult(10, 29));
        }

        [Fact]
        public async Task CA1309InstanceEqualsTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null, b = null;
        // wrong overload
        a.Equals(b);
        // right overload, wrong value
        a.Equals(b, StringComparison.CurrentCulture);
        // right overload, right value
        a.Equals(b, StringComparison.Ordinal);
        a.Equals(b, StringComparison.OrdinalIgnoreCase);
        a.Equals(15); // this is the (object) overload
    }
}
",
                CSharpResult(10, 11),
                CSharpResult(12, 21));
        }

        [Fact]
        public async Task CA1309InstanceEqualsTestBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
    Sub Method()
        Dim a As String, b As String
        ' wrong overload
        a.Equals(b)
        ' right overload, wrong value
        a.Equals(b, StringComparison.CurrentCulture)
        ' right overload, right value
        a.Equals(b, StringComparison.Ordinal)
        a.Equals(b, StringComparison.OrdinalIgnoreCase)
        a.Equals(15) ' this is the (Object) overload
    End Sub
End Class
",
                BasicResult(8, 11),
                BasicResult(10, 21));
        }

        [Fact]
        public async Task CA1309OperatorOverloadTestCSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null, b = null;
        if (a == b) { }
        if (a != b) { }
        if (a == null) { }
        if (null == a) { }
    }
}
");
        }

        [Fact]
        public async Task CA1309OperatorOverloadTestBasic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
    Sub Method()
        Dim a As String, b As String
        If a = b Then
        End If
        If a <> b Then
        End If
        If a = Nothing Then
        End If
        If a Is Nothing Then
        End If
        If Nothing = a Then
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1309NotReallyCompareOrEqualsTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void Method()
    {
        string s = null;

        // verify extension methods don't trigger
        if (s.Equals(1, 2, 3)) { }
        if (s.Compare(1, 2, 3)) { }

        // verify other static string methods don't trigger
        string.Format(s);

        // verify other instance string methods don't trigger
        s.EndsWith(s);
    }
}

static class Extensions
{
    public static bool Equals(this string s, int a, int b, int c) { return false; }
    public static bool Compare(this string s, int a, int b, int c) { return false; }
}
");
        }

        [Fact]
        public async Task CA1309NotReallyCompareOrEqualsTestBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.CompilerServices

Class C
    Sub Method()
        Dim s As String

        ' verify extension methods don't trigger
        If s.Equals(1, 2, 3) Then
        End If
        If s.Compare(1, 2) Then
        End If

        ' verify other static string methods don't trigger
        String.Format(s)

        ' verify other instance string methods don't trigger
        s.EndsWith(s)
    End Sub
End Class

Module Extensions
    <Extension>
    Public Function Equals(s As String, a As Integer, b As Integer, c As Integer) As Boolean
        Return False
    End Function
    <Extension()>
    Public Function Compare(s As String, a As Integer, b As Integer) As Boolean
        Return False
    End Function
End Module
");
        }

        #endregion
    }
}
