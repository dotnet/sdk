// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ImplementIDisposableCorrectlyAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpImplementIDisposableCorrectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ImplementIDisposableCorrectlyAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicImplementIDisposableCorrectlyFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ImplementIDisposableCorrectlyTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ImplementIDisposableCorrectlyAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ImplementIDisposableCorrectlyAnalyzer();
        }

        #region CSharp Unit Tests

        [Fact]
        public void CSharp_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact, WorkItem(1435, "https://github.com/dotnet/roslyn-analyzers/issues/1435")]
        public void CSharp_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern_WithAttributes()
        {
            VerifyCSharp(@"
using System;

public class MyAttribute : Attribute
{
    public MyAttribute(int x) { }
}

public class C : IDisposable
{
    [MyAttribute(0)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [MyAttribute(0)]
    ~C()
    {
        Dispose(false);
    }

    [MyAttribute(0)]
    protected virtual void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeSignature_NoDiagnostic_NotImplementingDisposable()
        {
            VerifyCSharp(@"
using System;

public class C
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
");
        }

        #endregion

        #region CSharp IDisposableReimplementation Unit Tests

        [Fact]
        public void CSharp_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposable()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public virtual void Dispose()
    {
    }
}

[|public class C : B, IDisposable
{
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}|]
",
            GetCA1063CSharpIDisposableReimplementationResultAt(11, 14, "C", "B"),
            GetCA1063CSharpDisposeSignatureResultAt(13, 26, "C", "Dispose"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ReimplementingIDisposable_Internal()
        {
            VerifyCSharp(@"
using System;

internal class B : IDisposable
{
    public virtual void Dispose()
    {
    }
}

[|internal class C : B, IDisposable
{
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}|]
");
        }

        [Fact]
        public void CSharp_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposableWithDeepInheritance()
        {
            VerifyCSharp(@"
using System;

public class A : IDisposable
{
    public virtual void Dispose()
    {
    }
}

public class B : A
{
}

[|public class C : B, IDisposable
{
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}|]
",
            GetCA1063CSharpIDisposableReimplementationResultAt(15, 14, "C", "B"),
            GetCA1063CSharpDisposeSignatureResultAt(17, 26, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInterfaceInheritedFromIDisposable()
        {
            VerifyCSharp(@"
using System;

public interface ITest : IDisposable
{
    int Test { get; set; }
}

public class B : IDisposable
{
    public void Dispose()
    {
    }
}

[|public class C : B, ITest
{
    public int Test { get; set; }

    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}|]
");
        }

        [Fact]
        public void CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ReImplementingIDisposableWithNoDisposeMethod()
        {
            VerifyCSharp(@"
using System;

public interface ITest : IDisposable
{
    int Test { get; set; }
}

public class B : IDisposable
{
    public void Dispose()
    {
    }
}

[|public class C : B, ITest, IDisposable
{
    public int Test { get; set; }
}|]
",
            GetCA1063CSharpIDisposableReimplementationResultAt(16, 14, "C", "B"));
        }

        [Fact]
        public void CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInheritedInterfaceWithNoDisposeReimplementation()
        {
            VerifyCSharp(@"
using System;

public interface ITest : IDisposable
{
    int Test { get; set; }
}

public class B : IDisposable
{
    public void Dispose()
    {
    }
}

[|public class C : B, ITest
{
    public int Test { get; set; }
}|]
");
        }

        #endregion

        #region CSharp DisposeSignature Unit Tests

        [Fact]
        public void CSharp_CA1063_DisposeSignature_Diagnostic_DisposeNotPublic()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}  
",
            GetCA1063CSharpDisposeSignatureResultAt(6, 22, "C", "System.IDisposable.Dispose"),
            GetCA1063CSharpRenameDisposeResultAt(6, 22, "C", "System.IDisposable.Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeSignature_Diagnostic_DisposeIsVirtual()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}  
",
            GetCA1063CSharpDisposeSignatureResultAt(6, 25, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeSignature_Diagnostic_DisposeIsOverriden()
        {
            VerifyCSharp(@"
using System;

public class B
{
    public virtual void Dispose()
    {
    }
}

public class C : B, IDisposable
{
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}  
",
            GetCA1063CSharpDisposeSignatureResultAt(13, 26, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeSignature_NoDiagnostic_DisposeIsOverridenAndSealed()
        {
            VerifyCSharp(@"
using System;

public class B
{
    public virtual void Dispose()
    {
    }
}

public class C : B, IDisposable
{
    public sealed override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}  
");
        }

        #endregion

        #region CSharp DisposeOverride Unit Tests

        [Fact]
        public void CSharp_CA1063_DisposeOverride_Diagnostic_SimpleDisposeOverride()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~B()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    public override void Dispose()
    {
    }
}|]
",
            GetCA1063CSharpDisposeOverrideResultAt(24, 26, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeOverride_Diagnostic_DoubleDisposeOverride()
        {
            VerifyCSharp(@"
using System;

public class A : IDisposable
{
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~A()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

public class B : A
{
    public override void Dispose()
    {
    }
}

[|public class C : B
{
    public override void Dispose()
    {
        Dispose(true);
    }
}|]
",
            GetCA1063CSharpDisposeOverrideResultAt(31, 26, "C", "Dispose"));
        }

        #endregion

        #region CSharp FinalizeOverride Unit Tests

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_OverridesDisposeBool()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
    }
}|]
",
            GetCA1063CSharpFinalizeImplementationResultAt(24, 6, "C", "Finalize"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~B()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    ~C()
    {
    }
}|]
",
            // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride()
        {
            VerifyCSharp(@"
using System;

public class A : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~A()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

public class B : A
{
    ~B()
    {
    }
}

[|public class C : B
{
    ~C()
    {
    }
}|]
",
            // Test0.cs(29,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(29, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_NoDiagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasNoFinalizer()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}|]
");
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasFinalizer()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~B()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}|]
",
            // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride_InvokesDisposeBool()
        {
            VerifyCSharp(@"
using System;

public class A : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~A()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

public class B : A
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~B()
    {
        Dispose(false);
    }
}

[|public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}|]
",
            // Test0.cs(35,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(35, 14, "C", "B"));
        }

        [Fact]
        public void CSharp_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotInBaseType()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
    }
}

[|public class C : B
{
    ~C()
    {
    }
}|]
");
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void CSharp_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotOverriden()
        {
            VerifyCSharp(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~B()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

[|public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}|]
");
        }
        #endregion

        #region CSharp ProvideDisposeBool Unit Tests

        [Fact]
        public void CSharp_CA1063_ProvideDisposeBool_Diagnostic_MissingDisposeBool()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
    }

    ~C()
    {
    }
}
",
            GetCA1063CSharpProvideDisposeBoolResultAt(4, 14, "C"),
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"),
            GetCA1063CSharpFinalizeImplementationResultAt(10, 6, "C", "Finalize"));
        }

        [Fact]
        public void CSharp_CA1063_ProvideDisposeBool_NoDiagnostic_SealedClassAndMissingDisposeBool()
        {
            VerifyCSharp(@"
using System;

public sealed class C : IDisposable
{
    public void Dispose()
    {
    }

    ~C()
    {
    }
}
");
        }

        #endregion

        #region CSharp DisposeBoolSignature Unit Tests

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsPublic()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    public virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeBoolSignatureResultAt(17, 25, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsProtectedInternal()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected internal virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeBoolSignatureResultAt(17, 37, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsNotVirtual()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeBoolSignatureResultAt(17, 20, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsSealedOverriden()
        {
            VerifyCSharp(@"
using System;

public abstract class B
{
    protected abstract void Dispose(bool disposing);
}

public class C : B, IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected sealed override void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeBoolSignatureResultAt(22, 36, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsOverriden()
        {
            VerifyCSharp(@"
using System;

public abstract class B
{
    protected abstract void Dispose(bool disposing);
}

public class C : B, IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected override void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsAbstract()
        {
            VerifyCSharp(@"
using System;

public abstract class C : IDisposable
{
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected abstract void Dispose(bool disposing);
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPublicAndClassIsSealed()
        {
            VerifyCSharp(@"
using System;

public sealed class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    public void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPrivateAndClassIsSealed()
        {
            VerifyCSharp(@"
using System;

public sealed class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact, WorkItem(1815, "https://github.com/dotnet/roslyn-analyzers/issues/1815")]
        public void CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsStatic()
        {
            VerifyCSharp(@"
using System;

public class Class1 : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    protected static void Dispose(bool disposing)
    {
        if (!disposing) return;
    }
}
",
            // Test0.cs(11,27): warning CA1063: Ensure that 'Class1.Dispose' is declared as protected, virtual, and unsealed.
            GetCA1063CSharpDisposeBoolSignatureResultAt(11, 27, "Class1", "Dispose"));
        }

        #endregion

        #region CSharp DisposeImplementation Unit Tests

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"));
        }

        [Fact, WorkItem(1974, "https://github.com/dotnet/roslyn-analyzers/issues/1974")]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_MissingCallSuppressFinalize_HasFinalizer()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"));
        }

        [Fact, WorkItem(1974, "https://github.com/dotnet/roslyn-analyzers/issues/1974")]
        public void CSharp_CA1063_DisposeImplementation_NoDiagnostic_MissingCallSuppressFinalize_NoFinalizer()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_EmptyDisposeBody()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_CallDisposeWithFalseArgument()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_ConditionalStatement()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    private bool disposed;

    public void Dispose()
    {
        if (!disposed)
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(8, 17, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_NoDiagnostic_ConditionalStatement_Internal()
        {
            VerifyCSharp(@"
using System;

internal class C : IDisposable
{
    private bool disposed;

    public void Dispose()
    {
        if (!disposed)
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
");
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpDisposeImplementationResultAt(6, 17, "C", "Dispose"));
        }

        [Fact]
        public void CSharp_CA1063_DisposeImplementation_NoDiagnostic_EmptyDisposeBodyInSealedClass()
        {
            VerifyCSharp(@"
using System;

public sealed class C : IDisposable
{
    public void Dispose()
    {
    }

    ~C()
    {
    }
}
");
        }

        #endregion

        #region CSharp FinalizeImplementation Unit Tests

        [Fact, WorkItem(1788, "https://github.com/dotnet/roslyn-analyzers/issues/1788")]
        public void CSharp_CA1063_FinalizeImplementation_NoDiagnostic_ExpressionBodiedImpl()
        {
            VerifyCSharp(@"
using System;
using System.IO;

public class SomeTestClass : IDisposable
{
    private readonly Stream resource = new MemoryStream(1024);

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            this.resource.Dispose();
        }
    }

    ~SomeTestClass() => this.Dispose(false);
}");
        }

        [Fact]
        public void CSharp_CA1063_FinalizeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpFinalizeImplementationResultAt(12, 6, "C", "Finalize"));
        }

        [Fact]
        public void CSharp_CA1063_FinalizeImplementation_Diagnostic_CallDisposeWithTrueArgument()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpFinalizeImplementationResultAt(12, 6, "C", "Finalize"));
        }

        [Fact]
        public void CSharp_CA1063_FinalizeImplementation_Diagnostic_ConditionalStatement()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    private bool disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        if (!disposed)
        {
            Dispose(false);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpFinalizeImplementationResultAt(14, 6, "C", "Finalize"));
        }

        [Fact]
        public void CSharp_CA1063_FinalizeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            VerifyCSharp(@"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~C()
    {
        Dispose(false);
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
",
            GetCA1063CSharpFinalizeImplementationResultAt(12, 6, "C", "Finalize"));
        }

        #endregion

        #region VB Unit Tests

        [Fact]
        public void Basic_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeSignature_NoDiagnostic_NotImplementingDisposable()
        {
            VerifyBasic(@"
Imports System

Public Class C

    Public Sub Dispose()
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        #endregion

        #region VB IDisposableReimplementation Unit Tests

        [Fact]
        public void Basic_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposable()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Public Class C
    Inherits B
    Implements IDisposable

    Public Overrides Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class|]
",
            GetCA1063BasicIDisposableReimplementationResultAt(11, 14, "C", "B"),
            GetCA1063BasicDisposeSignatureResultAt(15, 26, "C", "Dispose"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ReimplementingIDisposable_Internal()
        {
            VerifyBasic(@"
Imports System

Friend Class B
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Friend Class C
    Inherits B
    Implements IDisposable

    Public Overrides Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class|]
");
        }

        [Fact]
        public void Basic_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposableWithDeepInheritance()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Public Class B
    Inherits A
End Class

[|Public Class C
    Inherits B
    Implements IDisposable

    Public Overrides Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class|]
",
            GetCA1063BasicIDisposableReimplementationResultAt(15, 14, "C", "B"),
            GetCA1063BasicDisposeSignatureResultAt(19, 26, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInterfaceInheritedFromIDisposable()
        {
            VerifyBasic(@"
Imports System

Public Interface ITest
    Inherits IDisposable

    Property Test As Integer
End Interface

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Public Class C
    Inherits B
    Implements ITest

    Public Property Test As Integer Implements ITest.Test

    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class|]
");
        }

        [Fact]
        public void Basic_CA1063_IDisposableReimplementation_Diagnostic_ReImplementingIDisposableWithNoDisposeMethod()
        {
            VerifyBasic(@"
Imports System

Public Interface ITest
    Inherits IDisposable

    Property Test As Integer
End Interface

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Public NotInheritable Class C
    Inherits B
    Implements ITest
    Implements IDisposable

    Public Property Test As Integer Implements ITest.Test

End Class|]
",
            GetCA1063BasicIDisposableReimplementationResultAt(17, 29, "C", "B"));
        }

        [Fact]
        public void Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInheritedInterfaceWithNoDisposeReimplementation()
        {
            VerifyBasic(@"
Imports System

Public Interface ITest
    Inherits IDisposable

    Property Test As Integer
End Interface

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Public NotInheritable Class C
    Inherits B
    Implements ITest

    Public Property Test As Integer Implements ITest.Test

End Class|]
");
        }

        #endregion

        #region VB DisposeSignature Unit Tests

        [Fact]
        public void Basic_CA1063_DisposeSignature_Diagnostic_DisposeProtected()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Protected Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeSignatureResultAt(7, 19, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeSignature_Diagnostic_DisposePrivate()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Private Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeSignatureResultAt(7, 17, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsVirtual()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeSignatureResultAt(7, 28, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsOverriden()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Public Overridable Sub Dispose()
    End Sub
End Class

Public Class C
    Inherits B
    Implements IDisposable

    Public Overrides Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeSignatureResultAt(13, 26, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsOverridenAndSealed()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Public Overridable Sub Dispose()
    End Sub
End Class

Public Class C
    Inherits B
    Implements IDisposable

    Public NotOverridable Overrides Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Overloads Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        #endregion

        #region VB RenameDispose Unit Tests

        [Fact]
        public void Basic_CA1063_RenameDispose_Diagnostic_DisposeNamedD()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub D() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicRenameDisposeResultAt(7, 16, "C", "D"));
        }

        #endregion

        #region VB DisposeOverride Unit Tests

        [Fact]
        public void Basic_CA1063_DisposeOverride_Diagnostic_SimpleDisposeOverride()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

[|Public Class C
    Inherits B

    Public Overrides Sub Dispose()
    End Sub
End Class|]
",
            GetCA1063BasicDisposeOverrideResultAt(25, 26, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeOverride_Diagnostic_DoubleDisposeOverride()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

Public Class B
    Inherits A

    Public Overrides Sub Dispose()
    End Sub
End Class
    
[|Public Class C
    Inherits B

    Public Overrides Sub Dispose()
        Dispose(True)
    End Sub
End Class|]
",
            GetCA1063BasicDisposeOverrideResultAt(32, 26, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeOverride_Diagnostic_2DisposeImplementationsOverriden()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

Public Class B
    Inherits A
    Implements IDisposable

    Public Overridable Sub D() Implements IDisposable.Dispose
    End Sub
End Class
    
[|Public Class C
    Inherits B

    Public Overrides Sub Dispose()
        Dispose(True)
    End Sub

    Public Overrides Sub D()
        Dispose()
    End Sub
End Class|]
",
            GetCA1063BasicDisposeOverrideResultAt(33, 26, "C", "Dispose"),
            GetCA1063BasicDisposeOverrideResultAt(37, 26, "C", "D"));
        }

        #endregion

        #region VB FinalizeOverride Unit Tests

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void Basic_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class|]
",
            // Test0.vb(20,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063BasicFinalizeOverrideResultAt(20, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void Basic_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

Public Class B
    Inherits A

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class|]
",
            // Test0.vb(29,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063BasicFinalizeOverrideResultAt(29, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void Basic_CA1063_FinalizeOverride_NoDiagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasNoFinalizer()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class|]
");
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void Basic_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasFinalizer()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class|]
",
            // Test0.vb(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063BasicFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public void Basic_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride_InvokesDisposeBool()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class

Public Class B
    Inherits A

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class|]
",
            // Test0.vb(30,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063BasicFinalizeOverrideResultAt(30, 14, "C", "B"));
        }

        [Fact]
        public void Basic_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotInBaseType()
        {
            VerifyBasic(@"
Imports System

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

[|Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class|]
");
        }

        #endregion

        #region VB ProvideDisposeBool Unit Tests

        [Fact]
        public void Basic_CA1063_ProvideDisposeBool_Diagnostic_MissingDisposeBool()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Protected Overrides Sub Finalize()
    End Sub
End Class
",
            GetCA1063BasicProvideDisposeBoolResultAt(4, 14, "C"),
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"),
            GetCA1063BasicFinalizeImplementationResultAt(10, 29, "C", "Finalize"));
        }

        [Fact]
        public void Basic_CA1063_ProvideDisposeBool_Diagnostic_SealedClassAndMissingDisposeBool()
        {
            VerifyBasic(@"
Imports System

Public NotInheritable Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Protected Overrides Sub Finalize()
    End Sub
End Class
");
        }

        #endregion

        #region VB DisposeBoolSignature Unit Tests

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsPublic()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Public Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeBoolSignatureResultAt(17, 28, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsProtectedInternal()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Friend Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeBoolSignatureResultAt(17, 38, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsNotVirtual()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeBoolSignatureResultAt(17, 19, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsSealedOverriden()
        {
            VerifyBasic(@"
Imports System

Public MustInherit Class B
    Protected MustOverride Sub Dispose(disposing As Boolean)
End Class

Public Class C
    Inherits B
    Implements IDisposable

    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected NotOverridable Overrides Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeBoolSignatureResultAt(22, 44, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsOverriden()
        {
            VerifyBasic(@"
Imports System

Public MustInherit Class B
    Protected MustOverride Sub Dispose(disposing As Boolean)
End Class

Public Class C
    Inherits B
    Implements IDisposable

    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsAbstract()
        {
            VerifyBasic(@"
Imports System

Public MustInherit Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected MustOverride Sub Dispose(disposing As Boolean)

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPublicAndClassIsSealed()
        {
            VerifyBasic(@"
Imports System

Public NotInheritable Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Public Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPrivateAndClassIsSealed()
        {
            VerifyBasic(@"
Imports System

Public NotInheritable Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Private Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        #endregion

        #region VB DisposeImplementation Unit Tests

        [Fact]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"));
        }

        [Fact, WorkItem(1974, "https://github.com/dotnet/roslyn-analyzers/issues/1974")]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_MissingCallSuppressFinalize_HasFinalizer()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"));
        }

        [Fact, WorkItem(1974, "https://github.com/dotnet/roslyn-analyzers/issues/1974")]
        public void Basic_CA1063_DisposeImplementation_NoDiagnostic_MissingCallSuppressFinalize_NoFinalizer()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_EmptyDisposeBody()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_CallDisposeWithFalseArgument()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(False)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_ConditionalStatement()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Private disposed As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        If Not disposed Then
            Dispose(True)
            GC.SuppressFinalize(Me)
        End If
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(9, 16, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_NoDiagnostic_ConditionalStatement_Internal()
        {
            VerifyBasic(@"
Imports System

Friend Class C
    Implements IDisposable

    Private disposed As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        If Not disposed Then
            Dispose(True)
            GC.SuppressFinalize(Me)
        End If
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
");
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicDisposeImplementationResultAt(7, 16, "C", "Dispose"));
        }

        [Fact]
        public void Basic_CA1063_DisposeImplementation_NoDiagnostic_EmptyDisposeBodyInSealedClass()
        {
            VerifyBasic(@"
Imports System

Public NotInheritable Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub

End Class
");
        }

        #endregion

        #region VB FinalizeImplementation Unit Tests

        [Fact]
        public void Basic_CA1063_FinalizeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicFinalizeImplementationResultAt(12, 29, "C", "Finalize"));
        }

        [Fact]
        public void Basic_CA1063_FinalizeImplementation_Diagnostic_CallDisposeWithTrueArgument()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(True)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicFinalizeImplementationResultAt(12, 29, "C", "Finalize"));
        }

        [Fact]
        public void Basic_CA1063_FinalizeImplementation_Diagnostic_ConditionalStatement()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Private disposed As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        If Not disposed Then
            Dispose(False)
        End If
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicFinalizeImplementationResultAt(14, 29, "C", "Finalize"));
        }

        [Fact]
        public void Basic_CA1063_FinalizeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        Dispose(False)
        MyBase.Finalize()
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub

End Class
",
            GetCA1063BasicFinalizeImplementationResultAt(12, 29, "C", "Finalize"));
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCA1063CSharpIDisposableReimplementationResultAt(int line, int column, string typeName, string baseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageIDisposableReimplementation, typeName, baseTypeName);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicIDisposableReimplementationResultAt(int line, int column, string typeName, string baseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageIDisposableReimplementation, typeName, baseTypeName);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpDisposeSignatureResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeSignature, typeName + "." + disposeMethod);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicDisposeSignatureResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeSignature, typeName + "." + disposeMethod);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpRenameDisposeResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageRenameDispose, typeName + "." + disposeMethod);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicRenameDisposeResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageRenameDispose, typeName + "." + disposeMethod);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpDisposeOverrideResultAt(int line, int column, string typeName, string method)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeOverride, typeName + "." + method);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicDisposeOverrideResultAt(int line, int column, string typeName, string method)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeOverride, typeName + "." + method);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpFinalizeOverrideResultAt(int line, int column, string typeName, string baseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeOverride, typeName, baseTypeName);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicFinalizeOverrideResultAt(int line, int column, string typeName, string baseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeOverride, typeName, baseTypeName);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpProvideDisposeBoolResultAt(int line, int column, string typeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageProvideDisposeBool, typeName);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicProvideDisposeBoolResultAt(int line, int column, string typeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageProvideDisposeBool, typeName);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpDisposeBoolSignatureResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeBoolSignature, typeName + "." + disposeMethod);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicDisposeBoolSignatureResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeBoolSignature, typeName + "." + disposeMethod);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpDisposeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeImplementation, typeName + "." + disposeMethod);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicDisposeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeImplementation, typeName + "." + disposeMethod);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063CSharpFinalizeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture,
                MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeImplementation, typeName + "." +
                disposeMethod);
            return GetCSharpResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1063BasicFinalizeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture,
                MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeImplementation, typeName + "." +
                disposeMethod);
            return GetBasicResultAt(line, column, ImplementIDisposableCorrectlyAnalyzer.RuleId, message);
        }

        #endregion
    }
}
