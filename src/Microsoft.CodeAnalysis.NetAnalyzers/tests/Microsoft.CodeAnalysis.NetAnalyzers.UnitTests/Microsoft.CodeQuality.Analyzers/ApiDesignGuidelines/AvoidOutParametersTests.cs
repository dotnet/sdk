// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidOutParameters,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidOutParameters,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class AvoidOutParametersTests
    {
        [Fact]
        public async Task SimpleCases_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M1(out C c)
    {
        c = null;
    }

    public void M2(string s, out C c)
    {
        c = null;
    }

    public void M3(string s1, out C c, string s2)
    {
        c = null;
    }

    public void M4(out C c, out string s1)
    {
        c = null;
        s1 = null;
    }
}",
                GetCSharpExpectedResult(4, 17),
                GetCSharpExpectedResult(9, 17),
                GetCSharpExpectedResult(14, 17),
                GetCSharpExpectedResult(19, 17));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub M1(<Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Sub M2(ByVal s As String, <Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Sub M3(ByVal s1 As String, <Out> ByRef c As C, ByVal s2 As String)
        c = Nothing
    End Sub

    Public Sub M4(<Out> ByRef c As C, <Out> ByRef s1 As String)
        c = Nothing
        s1 = Nothing
    End Sub
End Class",
                GetBasicExpectedResult(5, 16),
                GetBasicExpectedResult(9, 16),
                GetBasicExpectedResult(13, 16),
                GetBasicExpectedResult(17, 16));
        }

        [Fact]
        public async Task MultipleOut_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M1(out C c, out string s1)
    {
        c = null;
        s1 = null;
    }

    public void M2(out C c, string s1, out string s2)
    {
        c = null;
        s2 = null;
    }
}",
                GetCSharpExpectedResult(4, 17),
                GetCSharpExpectedResult(10, 17));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub M1(<Out> ByRef c As C, <Out> ByRef s1 As String)
        c = Nothing
        s1 = Nothing
    End Sub

    Public Sub M2(<Out> ByRef c As C, ByVal s1 As String, <Out> ByRef s2 As String)
        c = Nothing
        s2 = Nothing
    End Sub
End Class",
                GetBasicExpectedResult(5, 16),
                GetBasicExpectedResult(10, 16));
        }

        [Fact]
        public async Task OutAndRef_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M1(out C c, ref string s1)
    {
        c = null;
    }

    public void M2(out C c, string s1, out string s2, ref string s3)
    {
        c = null;
        s2 = null;
    }
}",
                GetCSharpExpectedResult(4, 17),
                GetCSharpExpectedResult(9, 17));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub M1(<Out> ByRef c As C, ByRef s1 As String)
        c = Nothing
    End Sub

    Public Sub M2(<Out> ByRef c As C, ByVal s1 As String, <Out> ByRef s2 As String, ByRef s3 As String)
        c = Nothing
        s2 = Nothing
    End Sub
End Class",
                GetBasicExpectedResult(5, 16),
                GetBasicExpectedResult(9, 16));
        }

        [Fact]
        public async Task Ref_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(ref string s1)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub M(ByRef s1 As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task TryPattern_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    private Dictionary<string, C> dict = new Dictionary<string, C>();

    public static bool TryParse(string s, out C result)
    {
        result = null;
        return false;
    }

    private bool TryGetOrAdd(string key, C valueIfNotFound, out C result)
    {
        if (dict.ContainsKey(key))
        {
            result = dict[key];
            return true;
        }

        dict[key] = valueIfNotFound;
        result = valueIfNotFound;
        return false;
    }

    public static bool Try(out C c)
    {
        c = null;
        return true;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Public Class C
    Private dict = New Dictionary(Of String, C)

    Public Shared Function TryParse(ByVal s As String, <Out> ByRef result As C) As Boolean
        result = Nothing
        Return False
    End Function

    Private Function TryGetOrAdd(ByVal key As String, ByVal valueIfNotFound As C, <Out> ByRef result As C) As Boolean
        If dict.ContainsKey(key) Then
            result = dict(key)
            Return True
        End If
        dict(key) = valueIfNotFound
        result = valueIfNotFound
        Return False
    End Function

    Public Shared Function [Try](<Out> ByRef c As C) As Boolean
        c = Nothing
        Return True
    End Function
End Class");
        }

        [Fact]
        public async Task InvalidTryPattern_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void TryM1(out C c)
    {
        c = null;
    }

    public bool TryM2(out C c, string s)
    {
        c = null;
        return false;
    }

    public static bool TRY_PARSE(string s, out C c)
    {
        c = null;
        return true;
    }
}",
                GetCSharpExpectedResult(4, 17),
                GetCSharpExpectedResult(9, 17),
                GetCSharpExpectedResult(15, 24));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub TryM1(<Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Function TryM2(<Out> ByRef c As C, ByVal s As String) As Boolean
        c = Nothing
        Return False
    End Function

    Public Shared Function TRY_PARSE(ByVal s As String, <Out> ByRef c As C) As Boolean
        c = Nothing
        Return True
    End Function
End Class",
                GetCSharpExpectedResult(5, 16),
                GetCSharpExpectedResult(9, 21),
                GetCSharpExpectedResult(14, 28));
        }

        private static DiagnosticResult GetCSharpExpectedResult(int line, int col) =>
            VerifyCS.Diagnostic().WithLocation(line, col);

        private static DiagnosticResult GetBasicExpectedResult(int line, int col) =>
            VerifyVB.Diagnostic().WithLocation(line, col);
    }
}