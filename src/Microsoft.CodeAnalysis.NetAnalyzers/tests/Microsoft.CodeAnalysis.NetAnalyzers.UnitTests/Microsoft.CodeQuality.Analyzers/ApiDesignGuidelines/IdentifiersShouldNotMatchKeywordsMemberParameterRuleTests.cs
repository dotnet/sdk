﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotMatchKeywordsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotMatchKeywordsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    /// <summary>
    /// Contains those unit tests for the IdentifiersShouldNotMatchKeywords analyzer that
    /// pertain to the MemberParameterRule, which applies to the names of type member parameters.
    /// </summary>
    public class IdentifiersShouldNotMatchKeywordsMemberParameterRuleTests
    {
        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @int) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpDiagnosticForEachKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @int, float @float) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int, float)", "int", "int"),
                GetCSharpResultAt(4, 43, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int, float)", "float", "float"));
        }

        [Fact]
        public async Task BasicDiagnosticForEachKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int] As Integer, [float] As Single)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer, Single)", "int", "int"),
                GetBasicResultAt(3, 48, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer, Single)", "float", "float"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForCaseSensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassWithDifferentCasingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @iNt) {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForCaseSensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassWithDifferentCasingAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([iNt] As Integer)
    End Sub
End Class");
        }
        [Fact]
        public async Task CSharpDiagnosticForCaseInsensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @aDdHaNdLeR) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public async Task BasicDiagnosticForCaseInsensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([aDdHaNdLeR] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected virtual void F(int @int) {}
}",
                GetCSharpResultAt(4, 34, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Overridable Sub F([int] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedParameterOfInternalVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    internal virtual void F(int @int) {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfInternalVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Friend Overridable Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForParameterOfPublicNonVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void F(int @int) {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfPublicNonVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void F(int int2) {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonKeywordNamedParameterOfPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int2] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInInternalClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
    public void F(int @int) {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInInternalClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfMethodInPublicInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
    void F(int @int);
}",
                GetCSharpResultAt(4, 16, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "I.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfMethodInPublicInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Sub F([int] As Integer)
End Interface",
                GetBasicResultAt(3, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "I.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedParameterOfMethodInInternalInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface I
{
    void F(int @int);
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfMethodInInternalInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface I
    Sub F([int] As Integer)
End Interface");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedParameterOfOverrideOfPublicMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @int) {}
}

public class D : C
{
    public override void F(int @int) {}
}",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfOverrideOfMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class

Public Class D
    Inherits C

    Public Overrides Sub F([int] As Integer)
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedParameterOfNewMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @int) {}
}

public class D : C
{
    public new void F(int @int) {}
}",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedParameterOfNewMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class

Public Class D
    Inherits C

    Public Shadows Sub F([int] As Integer)
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfVirtualNewMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void F(int @int) {}
}

public class D : C
{
    public virtual new void F(int @int) {}
}",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"),
                GetCSharpResultAt(9, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "D.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfVirtualNewMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class

Public Class D
    Inherits C

    Public Overridable Shadows Sub F([int] As Integer)
    End Sub
End Class",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"),
                GetBasicResultAt(10, 38, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "D.F(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfVirtualPublicIndexerInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual int this[int @int]
    {
        get { return 0; }
    }
}",
                // TODO: FxCop doesn't mention the "get", but the formatting we use displays the "get" for
                // C# (but not for VB, as shown in the next test).
                GetCSharpResultAt(4, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.this[int].get", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfVirtualPublicParameterizedPropertyInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable ReadOnly Property P([int] As Integer) As Integer
        Get
            Return 0
        End Get
    End Property
End Class",
                GetBasicResultAt(3, 44, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.P(Integer)", "int", "int"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInProtectedTypeNestedInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected class D
    {
        protected virtual void F(int @int) {}
    }
}",
                GetCSharpResultAt(6, 38, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.D.F(int)", "int", "int"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInProtectedTypeNestedInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Class D
        Protected Overridable Sub F([iNtEgEr] As Integer)
        End Sub
    End Class
End Class",
                GetBasicResultAt(4, 37, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.D.F(Integer)", "iNtEgEr", "Integer"));
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method, Property")]
        public async Task UserOptionDoesNotIncludeParameter_NoDiagnosticAsync(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual void F(int @int) {}
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Parameter")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Parameter, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Parameter")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Parameter, Property")]
        public async Task UserOptionIncludesParameter_DiagnosticAsync(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual void F(int @int) {}
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"), },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
