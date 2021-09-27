// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareVisibleInstanceFieldsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareVisibleInstanceFieldsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDeclareVisibleInstanceFieldsTests
    {
        [Fact]
        public async Task CSharp_PublicVariable_PublicContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public string field; 
}", GetCSharpResultAt(4, 19));
        }

        [Fact]
        public async Task VisualBasic_PublicVariable_PublicContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public field As System.String
End Class", GetBasicResultAt(3, 12));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_PublicVariable_InternalContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class A
{
    public string field; 

    public class B
    {
        public string field; 
    }
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_PublicVariable_InternalContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class A
    Public field As System.String

    Public Class B
        Public field As System.String
    End Class
End Class
");
        }

        [Fact]
        public async Task CSharp_DefaultVisibilityAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    string field; 
}");
        }

        [Fact]
        public async Task VisualBasic_DefaultVisibilityAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Dim field As System.String
End Class");
        }

        [Fact]
        public async Task CSharp_PublicStaticVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static string field; 
}");
        }

        [Fact]
        public async Task VisualBasic_PublicStaticVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Shared field as System.String
End Class");
        }

        [Fact]
        public async Task CSharp_PublicStaticReadonlyVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static readonly string field; 
}");
        }

        [Fact]
        public async Task VisualBasic_PublicStaticReadonlyVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Shared ReadOnly field as System.String
End Class");
        }

        [Fact]
        public async Task CSharp_PublicConstVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public const string field = ""X"";
}");
        }

        [Fact]
        public async Task VisualBasic_PublicConstVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Const field as System.String = ""X""
End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedVariable_PublicContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    protected string field;
}", GetCSharpResultAt(4, 22));
        }

        [Fact]
        public async Task VisualBasic_ProtectedVariable_PublicContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Protected field As System.String
End Class", GetBasicResultAt(3, 15));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_ProtectedVariable_InternalContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        internal class A
        {
            protected string field; 

            public class B
            {
                protected string field; 
            }
        }");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_ProtectedVariable_InternalContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Friend Class A
            Protected field As System.String

            Public Class B
                Protected field As System.String
            End Class
        End Class
        ");
        }

        [Fact]
        public async Task CSharp_ProtectedStaticVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected static string field; 
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedStaticVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Shared field as System.String
        End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedStaticReadonlyVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected static readonly string field; 
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedStaticReadonlyVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Shared ReadOnly field as System.String
        End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedConstVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected const string field = ""X"";
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedConstVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Const field as System.String = ""X""
        End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedInternalVariable_PublicContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    protected internal string field;
}", GetCSharpResultAt(4, 31));
        }

        [Fact]
        public async Task VisualBasic_ProtectedFriendVariable_PublicContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Protected Friend field As System.String
End Class", GetBasicResultAt(3, 22));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_ProtectedInternalVariable_InternalContainingTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        internal class A
        {
            protected internal string field; 

            public class B
            {
                protected internal string field; 
            }
        }");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_ProtectedFriendVariable_InternalContainingTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Friend Class A
            Protected Friend field As System.String

            Public Class B
                Protected Friend field As System.String
            End Class
        End Class
        ");
        }

        [Fact]
        public async Task CSharp_ProtectedInternalStaticVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected internal static string field; 
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedFriendStaticVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Friend Shared field as System.String
        End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedInternalStaticReadonlyVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected internal static readonly string field; 
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedFriendStaticReadonlyVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Friend Shared ReadOnly field as System.String
        End Class");
        }

        [Fact]
        public async Task CSharp_ProtectedInternalConstVariableAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
        public class A
        {
            protected internal const string field = ""X"";
        }");
        }

        [Fact]
        public async Task VisualBasic_ProtectedFriendConstVariableAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
        Public Class A
            Protected Friend Const field as System.String = ""X""
        End Class");
        }

        [Fact, WorkItem(4149, "https://github.com/dotnet/roslyn-analyzers/issues/4149")]
        public async Task TypeWithStructLayoutAttribute_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public class C
{
    public int F;
}

[StructLayout(LayoutKind.Sequential)]
public struct S
{
    public int F;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

<StructLayout(LayoutKind.Sequential)>
Public Class C
    Public F As Integer
End Class

<StructLayout(LayoutKind.Sequential)>
Public Structure S
    Public F As Integer
End Structure");
        }

        [Theory, WorkItem(4149, "https://github.com/dotnet/roslyn-analyzers/issues/4149")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.exclude_structs = true")]
        [InlineData("dotnet_code_quality.exclude_structs = false")]
        [InlineData("dotnet_code_quality.CA1051.exclude_structs = true")]
        [InlineData("dotnet_code_quality.CA1051.exclude_structs = false")]
        public async Task PublicFieldOnStruct_AnalyzerOptionAsync(string editorConfigText)
        {
            var expectsIssue = !editorConfigText.EndsWith("true", StringComparison.OrdinalIgnoreCase);

            var csharpCode = @"
public struct S
{
    public int " + (expectsIssue ? "[|F|]" : "F") + @";
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { csharpCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();

            var vbCode = @"
Public Structure S
    Public " + (expectsIssue ? "[|F|]" : "F") + @" As Integer
End Structure";
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { vbCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}