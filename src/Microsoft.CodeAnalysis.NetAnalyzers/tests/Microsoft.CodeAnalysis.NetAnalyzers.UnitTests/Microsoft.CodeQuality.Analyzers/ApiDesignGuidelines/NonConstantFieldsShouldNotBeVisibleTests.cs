// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.NonConstantFieldsShouldNotBeVisibleAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.NonConstantFieldsShouldNotBeVisibleAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class NonConstantFieldsShouldNotBeVisibleTests
    {
        [Fact]
        public async Task DefaultVisibilityCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    string field; 
}");
        }

        [Fact]
        public async Task DefaultVisibilityVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Dim field As System.String
End Class");
        }

        [Fact]
        public async Task PublicVariableCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public string field; 
}");
        }

        [Fact]
        public async Task PublicVariableVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public field As System.String
End Class");
        }

        [Fact]
        public async Task ExternallyVisibleStaticVariableCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static string field; 
}", GetCSharpResultAt(4, 26));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task PublicNotExternallyVisibleStaticVariableCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class A
{
    public static string field;
}

public class B
{
    private class C
    {
        public static string field;
    }
}
");
        }

        [Fact]
        public async Task ExternallyVisibleStaticVariableVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Shared field as System.String
End Class", GetBasicResultAt(3, 19));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task PublicNotExternallyVisibleStaticVariableVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class A
    Public Shared field as System.String
End Class

Public Class B
    Private Class C
        Public Shared field as System.String
    End Class
End Class
");
        }

        [Fact]
        public async Task PublicStaticReadonlyVariableCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static readonly string field; 
}");
        }

        [Fact]
        public async Task PublicStaticReadonlyVariableVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Shared ReadOnly field as System.String
End Class");
        }

        [Fact]
        public async Task PublicConstVariableCSAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public const string field = ""X"";
}");
        }

        [Fact]
        public async Task PublicConstVariableVBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Const field as System.String = ""X""
End Class");
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
