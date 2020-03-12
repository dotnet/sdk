// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class SpecifyStringComparisonTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new SpecifyStringComparisonAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SpecifyStringComparisonAnalyzer();
        }

        [Fact]
        public void CA1307_StringCompareTests_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringCompare()
    {
        string strA = """";
        string strB = """";
        var x1 = String.Compare(strA, strB);
        var x2 = String.Compare(strA, strB, true);
        var x3 = String.Compare(strA, 0, strB, 0, 1);
        var x4 = String.Compare(strA, 0, strB, 0, 1, true);
        return 0;
    }
}",
GetCA1307CSharpResultsAt(11, 18, "string.Compare(string, string)",
                                 "StringComparisonTests.StringCompare()",
                                 "string.Compare(string, string, System.StringComparison)"),
GetCA1307CSharpResultsAt(12, 18, "string.Compare(string, string, bool)",
                                 "StringComparisonTests.StringCompare()",
                                 "string.Compare(string, string, System.StringComparison)"),
GetCA1307CSharpResultsAt(13, 18, "string.Compare(string, int, string, int, int)",
                                 "StringComparisonTests.StringCompare()",
                                 "string.Compare(string, int, string, int, int, System.StringComparison)"),
GetCA1307CSharpResultsAt(14, 18, "string.Compare(string, int, string, int, int, bool)",
                                 "StringComparisonTests.StringCompare()",
                                 "string.Compare(string, int, string, int, int, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringWithTests_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public bool StringWith()
    {
        string strA = """";
        string strB = """";
        var x = strA.EndsWith(strB);
        return strA.StartsWith(strB);
    }
}",
GetCA1307CSharpResultsAt(11, 17, "string.EndsWith(string)",
                                 "StringComparisonTests.StringWith()",
                                 "string.EndsWith(string, System.StringComparison)"),
GetCA1307CSharpResultsAt(12, 16, "string.StartsWith(string)",
                                 "StringComparisonTests.StringWith()",
                                 "string.StartsWith(string, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringIndexOfTests_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringIndexOf()
    {
        string strA = """";
        var x1 = strA.IndexOf("""");
        var x2 = strA.IndexOf("""", 0);
        return strA.IndexOf("""", 0, 1);
    }
}",
GetCA1307CSharpResultsAt(10, 18, "string.IndexOf(string)",
                                "StringComparisonTests.StringIndexOf()",
                                "string.IndexOf(string, System.StringComparison)"),
GetCA1307CSharpResultsAt(11, 18, "string.IndexOf(string, int)",
                                 "StringComparisonTests.StringIndexOf()",
                                 "string.IndexOf(string, int, System.StringComparison)"),
GetCA1307CSharpResultsAt(12, 16, "string.IndexOf(string, int, int)",
                                 "StringComparisonTests.StringIndexOf()",
                                 "string.IndexOf(string, int, int, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringCompareToTests_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringCompareTo()
    {
            string strA = """";
            string strB = """";
            var x1 = strA.CompareTo(strB);
            return """".CompareTo(new object());
    }
}",
GetCA1307CSharpResultsAt(11, 22, "string.CompareTo(string)",
                                 "StringComparisonTests.StringCompareTo()",
                                 "string.Compare(string, string, System.StringComparison)"),
GetCA1307CSharpResultsAt(12, 20, "string.CompareTo(object)",
                                 "StringComparisonTests.StringCompareTo()",
                                 "string.Compare(string, string, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_OverloadTests_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public void NonString()
    {
        DoNothing("""");
        DoNothing<string>(""""); // No diagnostics since this is generics
    }

    public void DoNothing(string str)
    {
    }

    public void DoNothing<T>(string str)
    {
    }

    public void DoNothing<T>(string str, StringComparison strCompare)
    {
    }
}",
GetCA1307CSharpResultsAt(9, 9, "StringComparisonTests.DoNothing(string)",
                               "StringComparisonTests.NonString()",
                               "StringComparisonTests.DoNothing<T>(string, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_OverloadWithMismatchRefKind_CSharp()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public void MyMethod()
    {
        M("""");
    }

    public void M(string str)
    {
    }

    public void M(string str, out StringComparison strCompare)
    {
        strCompare = StringComparison.Ordinal;
    }

    public void M(ref StringComparison strCompare, string str)
    {
        strCompare = StringComparison.Ordinal;
    }

    public void M(ref string str, StringComparison strCompare)
    {
    }
}");
        }

        [Fact]
        public void CA1307_StringCompareTests_VisualBasic()
        {
            VerifyBasic(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringCompare() As Integer
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x1 = [String].Compare(strA, strB)
        Dim x2 = [String].Compare(strA, strB, True)
        Dim x3 = [String].Compare(strA, 0, strB, 0, 1)
        Dim x4 = [String].Compare(strA, 0, strB, 0, 1, True)
        Return 0
    End Function
End Class",
GetCA1307BasicResultsAt(9, 18, "String.Compare(String, String)",
                               "StringComparisonTests.StringCompare()",
                               "String.Compare(String, String, System.StringComparison)"),
GetCA1307BasicResultsAt(10, 18, "String.Compare(String, String, Boolean)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, String, System.StringComparison)"),
GetCA1307BasicResultsAt(11, 18, "String.Compare(String, Integer, String, Integer, Integer)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, Integer, String, Integer, Integer, System.StringComparison)"),
GetCA1307BasicResultsAt(12, 18, "String.Compare(String, Integer, String, Integer, Integer, Boolean)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, Integer, String, Integer, Integer, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringWithTests_VisualBasic()
        {
            VerifyBasic(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringWith() As Boolean
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x = strA.EndsWith(strB)
        Return strA.StartsWith(strB)
    End Function
End Class",
GetCA1307BasicResultsAt(9, 17, "String.EndsWith(String)",
                               "StringComparisonTests.StringWith()",
                               "String.EndsWith(String, System.StringComparison)"),
GetCA1307BasicResultsAt(10, 16, "String.StartsWith(String)",
                                "StringComparisonTests.StringWith()",
                                "String.StartsWith(String, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringIndexOfTests_VisualBasic()
        {
            VerifyBasic(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringIndexOf() As Integer
        Dim strA As String = """"
        Dim x1 = strA.IndexOf("""")
        Dim x2 = strA.IndexOf("""", 0)
        Return strA.IndexOf("""", 0, 1)
    End Function
End Class",
GetCA1307BasicResultsAt(8, 18, "String.IndexOf(String)",
                               "StringComparisonTests.StringIndexOf()",
                               "String.IndexOf(String, System.StringComparison)"),
GetCA1307BasicResultsAt(9, 18, "String.IndexOf(String, Integer)",
                                "StringComparisonTests.StringIndexOf()",
                                "String.IndexOf(String, Integer, System.StringComparison)"),
GetCA1307BasicResultsAt(10, 16, "String.IndexOf(String, Integer, Integer)",
                                "StringComparisonTests.StringIndexOf()",
                                "String.IndexOf(String, Integer, Integer, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_StringCompareToTests_VisualBasic()
        {
            VerifyBasic(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringCompareTo() As Integer
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x1 = strA.CompareTo(strB)
        Return """".CompareTo(New Object())
    End Function
End Class",
GetCA1307BasicResultsAt(9, 18, "String.CompareTo(String)",
                               "StringComparisonTests.StringCompareTo()",
                               "String.Compare(String, String, System.StringComparison)"),
GetCA1307BasicResultsAt(10, 16, "String.CompareTo(Object)",
                                "StringComparisonTests.StringCompareTo()",
                                "String.Compare(String, String, System.StringComparison)"));
        }

        [Fact]
        public void CA1307_OverloadTests_VisualBasic()
        {
            VerifyBasic(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Sub NonString()
        DoNothing("")
        ' No diagnostics since this is generics
        DoNothing(Of String)("")
    End Sub

    Public Sub DoNothing(str As String)
    End Sub

    Public Sub DoNothing(Of T)(str As String)
    End Sub

    Public Sub DoNothing(Of T)(str As String, strCompare As StringComparison)
    End Sub
End Class",
GetCA1307BasicResultsAt(7, 9, "StringComparisonTests.DoNothing(String)",
                              "StringComparisonTests.NonString()",
                              "StringComparisonTests.DoNothing(Of T)(String, System.StringComparison)"));
        }

        private DiagnosticResult GetCA1307CSharpResultsAt(int line, int column, string arg1, string arg2, string arg3)
        {
            return GetCSharpResultAt(
                line,
                column,
                SpecifyStringComparisonAnalyzer.Rule,
                arg1,
                arg2,
                arg3);
        }

        private DiagnosticResult GetCA1307BasicResultsAt(int line, int column, string arg1, string arg2, string arg3)
        {
            return GetBasicResultAt(
                line,
                column,
                SpecifyStringComparisonAnalyzer.Rule,
                arg1,
                arg2,
                arg3);
        }
    }
}