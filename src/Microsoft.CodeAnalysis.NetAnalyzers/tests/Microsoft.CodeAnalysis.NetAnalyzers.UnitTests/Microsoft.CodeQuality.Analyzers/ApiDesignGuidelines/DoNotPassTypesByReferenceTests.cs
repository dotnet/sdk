// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPassTypesByReference,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPassTypesByReference,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotPassTypesByReferenceTests
    {
        [Fact]
        public async Task CA1045_RefParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public void Method1(ref string s, ref object o)
    {
    }
}",
                GetCA1045CSharpResultAt(4, 36, "s"),
                GetCA1045CSharpResultAt(4, 50, "o"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Public Sub Method1(ByRef s As String, ByRef o As Object)
    End Sub
End Class",
                GetCA1045BasicResultAt(3, 30, "s"),
                GetCA1045BasicResultAt(3, 49, "o"));
        }

        [Fact]
        public async Task CA1045_MethodIsNotPublic_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private void Method1(ref string s)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Private Sub Method1(ByRef s As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1045_MethodIsOverride_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class BaseClass
{
    public virtual void Method1(ref string s) // issue here...
    {
    }
}

public class Class1 : BaseClass
{
    public override void Method1(ref string s) // ... but not here
    {
    }
}",
                GetCA1045CSharpResultAt(4, 44, "s"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class BaseClass
    Public Overridable Sub Method1(ByRef s As String) ' issue here...
    End Sub
End Class

Public Class Class1
    Inherits BaseClass

    Public Overrides Sub Method1(ByRef s As String) ' ... but not here
    End Sub
End Class
",
                GetCA1045BasicResultAt(3, 42, "s"));
        }

        [Fact]
        public async Task CA1045_MethodIsInterfaceImplementation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface Interface1
{
    void Method1(ref string s); // issue here...
}

public class Class1 : Interface1
{
    public void Method1(ref string s) // ... but not here
    {
    }
}",
                GetCA1045CSharpResultAt(4, 29, "s"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface Interface1
    Sub Method1(ByRef s As String) ' issue here...
End Interface

Public Class Class1
    Implements Interface1

    Public Sub Method1(ByRef s As String) Implements Interface1.Method1 ' ... but not here
    End Sub
End Class",
                GetCA1045BasicResultAt(3, 23, "s"));
        }

        [Fact]
        public async Task CA1045_VBNET_ByRefOutParameter_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class Class1
    Public Sub Method1(s As String, <Out> ByRef c As Class1)
        c = Nothing
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1045_PInvokeMethod_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

public class Class1
{
    [DllImport(""Advapi32.dll"", CharSet=CharSet.Auto)]
    public static extern Boolean FileEncryptionStatus(String filename, ref UInt32 status);
}",
                GetCA1045CSharpResultAt(8, 83, "status"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class Class1
    <DllImport(""Advapi32.dll"", CharSet:=CharSet.Auto)>
    Public Shared Function FileEncryptionStatus(ByVal filename As String, ByRef status As UInteger) As Boolean
    End Function
End Class",
                GetCA1045BasicResultAt(6, 81, "status"));
        }

        private static DiagnosticResult GetCA1045CSharpResultAt(int line, int column, string parameterName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(parameterName);

        private static DiagnosticResult GetCA1045BasicResultAt(int line, int column, string parameterName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(parameterName);
    }
}
