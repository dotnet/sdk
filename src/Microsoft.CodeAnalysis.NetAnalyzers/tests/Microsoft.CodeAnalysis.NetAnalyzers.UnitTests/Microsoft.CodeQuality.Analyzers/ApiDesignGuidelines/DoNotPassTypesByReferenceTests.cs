// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPassTypesByReference,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPassTypesByReference,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class DoNotPassTypesByReferenceTests
    {
        [TestMethod]
        public async Task CA1045_RefParameters_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public void Method1(ref string s, ref object o)
    {
    }
}",
                VerifyCS.Diagnostic().WithSpan(4, 36, 4, 37).WithArguments("s"),
                VerifyCS.Diagnostic().WithSpan(4, 50, 4, 51).WithArguments("o"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Public Sub Method1(ByRef s As String, ByRef o As Object)
    End Sub
End Class",
                VerifyVB.Diagnostic().WithSpan(3, 30, 3, 31).WithArguments("s"),
                VerifyVB.Diagnostic().WithSpan(3, 49, 3, 50).WithArguments("o"));
        }

        [TestMethod]
        public async Task CA1045_MethodIsNotPublic_NoDiagnosticAsync()
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

        [TestMethod]
        public async Task CA1045_MethodIsOverride_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class BaseClass
{
    public virtual void Method1(ref string [|s|]) // issue here...
    {
    }
}

public class Class1 : BaseClass
{
    public override void Method1(ref string s) // ... but not here
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class BaseClass
    Public Overridable Sub Method1(ByRef [|s|] As String) ' issue here...
    End Sub
End Class

Public Class Class1
    Inherits BaseClass

    Public Overrides Sub Method1(ByRef s As String) ' ... but not here
    End Sub
End Class
");
        }

        [TestMethod]
        public async Task CA1045_MethodIsInterfaceImplementation_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface Interface1
{
    void Method1(ref string [|s|]); // issue here...
}

public class Class1 : Interface1
{
    public void Method1(ref string s) // ... but not here
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface Interface1
    Sub Method1(ByRef [|s|] As String) ' issue here...
End Interface

Public Class Class1
    Implements Interface1

    Public Sub Method1(ByRef s As String) Implements Interface1.Method1 ' ... but not here
    End Sub
End Class");
        }

        [TestMethod]
        public async Task CA1045_OutParameter_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private void Method1(out string s)
    {
        s = string.Empty;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class Class1
    Public Sub Method1(s As String, <Out> ByRef c1 As Class1, ByRef [|c2|] As Class1)
        c1 = Nothing
    End Sub
End Class");
        }

        [TestMethod]
        public async Task CA1045_PInvokeMethod_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

public class Class1
{
    [DllImport(""Advapi32.dll"", CharSet=CharSet.Auto)]
    public static extern Boolean FileEncryptionStatus(String filename, ref UInt32 [|status|]);
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class Class1
    <DllImport(""Advapi32.dll"", CharSet:=CharSet.Auto)>
    Public Shared Function FileEncryptionStatus(ByVal filename As String, ByRef [|status|] As UInteger) As Boolean
    End Function
End Class");
        }

        [TestMethod]
        public async Task CA1045_InParameter_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private void Method1(in Class1 c)
    {
    }
}");
        }

        [TestMethod]
        // General analyzer option
        [DataRow("public", "dotnet_code_quality.api_surface = public")]
        [DataRow("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("public", "dotnet_code_quality.api_surface = all")]
        [DataRow("protected", "dotnet_code_quality.api_surface = public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.api_surface = internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = private, internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = all")]
        [DataRow("private", "dotnet_code_quality.api_surface = private")]
        [DataRow("private", "dotnet_code_quality.api_surface = private, public")]
        [DataRow("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [DataRow("internal", "dotnet_code_quality.CA1045.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1045.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1045.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1045.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void M(ref string [|s|]) {{ }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        // General analyzer option
        [DataRow("Public", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = All")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = All")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [DataRow("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [DataRow("Friend", "dotnet_code_quality.CA1045.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1045.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1045.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1045.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Sub M(ByRef [|s|] As String)
    End Sub
End Class",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync(CancellationToken.None);
        }
    }
}
