// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class StaticHolderTypeTests : DiagnosticAnalyzerTestBase
    {
        #region Verifiers

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new StaticHolderTypesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new StaticHolderTypesAnalyzer();
        }

        private static DiagnosticResult CSharpResult(int line, int column, string objectName)
        {
            return GetCSharpResultAt(line, column, StaticHolderTypesAnalyzer.RuleId, string.Format(MicrosoftCodeQualityAnalyzersResources.StaticHolderTypeIsNotStatic, objectName));
        }

        private static DiagnosticResult BasicResult(int line, int column, string objectName)
        {
            return GetBasicResultAt(line, column, StaticHolderTypesAnalyzer.RuleId, string.Format(MicrosoftCodeQualityAnalyzersResources.StaticHolderTypeIsNotStatic, objectName));
        }

        #endregion

        [Fact]
        public void CA1052NoDiagnosticForEmptyNonStaticClassCSharp()
        {
            VerifyCSharp(@"
public class C1
{
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForEmptyInheritableClassBasic()
        {
            VerifyBasic(@"
Public Class B1
End Class
");
        }

        [Fact]

        public void CA1052NoDiagnosticForStaticClassWithOnlyStaticDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
public static class C2
{
    public static void Foo() { }
}
");
        }

        [Fact, WorkItem(1320, "https://github.com/dotnet/roslyn-analyzers/issues/1320")]
        public void CA1052NoDiagnosticForSealedClassWithOnlyStaticDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
public sealed class C3
{
    public static void Foo() { }
}
");
        }

        [Fact, WorkItem(1320, "https://github.com/dotnet/roslyn-analyzers/issues/1320")]
        public void CA1052NoDiagnosticForNonInheritableClassWithOnlySharedDeclaredMembersBasic()
        {
            VerifyBasic(@"
Public NotInheritable Class B3
    Public Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact, WorkItem(1292, "https://github.com/dotnet/roslyn-analyzers/issues/1292")]
        public void CA1052NoDiagnosticForSealedClassWithPublicConstructorAndStaticMembers()
        {
            VerifyCSharp(@"
using System.Threading;

public sealed class ConcurrentCreationDummy
{
    private static int creationAttempts;

    public ConcurrentCreationDummy()
    {
        if (IsCreatingFirstInstance())
        {
            CreatingFirstInstance.Set();
            CreatedSecondInstance.Wait();
        }
    }

    public static ManualResetEventSlim CreatingFirstInstance { get; } = new ManualResetEventSlim();

    public static ManualResetEventSlim CreatedSecondInstance { get; } = new ManualResetEventSlim();

    private static bool IsCreatingFirstInstance()
    {
        return Interlocked.Increment(ref creationAttempts) == 1;
    }
}");
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
public class C4
{
    public static void Foo() { }
}
",
                CSharpResult(2, 14, "C4"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithOnlySharedDeclaredMembersBasic()
        {
            VerifyBasic(@"
Public Class B4
    Public Shared Sub Foo()
    End Sub
End Class
",
                BasicResult(2, 14, "B4"));
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithBothStaticAndInstanceDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
public class C5
{
    public void Moo() { }
    public static void Foo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithBothSharedAndInstanceDeclaredMembersBasic()
        {
            VerifyBasic(@"
Public Class B5
    Public Sub Moo()
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForInternalClassWithOnlyStaticDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
internal class C6
{
    public static void Foo() { }
}
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1052NoDiagnosticForEffectivelyInternalClassWithOnlyStaticDeclaredMembersCSharp()
        {
            VerifyCSharp(@"
internal class C6
{
    public class Inner
    {
        public static void Foo() { }
    }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForFriendClassWithOnlySharedDeclaredMembersBasic()
        {
            VerifyBasic(@"
Friend Class B6
    Public Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1052NoDiagnosticForEffectivelyFriendClassWithOnlySharedDeclaredMembersBasic()
        {
            VerifyBasic(@"
Friend Class B6
    Public Class InnerClass
        Public Shared Sub Foo()
        End Sub
    End Class
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithUserDefinedOperatorCSharp()
        {
            VerifyCSharp(@"
public class C7
{
    public static int operator +(C7 a, C7 b)
    {
        return 0;
    }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithUserDefinedOperatorBasic()
        {
            VerifyBasic(@"
Public Class B7
    Public Shared Operator +(a As B7, b As B7) As Integer
        Return 0
    End Operator
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithStaticMethodAndUserDefinedOperatorCSharp()
        {
            VerifyCSharp(@"
public class C8
{
    public static void Foo() { }

    public static int operator +(C8 a, C8 b)
    {
        return 0;
    }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithSharedMethodAndUserDefinedOperatorBasic()
        {
            VerifyBasic(@"
Public Class B8
    Public Shared Sub Foo()
    End Sub

    Public Shared Operator +(a As B8, b As B8) As Integer
        Return 0
    End Operator
End Class
");
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithPublicDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C9
{
    public C9() { }

    public static void Foo() { }
}
",
            CSharpResult(2, 14, "C9"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithPublicDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B9
    Public Sub New()
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(2, 14, "B9"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithProtectedDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C10
{
    protected C10() { }

    public static void Foo() { }
}
",
            CSharpResult(2, 14, "C10"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithProtectedDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B10
    Protected Sub New()
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(2, 14, "B10"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithPrivateDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C11
{
    private C11() { }

    public static void Foo() { }
}
",
            CSharpResult(2, 14, "C11"));
        }

        [Fact]
        public void CA1052DiagnosticForNonStaticClassWithPrivateDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B11
    Private Sub New()
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(2, 14, "B11"));
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithPublicNonDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C12
{
    public C12(int i) { }

    public static void Foo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithPublicNonDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B12
    Public Sub New(i as Integer)
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithPublicNonDefaultConstructorWithDefaultedParametersAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C13
{
    public C13(int i = 0, string s = """") { }

    public static void Foo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithPublicNonDefaultConstructorWithOptionalParametersAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B13
    Public Sub New(Optional i as Integer = 0, Optional s as String = """")
    End Sub

    Public Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052DiagnosticForNestedPublicNonStaticClassWithPublicDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C14
{
    public void Moo() { }

    public class C14Inner
    {
        public C14Inner() { }
        public static void Foo() { }
    }
}
",
                CSharpResult(6, 18, "C14Inner"));
        }

        [Fact]
        public void CA1052DiagnosticForNestedPublicNonStaticClassWithPublicDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B14
    Public Sub Moo()
    End Sub

    Public Class B14Inner
        Public Sub New()
        End Sub

        Public Shared Sub Foo()
        End Sub
    End Class
End Class
",
                BasicResult(6, 18, "B14Inner"));
        }

        [Fact]
        public void CA1052NoDiagnosticForEmptyStaticClassCSharp()
        {
            VerifyCSharp(@"
public static class C15
{
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithStaticConstructorCSharp()
        {
            VerifyCSharp(@"
public class C16
{
    static C16() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithStaticConstructorBasic()
        {
            VerifyBasic(@"
Public Class B16
    Shared Sub New()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForStaticClassWithStaticConstructorCSharp()
        {
            VerifyCSharp(@"
public static class C17
{
    static C17() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithStaticConstructorAndInstanceConstructorCSharp()
        {
            VerifyCSharp(@"
public class C18
{
    public C18() { }
    static C18() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithStaticConstructorAndInstanceConstructorBasic()
        {
            VerifyBasic(@"
Public Class B18
    Sub New()
    End Sub

    Shared Sub New()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052DiagnosticForNestedPublicClassInOtherwiseEmptyNonStaticClassCSharp()
        {
            VerifyCSharp(@"
public class C19
{
    public class C19Inner
    {
    }
}
",
                CSharpResult(2, 14, "C19"));
        }

        [Fact]
        public void CA1052DiagnosticForNestedPublicClassInOtherwiseEmptyNonStaticClassBasic()
        {
            VerifyBasic(@"
Public Class B19
    Public Class B19Inner
    End Class
End Class
",
                BasicResult(2, 14, "B19"));
        }

        [Fact]
        public void CA1052NoDiagnosticAnEnumCSharp()
        {
            VerifyCSharp(@"
public enum E20
{
    Unknown = 0
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticAnEnumBasic()
        {
            VerifyBasic(@"
Public Enum EB20
    Unknown = 0
End Enum
");
        }

        [Fact]
        public void CA1052NoDiagnosticOnClassWithOnlyDefaultConstructorCSharp()
        {
            VerifyCSharp(@"
public class C21
{
    public C21() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticOnClassWithOnlyDefaultConstructorBasic()
        {
            VerifyBasic(@"
Public Class B21
    Public Sub New()
    End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNestedPrivateNonStaticClassWithPublicDefaultConstructorAndStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C22
{
    public void Moo() { }

    private class C22Inner
    {
        public C22Inner() { }
        public static void Foo() { }
    }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNestedPrivateNonStaticClassWithPublicDefaultConstructorAndSharedMethodBasic()
        {
            VerifyBasic(@"
Public Class B22
    Public Sub Moo()
    End Sub

    Private Class B22Inner
        Public Sub New()
        End Sub

        Public Shared Sub Foo()
        End Sub
    End Class
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndBaseClassCSharp()
        {
            VerifyCSharp(@"
public class C23Base
{
}
public class C23 : C23Base
{
    public static void Foo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndBaseClassBasic()
        {
            VerifyBasic(@"
Public Class B23Base
End Class
Public Class B23
	Inherits B23Base
	Public Shared Sub Foo()
	End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndEmptyBaseInterfaceCSharp()
        {
            VerifyCSharp(@"
public interface IC24Base
{
}
public class C24 : IC24Base
{
    public static void Foo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndEmptyBaseInterfaceBasic()
        {
            VerifyBasic(@"
Public Interface IB24Base
End Interface
Public Class B24
	Implements IB24Base
	Public Shared Sub Foo()
	End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndNotEmptyBaseInterfaceCSharp()
        {
            VerifyCSharp(@"
public interface IC25Base
{
    void Moo();
}
public class C25 : IC25Base
{
    public static void Foo() { }
    void IC25Base.Moo() { }
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndNotEmptyBaseInterfaceBasic()
        {
            VerifyBasic(@"
Public Interface IB25Base
    Sub Moo()
End Interface
Public Class B25
	Implements IB25Base
	Public Shared Sub Foo()
	End Sub
	Private Sub B25Base_Moo() Implements IB25Base.Moo
	End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndIncompleteBaseClassDefinitionCSharp()
        {
            VerifyCSharp(@"
public class C26 :
{
    public static void Foo() { }
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyStaticDeclaredMembersAndIncompleteBaseClassDefinitionBasic()
        {
            VerifyBasic(@"
Public Class B26
	Inherits
	Public Shared Sub Foo()
	End Sub
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1052NoDiagnosticForEmptyNonStaticClassWithIncompleteBaseClassDefinitionCSharp()
        {
            VerifyCSharp(@"
public class C27 :
{
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1052NoDiagnosticForEmptyNonStaticClassWithIncompleteBaseClassDefinitionBasic()
        {
            VerifyBasic(@"
Public Class B27
	Inherits
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyPrivateAndProtectedStaticMethodsCSharp()
        {
            VerifyCSharp(@"
public class C28
{
    private static void Foo() {}
    protected static void Bar() {}
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyPrivateAndProtectedStaticMethodsBasic()
        {
            VerifyBasic(@"
Public Class B28
	Private Shared Sub Foo()
	End Sub
	Protected Shared Sub Bar()
	End Sub
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyExplicitConversionOperatorsCSharp()
        {
            VerifyCSharp(@"
public class C29
{
    public static explicit operator C29(int foo) => new C29();
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyImplicitConversionOperatorsCSharp()
        {
            VerifyCSharp(@"
public class C29
{
    public static implicit operator C29(int foo) => new C29();
}
");
        }

        [Fact]
        public void CA1052NoDiagnosticForNonStaticClassWithOnlyExplicitConversionOperatorsBasic()
        {
            VerifyBasic(@"
Public Class B29
    Public Shared Widening Operator CType(ByVal foo As Integer) As B29
        Return New B29()
    End Operator
End Class
");
        }

        [Fact]
        public void CA1052NoDiagnosticForAbstractNonStaticClassCSharp()
        {
            VerifyCSharp(@"
public abstract class C1
{
    internal class C2 : C1 {
    }
}
");
        }
    }
}
