// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    /// <summary>
    /// Contains those unit tests for the IdentifiersShouldNotMatchKeywords analyzer that
    /// pertain to the MemberRule, which applies to the names of type members.
    /// </summary>
    public class IdentifiersShouldNotMatchKeywordsMemberRuleTests : DiagnosticAnalyzerTestBase
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
        public void CSharpDiagnosticForKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @internal() {}
}",
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub internal()
    End Sub
End Class
",
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public void CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @iNtErNaL() {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub iNtErNaL()
    End Sub
End Class");
        }

        [Fact]
        public void CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    // Matches VB AddHandler keyword:
    public virtual void aDdHaNdLeR() {}
}",
                GetCSharpResultAt(5, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.aDdHaNdLeR()", "AddHandler"));
        }

        [Fact]
        public void BasicDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    ' Matches VB AddHandler keyword:
    Public Overridable Sub [aDdHaNdLeR]()
    End Sub
End Class",
                GetBasicResultAt(4, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.aDdHaNdLeR()", "AddHandler"));
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    protected virtual void @for() {}
}",
                GetCSharpResultAt(4, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Protected Overridable Sub [for]()
    End Sub
End Class",
                GetBasicResultAt(3, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    internal virtual void @for() {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Friend Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public void @for() {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void fort() {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub fort()
    End Sub
End Class");
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedVirtualMethodInInternalClass()
        {
            VerifyCSharp(@"
internal class C
{
    public virtual void @for() {}
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedVirtualMethodInInternalClass()
        {
            VerifyBasic(@"
Friend Class C
    Public Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedMethodInPublicInterface()
        {
            VerifyCSharp(@"
public interface I
{
    void @for();
}",
                GetCSharpResultAt(4, 10, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedMethodInPublicInterface()
        {
            VerifyBasic(@"
Public Interface I
    Sub [for]()
End Interface",
                GetBasicResultAt(3, 9, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedMethodOfInternalInterface()
        {
            VerifyCSharp(@"
internal interface I
{
    void @for();
}");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedMethodOfInternalInterface()
        {
            VerifyBasic(@"
Friend Interface I
    Sub [for]()
End Interface");
        }

        [Fact]
        public void CSharpDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        get
        {
            return _for;
        }
        set
        {
            _for = value;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Private _for As Integer
    Public Overridable Property [Sub] As Integer
        Get
            Return _for
        End Get
        Set(value As Integer)
            _for = value
        End Set
    End Property
End Class",
                GetBasicResultAt(4, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public void CSharpDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual int @for { get; set; }
}",
                GetCSharpResultAt(4, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Property [Sub] As Integer
End Class",
                GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public void CSharpDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        get
        {
            return _for;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Private _for As Integer
    Public Overridable ReadOnly Property [Sub] As Integer
        Get
            Return _for
        End Get
    End Property
End Class",
                GetBasicResultAt(4, 42, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public void CSharpDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        set
        {
            _for = value;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public void BasicDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Private _for As Integer
    Public Overridable WriteOnly Property [Sub] As Integer
        Set(value As Integer)
            _for = value
        End Set
    End Property
End Class",
                GetBasicResultAt(4, 43, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public void CSharpDiagnosticForKeyWordNamedPublicVirtualExpressionBodyPropertyOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    private int _for;
    public virtual int @for => _for;
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public void CSharpNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @internal() {}
}

public class D : C
{
    public override void @internal() {}
}",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public void BasicNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub [internal]()
    End Sub
End Class

Public Class D
    Inherits C
    Public Overrides Sub [internal]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public void CSharpNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @internal() {}
}

public class D : C
{
    public sealed override void @internal() {}
}",
                // Diagnostic for the virtual in C, but none for the sealed override in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public void BasicNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub [friend]()
    End Sub
End Class

Public Class D
    Inherits C
    Public NotOverridable Overrides Sub [friend]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the sealed override in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.friend()", "friend"));
        }

        [Fact]
        public void CSharpDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @internal() {}
    public virtual void @internal(int n) {}
}",
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"),
                GetCSharpResultAt(5, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal(int)", "internal"));
        }

        [Fact]
        public void BasicDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub internal()
    End Sub
    Public Overridable Sub internal(n As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"),
                GetBasicResultAt(5, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal(Integer)", "internal"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedNewMethodInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @for() {}
}

public class D : C
{
    public new void @for() {}
}",
                // Diagnostic for the virtual in C, but none for the new method in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedNewMethodInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub [for]()
    End Sub
End Class

Public Class D
    Inherits C

    Public Shadows Sub [for]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the new method in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public void CSharpDiagnosticForVirtualNewMethod()
        {
            VerifyCSharp(@"
public class C
{
    public virtual void @for() {}
}

public class D : C
{
    public virtual new void @for() {}
}",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"),
                GetCSharpResultAt(9, 29, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "D.for()", "for"));
        }

        [Fact]
        public void BasicDiagnosticForVirtualNewMethod()
        {
            VerifyBasic(@"
Public Class C
    Public Overridable Sub [for]()
    End Sub
End Class

Public Class D
    Inherits C

    Public Overridable Shadows Sub [for]()
    End Sub
End Class",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"),
                GetBasicResultAt(10, 36, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "D.for()", "for"));
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    protected class D
    {
        protected virtual void @protected() {}
    }
}",
                GetCSharpResultAt(6, 32, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.D.protected()", "protected"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Protected Class D
        Protected Overridable Sub [Protected]()
        End Sub
    End Class
End Class",
                GetBasicResultAt(4, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.D.Protected()", "Protected"));
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedPublicVirtualEventInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    public delegate void Callback(object sender, System.EventArgs e);
    public virtual event Callback @float;
}",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetCSharpResultAt(5, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.float", "float"));
        }

        // These tests are just to verify that the formatting of the displayed member name
        // is consistent with FxCop, for the case where the class is in a namespace.
        [Fact]
        public void CSharpDiagnosticForVirtualPublicMethodInPublicClassInNamespace()
        {
            VerifyCSharp(@"
namespace N
{
    public class C
    {
        public virtual void @for() {}
    }
}",
                // Don't include the namespace name.
                GetCSharpResultAt(6, 29, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public void BasicDiagnosticForVirtualPublicMethodInPublicClassInNamespace()
        {
            VerifyBasic(@"
Namespace N
    Public Class C
        Public Overridable Sub [for]()
        End Sub
    End Class
End Namespace",
                // Don't include the namespace name.
                GetBasicResultAt(4, 32, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        // These tests are just to verify that the formatting of the displayed member name
        // is consistent with FxCop, for the case where the class is generic.
        [Fact]
        public void CSharpDiagnosticForVirtualPublicMethodInPublicGenericClass()
        {
            VerifyCSharp(@"
public class C<T> where T : class
{
    public virtual void @for() {}
}",
                // Include the type parameter name but not the constraint.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C<T>.for()", "for"));
        }

        [Fact]
        public void BasicDiagnosticForVirtualPublicMethodInPublicGenericClass()
        {
            VerifyBasic(@"
Public Class C(Of T As Class)
    Public Overridable Sub [for]()
    End Sub
End Class",
                // Include the type parameter name but not the constraint.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C(Of T).for()", "for"));
        }
    }
}