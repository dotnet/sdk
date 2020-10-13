// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class SealMethodsThatSatisfyPrivateInterfacesTests
    {
        [Fact]
        public async Task TestCSharp_ClassesThatCannotBeSubClassedOutsideThisAssembly_HasNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

// Declaring type only accessible to this assembly
internal class C : IFace
{
    public virtual void M()
    {
    }
}

// Declaring type can only be instantiated in this assembly
public class D : IFace
{
    internal D()
    {
    }

    public virtual void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_VirtualImplicit_HasDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public class C : IFace
{
    public virtual void M()
    {
    }
}
", GetCSharpResultAt(9, 25));
        }

        [Fact]
        public async Task TestCSharp_AbstractImplicit_HasDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public abstract class C : IFace
{
    public abstract void M();
}
", GetCSharpResultAt(9, 26));
        }

        [Fact]
        public async Task TestCSharp_Explicit_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public class C : IFace
{
    void IFace.M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_NoInterface_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_StructImplicit_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public class C : IFace
{
    public void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_PublicInterface_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface IFace
{
    void M();
}

public class C : IFace
{
    public void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_OverriddenFromBase_HasDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public abstract class B 
{
   public abstract void M();
}

public class C : B, IFace
{
    public override void M()
    {
    }
}
", GetCSharpResultAt(14, 26));
        }

        [Fact]
        public async Task TestCSharp_OverriddenFromBaseButMethodIsSealed_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public abstract class B 
{
   public abstract void M();
}

public class C : B, IFace
{
    public sealed override void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_OverriddenFromBaseButClassIsSealed_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public abstract class B 
{
   public abstract void M();
}

public sealed class C : B, IFace
{
    public override void M()
    {
    }
}
");
        }

        [Fact]
        public async Task TestCSharp_ImplicitlyImplementedFromBaseMember_HasDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

public class C : B, IFace
{
}
", GetCSharpResultAt(14, 14));
        }

        [Fact]
        public async Task TestCSharp_ImplicitlyImplementedFromBaseMember_Public_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

class C : B, IFace
{
}
");
        }

        [Fact]
        public async Task TestVB_Overridable_HasDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Overridable Sub M() Implements IFace.M
    End Sub
End Class
", GetBasicResultAt(9, 28));
        }

        [Fact]
        public async Task TestVB_MustOverride_HasDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class C
    Implements IFace

    Public MustOverride Sub M() Implements IFace.M
End Class
", GetBasicResultAt(9, 29));
        }

        [Fact]
        public async Task TestVB_OverridenFromBase_HasDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class B
    Public MustOverride Sub M()
End Class

Public Class C
    Inherits B
    Implements IFace

    Public Overrides Sub M() Implements IFace.M
    End Sub
End Class
", GetBasicResultAt(14, 26));
        }

        [Fact]
        public async Task TestVB_OverridenFromBaseButNotOverridable_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class B
    Public MustOverride Sub M()
End Class

Public Class C
    Inherits B
    Implements IFace

    Public NotOverridable Overrides Sub M() Implements IFace.M
    End Sub
End Class
");
        }

        [Fact]
        public async Task TestVB_NotExplicit_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class C
    Implements IFace

    Public MustOverride Sub M()

    Public Sub IFace_M() Implements IFace.M
    End Sub
End Class
");
        }

        [Fact]
        public async Task TestVB_PrivateMethod_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Private Sub M() Implements IFace.M
    End Sub
End Class
");
        }

        [Fact]
        public async Task TestVB_PublicMethod_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Sub M() Implements IFace.M
    End Sub
End Class
");
        }

        [Fact]
        public async Task TestVB_FriendMethod_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Friend Sub M() Implements IFace.M
    End Sub
End Class
");
        }

        [Fact]
        public async Task TestVB_PublicInterface_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Overridable Sub M() Implements IFace.M
    End Sub
End Class
");
        }

        // TODO:

        // sealed overrides - no diagnostic

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);

        private static DiagnosticResult GetBasicResultAt(int line, int column)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
    }
}