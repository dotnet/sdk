// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class SealMethodsThatSatisfyPrivateInterfacesTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new SealMethodsThatSatisfyPrivateInterfacesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SealMethodsThatSatisfyPrivateInterfacesAnalyzer();
        }

        [Fact]
        public void TestCSharp_ClassesThatCannotBeSubClassedOutsideThisAssembly_HasNoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_VirtualImplicit_HasDiagnostic()
        {
            VerifyCSharp(@"
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
", GetCSharpResultAt(9, 25, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharp_AbstractImplicit_HasDiagnostic()
        {
            VerifyCSharp(@"
internal interface IFace
{
    void M();
}

public abstract class C : IFace
{
    public abstract void M();
}
", GetCSharpResultAt(9, 26, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharp_Explicit_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_NoInterface_NoDiagnostic()
        {
            VerifyCSharp(@"
public class C
{
    public void M()
    {
    }
}
");
        }

        [Fact]
        public void TestCSharp_StructImplicit_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_PublicInterface_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_OverriddenFromBase_HasDiagnostic()
        {
            VerifyCSharp(@"
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
", GetCSharpResultAt(14, 26, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharp_OverriddenFromBaseButMethodIsSealed_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_OverriddenFromBaseButClassIsSealed_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestCSharp_ImplicitlyImplementedFromBaseMember_HasDiagnostic()
        {
            VerifyCSharp(@"
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
", GetCSharpResultAt(14, 14, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharp_ImplicitlyImplementedFromBaseMember_Public_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void TestVB_Overridable_HasDiagnostic()
        {
            VerifyBasic(@"
Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Overridable Sub M() Implements IFace.M
    End Sub
End Class
", GetBasicResultAt(9, 28, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestVB_MustOverride_HasDiagnostic()
        {
            VerifyBasic(@"
Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class C
    Implements IFace

    Public MustOverride Sub M() Implements IFace.M
End Class
", GetBasicResultAt(9, 29, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestVB_OverridenFromBase_HasDiagnostic()
        {
            VerifyBasic(@"
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
", GetBasicResultAt(14, 26, SealMethodsThatSatisfyPrivateInterfacesAnalyzer.Rule));
        }

        [Fact]
        public void TestVB_OverridenFromBaseButNotOverridable_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void TestVB_NotExplicit_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void TestVB_PrivateMethod_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void TestVB_PublicMethod_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void TestVB_FriendMethod_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void TestVB_PublicInterface_NoDiagnostic()
        {
            VerifyBasic(@"
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
    }
}