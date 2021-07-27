// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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

        [Fact]
        public async Task CA1045_MethodIsInterfaceImplementation_Diagnostic()
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

        [Fact]
        public async Task CA1045_OutParameter_NoDiagnostic()
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

        [Fact]
        public async Task CA1045_PInvokeMethod_Diagnostic()
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

        [Fact]
        public async Task CA1045_InParameter_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private void Method1(in Class1 c)
    {
    }
}");
        }

        [Theory]
        // General analyzer option
        [InlineData("public", "dotnet_code_quality.api_surface = public")]
        [InlineData("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("public", "dotnet_code_quality.api_surface = all")]
        [InlineData("protected", "dotnet_code_quality.api_surface = public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.api_surface = internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = private, internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = all")]
        [InlineData("private", "dotnet_code_quality.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.api_surface = private, public")]
        [InlineData("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [InlineData("internal", "dotnet_code_quality.CA1045.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1045.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1045.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1045.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOption(string accessibility, string editorConfigText)
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
            }.RunAsync();
        }

        [Theory]
        // General analyzer option
        [InlineData("Public", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = All")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = All")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [InlineData("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [InlineData("Friend", "dotnet_code_quality.CA1045.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1045.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1045.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1045.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOption(string accessibility, string editorConfigText)
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
            }.RunAsync();
        }
    }
}
