
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotContainUnderscoresAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotContainUnderscoresFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotContainUnderscoresAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotContainUnderscoresFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldNotContainUnderscoresTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotContainUnderscoresAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotContainUnderscoresAnalyzer();
        }

        #region CSharp Tests
        [Fact]
        public void CA1707_ForAssembly_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
}
",
            testProjectName: "AssemblyNameHasUnderScore_",
            expected: GetCA1707CSharpResultAt(line: 2, column: 1, symbolKind: SymbolKind.Assembly, identifierNames: "AssemblyNameHasUnderScore_"));
        }

        [Fact]
        public void CA1707_ForAssembly_NoDiagnostics_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
}
",
            testProjectName: "AssemblyNameHasNoUnderScore");
        }

        [Fact]
        public void CA1707_ForNamespace_CSharp()
        {
            VerifyCSharp(@"
namespace OuterNamespace
{
    namespace HasUnderScore_
    {
        public class DoesNotMatter
        {
        }
    }
}

namespace HasNoUnderScore
{
    public class DoesNotMatter
    {
    }
}",
            GetCA1707CSharpResultAt(line: 4, column: 15, symbolKind: SymbolKind.Namespace, identifierNames: "OuterNamespace.HasUnderScore_"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1707_ForTypes_CSharp()
        {
            VerifyCSharp(@"
public class OuterType
{
    public class UnderScoreInName_
    {
    }

    private class UnderScoreInNameButPrivate_
    {
    }

    internal class UnderScoreInNameButInternal_
    {
    }
}

internal class OuterType2
{
    public class UnderScoreInNameButNotExternallyVisible_
    {
    }
}
",
            GetCA1707CSharpResultAt(line: 4, column: 18, symbolKind: SymbolKind.NamedType, identifierNames: "OuterType.UnderScoreInName_"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1707_ForFields_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
        public const int ConstField_ = 5;
        public static readonly int StaticReadOnlyField_ = 5;

        // No diagnostics for the below
        private string InstanceField_;
        private static string StaticField_;
        public string _field;
        protected string Another_field;
}

public enum DoesNotMatterEnum
{
    _EnumWithUnderscore,
    _
}

public class C
{
    internal class C2
    {
        public const int ConstField_ = 5;
    }
}
",
            GetCA1707CSharpResultAt(line: 4, column: 26, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ConstField_"),
            GetCA1707CSharpResultAt(line: 5, column: 36, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.StaticReadOnlyField_"),
            GetCA1707CSharpResultAt(line: 16, column: 5, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatterEnum._EnumWithUnderscore"),
            GetCA1707CSharpResultAt(line: 17, column: 5, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatterEnum._"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1707_ForMethods_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
    public void PublicM1_() { }
    private void PrivateM2_() { } // No diagnostic
    internal void InternalM3_() { } // No diagnostic
    protected void ProtectedM4_() { }
}

public interface I1
{
    void M_();
}

public class ImplementI1 : I1
{
    public void M_() { } // No diagnostic
    public virtual void M2_() { }
}

public class Derives : ImplementI1
{
    public override void M2_() { } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public void PublicM1_() { } // No diagnostic
        protected void ProtectedM4_() { } // No diagnostic
    }
}",
            GetCA1707CSharpResultAt(line: 4, column: 17, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicM1_()"),
            GetCA1707CSharpResultAt(line: 7, column: 20, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedM4_()"),
            GetCA1707CSharpResultAt(line: 12, column: 10, symbolKind: SymbolKind.Member, identifierNames: "I1.M_()"),
            GetCA1707CSharpResultAt(line: 18, column: 25, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.M2_()"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1707_ForProperties_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
    public int PublicP1_ { get; set; }
    private int PrivateP2_ { get; set; } // No diagnostic
    internal int InternalP3_ { get; set; } // No diagnostic
    protected int ProtectedP4_ { get; set; }
}

public interface I1
{
    int P_ { get; set; }
}

public class ImplementI1 : I1
{
    public int P_ { get; set; } // No diagnostic
    public virtual int P2_ { get; set; }
}

public class Derives : ImplementI1
{
    public override int P2_ { get; set; } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public int PublicP1_ { get; set; }// No diagnostic
        protected int ProtectedP4_ { get; set; } // No diagnostic
    }
}",
            GetCA1707CSharpResultAt(line: 4, column: 16, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicP1_"),
            GetCA1707CSharpResultAt(line: 7, column: 19, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedP4_"),
            GetCA1707CSharpResultAt(line: 12, column: 9, symbolKind: SymbolKind.Member, identifierNames: "I1.P_"),
            GetCA1707CSharpResultAt(line: 18, column: 24, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.P2_"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1707_ForEvents_CSharp()
        {
            VerifyCSharp(@"
using System;

public class DoesNotMatter
{
    public event EventHandler PublicE1_;
    private event EventHandler PrivateE2_; // No diagnostic
    internal event EventHandler InternalE3_; // No diagnostic
    protected event EventHandler ProtectedE4_;
}

public interface I1
{
    event EventHandler E_;
}

public class ImplementI1 : I1
{
    public event EventHandler E_;// No diagnostic
    public virtual event EventHandler E2_;
}

public class Derives : ImplementI1
{
    public override event EventHandler E2_; // No diagnostic
}

internal class C
{
    public class DoesNotMatter
    {
        public event EventHandler PublicE1_; // No diagnostic
        protected event EventHandler ProtectedE4_; // No diagnostic
    }
}",
            GetCA1707CSharpResultAt(line: 6, column: 31, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicE1_"),
            GetCA1707CSharpResultAt(line: 9, column: 34, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedE4_"),
            GetCA1707CSharpResultAt(line: 14, column: 24, symbolKind: SymbolKind.Member, identifierNames: "I1.E_"),
            GetCA1707CSharpResultAt(line: 20, column: 39, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.E2_"));
        }

        [Fact]
        public void CA1707_ForDelegates_CSharp()
        {
            VerifyCSharp(@"
public delegate void Dele(int intPublic_, string stringPublic_);
internal delegate void Dele2(int intInternal_, string stringInternal_); // No diagnostics
public delegate T Del<T>(int t_);
",
            GetCA1707CSharpResultAt(2, 31, SymbolKind.DelegateParameter, "Dele", "intPublic_"),
            GetCA1707CSharpResultAt(2, 50, SymbolKind.DelegateParameter, "Dele", "stringPublic_"),
            GetCA1707CSharpResultAt(4, 30, SymbolKind.DelegateParameter, "Del<T>", "t_"));
        }

        [Fact]
        public void CA1707_ForMemberparameters_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter
{
    public void PublicM1(int int_) { }
    private void PrivateM2(int int_) { } // No diagnostic
    internal void InternalM3(int int_) { } // No diagnostic
    protected void ProtectedM4(int int_) { }
}

public interface I
{
    void M(int int_);
}

public class implementI : I
{
    public void M(int int_)
    {
    }
}

public abstract class Base
{
    public virtual void M1(int int_)
    {
    }

    public abstract void M2(int int_);
}

public class Der : Base
{
    public override void M2(int int_)
    {
        throw new System.NotImplementedException();
    }

    public override void M1(int int_)
    {
        base.M1(int_);
    }
}",
            GetCA1707CSharpResultAt(4, 30, SymbolKind.MemberParameter, "DoesNotMatter.PublicM1(int)", "int_"),
            GetCA1707CSharpResultAt(7, 36, SymbolKind.MemberParameter, "DoesNotMatter.ProtectedM4(int)", "int_"),
            GetCA1707CSharpResultAt(12, 16, SymbolKind.MemberParameter, "I.M(int)", "int_"),
            GetCA1707CSharpResultAt(24, 32, SymbolKind.MemberParameter, "Base.M1(int)", "int_"),
            GetCA1707CSharpResultAt(28, 33, SymbolKind.MemberParameter, "Base.M2(int)", "int_"));
        }

        [Fact]
        public void CA1707_ForTypeTypeParameters_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter<T_>
{
}

class NoDiag<U_>
{
}",
            GetCA1707CSharpResultAt(2, 28, SymbolKind.TypeTypeParameter, "DoesNotMatter<T_>", "T_"));
        }

        [Fact]
        public void CA1707_ForMemberTypeParameters_CSharp()
        {
            VerifyCSharp(@"
public class DoesNotMatter22
{
    public void PublicM1<T1_>() { }
    private void PrivateM2<U_>() { } // No diagnostic
    internal void InternalM3<W_>() { } // No diagnostic
    protected void ProtectedM4<D_>() { }
}

public interface I
{
    void M<T_>();
}

public class implementI : I
{
    public void M<U_>()
    {
        throw new System.NotImplementedException();
    }
}

public abstract class Base
{
    public virtual void M1<T_>()
    {
    }

    public abstract void M2<U_>();
}

public class Der : Base
{
    public override void M2<U_>()
    {
        throw new System.NotImplementedException();
    }

    public override void M1<T_>()
    {
        base.M1<T_>();
    }
}",
            GetCA1707CSharpResultAt(4, 26, SymbolKind.MethodTypeParameter, "DoesNotMatter22.PublicM1<T1_>()", "T1_"),
            GetCA1707CSharpResultAt(7, 32, SymbolKind.MethodTypeParameter, "DoesNotMatter22.ProtectedM4<D_>()", "D_"),
            GetCA1707CSharpResultAt(12, 12, SymbolKind.MethodTypeParameter, "I.M<T_>()", "T_"),
            GetCA1707CSharpResultAt(25, 28, SymbolKind.MethodTypeParameter, "Base.M1<T_>()", "T_"),
            GetCA1707CSharpResultAt(29, 29, SymbolKind.MethodTypeParameter, "Base.M2<U_>()", "U_"));
        }

        [Fact, WorkItem(947, "https://github.com/dotnet/roslyn-analyzers/issues/947")]
        public void CA1707_ForOperators_CSharp()
        {
            VerifyCSharp(@"
public struct S
{
    public static bool operator ==(S left, S right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(S left, S right)
    {
        return !(left == right);
    }
}
");
        }

        [Fact, WorkItem(1319, "https://github.com/dotnet/roslyn-analyzers/issues/1319")]
        public void CA1707_CustomOperator_CSharp()
        {
            VerifyCSharp(@"
public class Span
{
    public static implicit operator Span(string text) => new Span(text);
    public static explicit operator string(Span span) => span.GetText();
    private string _text;

    public Span(string text)
    {
        this._text = text;
    }

    public string GetText() => _text;
}
");
        }

        #endregion

        #region Visual Basic Tests
        [Fact]
        public void CA1707_ForAssembly_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
End Class
",
            testProjectName: "AssemblyNameHasUnderScore_",
            expected: GetCA1707BasicResultAt(line: 2, column: 1, symbolKind: SymbolKind.Assembly, identifierNames: "AssemblyNameHasUnderScore_"));
        }

        [Fact]
        public void CA1707_ForAssembly_NoDiagnostics_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
End Class
",
            testProjectName: "AssemblyNameHasNoUnderScore");
        }

        [Fact]
        public void CA1707_ForNamespace_VisualBasic()
        {
            VerifyBasic(@"
Namespace OuterNamespace
    Namespace HasUnderScore_
        Public Class DoesNotMatter
        End Class
    End Namespace
End Namespace

Namespace HasNoUnderScore
    Public Class DoesNotMatter
    End Class
End Namespace",
            GetCA1707BasicResultAt(line: 3, column: 15, symbolKind: SymbolKind.Namespace, identifierNames: "OuterNamespace.HasUnderScore_"));
        }

        [Fact]
        public void CA1707_ForTypes_VisualBasic()
        {
            VerifyBasic(@"
Public Class OuterType
    Public Class UnderScoreInName_
    End Class

    Private Class UnderScoreInNameButPrivate_
    End Class
End Class",
            GetCA1707BasicResultAt(line: 3, column: 18, symbolKind: SymbolKind.NamedType, identifierNames: "OuterType.UnderScoreInName_"));
        }

        [Fact]
        public void CA1707_ForFields_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
    Public Const ConstField_ As Integer = 5
    Public Shared ReadOnly SharedReadOnlyField_ As Integer = 5

    ' No diagnostics for the below
    Private InstanceField_ As String
    Private Shared StaticField_ As String
    Public _field As String
    Protected Another_field As String
End Class

Public Enum DoesNotMatterEnum
    _EnumWithUnderscore
End Enum",
            GetCA1707BasicResultAt(line: 3, column: 18, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ConstField_"),
            GetCA1707BasicResultAt(line: 4, column: 28, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.SharedReadOnlyField_"),
            GetCA1707BasicResultAt(line: 14, column: 5, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatterEnum._EnumWithUnderscore"));
        }

        [Fact]
        public void CA1707_ForMethods_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
    Public Sub PublicM1_()
    End Sub
    ' No diagnostic
    Private Sub PrivateM2_()
    End Sub
    ' No diagnostic
    Friend Sub InternalM3_()
    End Sub
    Protected Sub ProtectedM4_()
    End Sub
End Class

Public Interface I1
    Sub M_()
End Interface

Public Class ImplementI1
    Implements I1
    Public Sub M_() Implements I1.M_
    End Sub
    ' No diagnostic
    Public Overridable Sub M2_()
    End Sub
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Sub M2_()
    End Sub
End Class",
            GetCA1707BasicResultAt(line: 3, column: 16, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicM1_()"),
            GetCA1707BasicResultAt(line: 11, column: 19, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedM4_()"),
            GetCA1707BasicResultAt(line: 16, column: 9, symbolKind: SymbolKind.Member, identifierNames: "I1.M_()"),
            GetCA1707BasicResultAt(line: 24, column: 28, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.M2_()"));
        }

        [Fact]
        public void CA1707_ForProperties_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
    Public Property PublicP1_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Private Property PrivateP2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Friend Property InternalP3_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Protected Property ProtectedP4_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Interface I1
    Property P_() As Integer
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Property P_() As Integer Implements I1.P_
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Public Overridable Property P2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Property P2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class",
            GetCA1707BasicResultAt(line: 3, column: 21, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicP1_"),
            GetCA1707BasicResultAt(line: 26, column: 24, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedP4_"),
            GetCA1707BasicResultAt(line: 36, column: 14, symbolKind: SymbolKind.Member, identifierNames: "I1.P_"),
            GetCA1707BasicResultAt(line: 49, column: 33, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.P2_"));
        }

        [Fact]
        public void CA1707_ForEvents_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter
    Public Event PublicE1_ As System.EventHandler
    Private Event PrivateE2_ As System.EventHandler
    ' No diagnostic
    Friend Event InternalE3_ As System.EventHandler
    ' No diagnostic
    Protected Event ProtectedE4_ As System.EventHandler
End Class

Public Interface I1
    Event E_ As System.EventHandler
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Event E_ As System.EventHandler Implements I1.E_
    Public Event E2_ As System.EventHandler
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Shadows Event E2_ As System.EventHandler
End Class",
            GetCA1707BasicResultAt(line: 3, column: 18, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.PublicE1_"),
            GetCA1707BasicResultAt(line: 8, column: 21, symbolKind: SymbolKind.Member, identifierNames: "DoesNotMatter.ProtectedE4_"),
            GetCA1707BasicResultAt(line: 12, column: 11, symbolKind: SymbolKind.Member, identifierNames: "I1.E_"),
            GetCA1707BasicResultAt(line: 19, column: 18, symbolKind: SymbolKind.Member, identifierNames: "ImplementI1.E2_"),
            GetCA1707BasicResultAt(line: 25, column: 26, symbolKind: SymbolKind.Member, identifierNames: "Derives.E2_"));
        }

        [Fact]
        public void CA1707_ForDelegates_VisualBasic()
        {
            VerifyBasic(@"
Public Delegate Sub Dele(intPublic_ As Integer, stringPublic_ As String)
' No diagnostics
Friend Delegate Sub Dele2(intInternal_ As Integer, stringInternal_ As String)
Public Delegate Function Del(Of T)(t_ As Integer) As T
",
                    GetCA1707BasicResultAt(2, 26, SymbolKind.DelegateParameter, "Dele", "intPublic_"),
                    GetCA1707BasicResultAt(2, 49, SymbolKind.DelegateParameter, "Dele", "stringPublic_"),
                    GetCA1707BasicResultAt(5, 36, SymbolKind.DelegateParameter, "Del(Of T)", "t_"));
        }

        [Fact]
        public void CA1707_ForMemberparameters_VisualBasic()
        {
            VerifyBasic(@"Public Class DoesNotMatter
    Public Sub PublicM1(int_ As Integer)
    End Sub
    Private Sub PrivateM2(int_ As Integer)
    End Sub
    ' No diagnostic
    Friend Sub InternalM3(int_ As Integer)
    End Sub
    ' No diagnostic
    Protected Sub ProtectedM4(int_ As Integer)
    End Sub
End Class

Public Interface I
    Sub M(int_ As Integer)
End Interface

Public Class implementI
    Implements I
    Private Sub I_M(int_ As Integer) Implements I.M
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1(int_ As Integer)
    End Sub

    Public MustOverride Sub M2(int_ As Integer)
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(int_ As Integer)
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(int_ As Integer)
        MyBase.M1(int_)
    End Sub
End Class",
            GetCA1707BasicResultAt(2, 25, SymbolKind.MemberParameter, "DoesNotMatter.PublicM1(Integer)", "int_"),
            GetCA1707BasicResultAt(10, 31, SymbolKind.MemberParameter, "DoesNotMatter.ProtectedM4(Integer)", "int_"),
            GetCA1707BasicResultAt(15, 11, SymbolKind.MemberParameter, "I.M(Integer)", "int_"),
            GetCA1707BasicResultAt(25, 31, SymbolKind.MemberParameter, "Base.M1(Integer)", "int_"),
            GetCA1707BasicResultAt(28, 32, SymbolKind.MemberParameter, "Base.M2(Integer)", "int_"));
        }

        [Fact]
        public void CA1707_ForTypeTypeParameters_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter(Of T_)
End Class

Class NoDiag(Of U_)
End Class",
            GetCA1707BasicResultAt(2, 31, SymbolKind.TypeTypeParameter, "DoesNotMatter(Of T_)", "T_"));
        }

        [Fact]
        public void CA1707_ForMemberTypeParameters_VisualBasic()
        {
            VerifyBasic(@"
Public Class DoesNotMatter22
    Public Sub PublicM1(Of T1_)()
    End Sub
    Private Sub PrivateM2(Of U_)()
    End Sub
    Friend Sub InternalM3(Of W_)()
    End Sub
    Protected Sub ProtectedM4(Of D_)()
    End Sub
End Class

Public Interface I
    Sub M(Of T_)()
End Interface

Public Class implementI
    Implements I
    Public Sub M(Of U_)() Implements I.M
        Throw New System.NotImplementedException()
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1(Of T_)()
    End Sub

    Public MustOverride Sub M2(Of U_)()
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(Of U_)()
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(Of T_)()
        MyBase.M1(Of T_)()
    End Sub
End Class",
            GetCA1707BasicResultAt(3, 28, SymbolKind.MethodTypeParameter, "DoesNotMatter22.PublicM1(Of T1_)()", "T1_"),
            GetCA1707BasicResultAt(9, 34, SymbolKind.MethodTypeParameter, "DoesNotMatter22.ProtectedM4(Of D_)()", "D_"),
            GetCA1707BasicResultAt(14, 14, SymbolKind.MethodTypeParameter, "I.M(Of T_)()", "T_"),
            GetCA1707BasicResultAt(25, 34, SymbolKind.MethodTypeParameter, "Base.M1(Of T_)()", "T_"),
            GetCA1707BasicResultAt(28, 35, SymbolKind.MethodTypeParameter, "Base.M2(Of U_)()", "U_"));
        }

        [Fact, WorkItem(947, "https://github.com/dotnet/roslyn-analyzers/issues/947")]
        public void CA1707_ForOperators_VisualBasic()
        {
            VerifyBasic(@"
Public Structure S
	Public Shared Operator =(left As S, right As S) As Boolean
		Return left.Equals(right)
	End Operator

	Public Shared Operator <>(left As S, right As S) As Boolean
		Return Not (left = right)
	End Operator
End Structure
");
        }

        [Fact, WorkItem(1319, "https://github.com/dotnet/roslyn-analyzers/issues/1319")]
        public void CA1707_CustomOperator_VisualBasic()
        {
            VerifyBasic(@"
Public Class Span
    Public Shared Narrowing Operator CType(ByVal text As String) As Span
        Return New Span(text)
    End Operator

    Public Shared Widening Operator CType(ByVal span As Span) As String
        Return span.GetText()
    End Operator

    Private _text As String
    Public Sub New(ByVal text)
        _text = text
    End Sub

    Public Function GetText() As String
        Return _text
    End Function
End Class
");
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCA1707CSharpResultAt(int line, int column, SymbolKind symbolKind, params string[] identifierNames)
        {
            return GetCA1707CSharpResultAt(line, column, GetApproriateMessage(symbolKind), identifierNames);
        }

        private static DiagnosticResult GetCA1707CSharpResultAt(int line, int column, string message, params string[] identifierName)
        {
            return GetCSharpResultAt(line, column, IdentifiersShouldNotContainUnderscoresAnalyzer.RuleId, string.Format(message, identifierName));
        }

        private void VerifyCSharp(string source, string testProjectName, params DiagnosticResult[] expected)
        {
            Verify(source, LanguageNames.CSharp, GetCSharpDiagnosticAnalyzer(), testProjectName, expected);
        }

        private static DiagnosticResult GetCA1707BasicResultAt(int line, int column, SymbolKind symbolKind, params string[] identifierNames)
        {
            return GetCA1707BasicResultAt(line, column, GetApproriateMessage(symbolKind), identifierNames);
        }

        private static DiagnosticResult GetCA1707BasicResultAt(int line, int column, string message, params string[] identifierName)
        {
            return GetBasicResultAt(line, column, IdentifiersShouldNotContainUnderscoresAnalyzer.RuleId, string.Format(message, identifierName));
        }

        private void VerifyBasic(string source, string testProjectName, params DiagnosticResult[] expected)
        {
            Verify(source, LanguageNames.VisualBasic, GetBasicDiagnosticAnalyzer(), testProjectName, expected);
        }

        private void Verify(string source, string language, DiagnosticAnalyzer analyzer, string testProjectName, DiagnosticResult[] expected)
        {
            var sources = new[] { source };
            var diagnostics = GetSortedDiagnostics(sources.ToFileAndSource(), language, analyzer, compilationOptions: null, parseOptions: null, referenceFlags: ReferenceFlags.None, projectName: testProjectName);
            diagnostics.Verify(analyzer, GetDefaultPath(language), expected);
        }

        private static string GetApproriateMessage(SymbolKind symbolKind)
        {
            return symbolKind switch
            {
                SymbolKind.Assembly => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageAssembly,
                SymbolKind.Namespace => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageNamespace,
                SymbolKind.NamedType => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageType,
                SymbolKind.Member => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMember,
                SymbolKind.DelegateParameter => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageDelegateParameter,
                SymbolKind.MemberParameter => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMemberParameter,
                SymbolKind.TypeTypeParameter => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageTypeTypeParameter,
                SymbolKind.MethodTypeParameter => MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMethodTypeParameter,
                _ => throw new System.Exception("Unknown Symbol Kind"),
            };
        }

        private enum SymbolKind
        {
            Assembly,
            Namespace,
            NamedType,
            Member,
            DelegateParameter,
            MemberParameter,
            TypeTypeParameter,
            MethodTypeParameter
        }
        #endregion
    }
}