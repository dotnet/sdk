// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
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
    public class IdentifiersShouldNotMatchKeywordsMemberParameterRuleTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotMatchKeywordsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotMatchKeywordsAnalyzer();
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void F(int @int) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public void CSharpDiagnosticForEachKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void F(int @int, float @float) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int, float)", "int", "int"),
                GetCSharpResultAt(4, 43, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int, float)", "float", "float"));
        }

        [Fact]
        public void BasicDiagnosticForEachKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub F([int] As Integer, [float] As Single)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer, Single)", "int", "int"),
                GetBasicResultAt(3, 48, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer, Single)", "float", "float"));
        }

        [Fact]
        public void CSharpNoDiagnosticForCaseSensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void F(int @iNt) {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForCaseSensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub F([iNt] As Integer)
    End Sub
End Class");
        }
        [Fact]
        public void CSharpDiagnosticForCaseInsensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void F(int @aDdHaNdLeR) {}
}",
                GetCSharpResultAt(4, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public void BasicDiagnosticForCaseInsensitiveKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub F([aDdHaNdLeR] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 30, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    protected virtual void F(int @int) {}
}",
                GetCSharpResultAt(4, 34, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(int)", "int", "int"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Protected Overridable Sub F([int] As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.F(Integer)", "int", "int"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedParameterOfInternalVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    internal virtual void F(int @int) {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedParameterOfInternalVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Friend Overridable Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForParameterOfPublicNonVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public void F(int @int) {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedParameterOfPublicNonVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForNonKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public void F(int int2) {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForNonKeywordNamedParameterOfPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub F([int2] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInInternalClass()
        {
            VerifyCSharp(@"
internal class C
{
    public void F(int @int) {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedParameterOfPublicVirtualMethodInInternalClass()
        {
            VerifyBasic(@"
Friend Class C
    Public Overridable Sub F([int] As Integer)
    End Sub
End Class");
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedParameterOfMethodInPublicInterface()
        {
            VerifyCSharp(@"
public interface I
{
    void F(int @int);
}",
                GetCSharpResultAt(4, 16, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "I.F(int)", "int", "int"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedParameterOfMethodInPublicInterface()
        {
            VerifyBasic(@"
Public Interface I
    Sub F([int] As Integer)
End Interface",
                GetBasicResultAt(3, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "I.F(Integer)", "int", "int"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedParameterOfMethodInInternalInterface()
        {
            VerifyCSharp(@"
internal interface I
{
    void F(int @int);
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedParameterOfMethodInInternalInterface()
        {
            VerifyBasic(@"
Friend Interface I
    Sub F([int] As Integer)
End Interface");
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedParameterOfOverrideOfPublicMethodInPublicClass()
        {
            VerifyCSharp(@"
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
        public void BasicNoDiagnosticForKeywordNamedParameterOfOverrideOfMethodInPublicClass()
        {
            VerifyBasic(@"
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
        public void CSharpNoDiagnosticForKeywordNamedParameterOfNewMethodInPublicClass()
        {
            VerifyCSharp(@"
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
        public void BasicNoDiagnosticForKeywordNamedParameterOfNewMethodInPublicClass()
        {
            VerifyBasic(@"
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
        public void CSharpDiagnosticForKeywordNamedParameterOfVirtualNewMethodInPublicClass()
        {
            VerifyCSharp(@"
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
        public void BasicDiagnosticForKeywordNamedParameterOfVirtualNewMethodInPublicClass()
        {
            VerifyBasic(@"
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
        public void CSharpDiagnosticForKeywordNamedParameterOfVirtualPublicIndexerInPublicClass()
        {
            VerifyCSharp(@"
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
        public void BasicDiagnosticForKeywordNamedParameterOfVirtualPublicParameterizedPropertyInPublicClass()
        {
            VerifyBasic(@"
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
        public void CSharpDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            VerifyCSharp(@"
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
        public void BasicDiagnosticForKeywordNamedParameterOfProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Protected Class D
        Protected Overridable Sub F([iNtEgEr] As Integer)
        End Sub
    End Class
End Class",
                GetBasicResultAt(4, 37, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberParameterRule, "C.D.F(Integer)", "iNtEgEr", "Integer"));
        }
    }
}
