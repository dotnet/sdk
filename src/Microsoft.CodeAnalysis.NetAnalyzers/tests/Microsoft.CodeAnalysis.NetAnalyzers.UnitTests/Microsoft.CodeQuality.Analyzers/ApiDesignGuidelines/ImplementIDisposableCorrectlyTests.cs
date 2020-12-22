
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
    public class ImplementIDisposableCorrectlyTests
    {
        #region CSharp Unit Tests

        [Fact]
        public async Task CSharp_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern_WithAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeSignature_NoDiagnostic_NotImplementingDisposable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class B : IDisposable
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
                // Test0.cs(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(4, 14, "B"),
                // Test0.cs(6,25): warning CA1063: Ensure that 'B.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(6, 25, "B", "Dispose"),
                // Test0.cs(6,25): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(6, 25, "B", "Dispose"),
                // Test0.cs(11,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063CSharpIDisposableReimplementationResultAt(11, 14, "C", "B"),
                // Test0.cs(13,26): warning CA1063: Ensure that 'C.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(13, 26, "C", "Dispose"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ReimplementingIDisposable_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

internal class B : IDisposable
{
    public virtual void Dispose()
    {
    }
}

internal class C : B, IDisposable
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
");
        }

        [Fact]
        public async Task CSharp_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposableWithDeepInheritance()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                // Test0.cs(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'A' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(4, 14, "A"),
                // Test0.cs(6,25): warning CA1063: Ensure that 'A.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(6, 25, "A", "Dispose"),
                // Test0.cs(6,25): warning CA1063: Modify 'A.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(6, 25, "A", "Dispose"),
                // Test0.cs(15,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063CSharpIDisposableReimplementationResultAt(15, 14, "C", "B"),
                // Test0.cs(17,26): warning CA1063: 'C.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(17, 26, "C", "Dispose"));
        }

        [Fact]
        public async Task CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInterfaceInheritedFromIDisposable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B, ITest
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
}
",
                // Test0.cs(9,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(9, 14, "B"),
                // Test0.cs(11,17): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(11, 17, "B", "Dispose"));
        }

        [Fact]
        public async Task CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ReImplementingIDisposableWithNoDisposeMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B, ITest, IDisposable
{
    public int Test { get; set; }
}
",
                // Test0.cs(9,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(9, 14, "B"),
                // Test0.cs(11,17): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(11, 17, "B", "Dispose"),
                // Test0.cs(16,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063CSharpIDisposableReimplementationResultAt(16, 14, "C", "B"));
        }

        [Fact]
        public async Task CSharp_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInheritedInterfaceWithNoDisposeReimplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B, ITest
{
    public int Test { get; set; }
}
",
                // Test0.cs(9,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(9, 14, "B"),
                // Test0.cs(11,17): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(11, 17, "B", "Dispose"));
        }

        #endregion

        #region CSharp DisposeSignature Unit Tests

        [Fact]
        public async Task CSharp_CA1063_DisposeSignature_Diagnostic_DisposeNotPublic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeSignature_Diagnostic_DisposeIsVirtual()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeSignature_Diagnostic_DisposeIsOverriden()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeSignature_NoDiagnostic_DisposeIsOverridenAndSealed()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeOverride_Diagnostic_SimpleDisposeOverride()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    public override void Dispose()
    {
    }
}
",
                // Test0.cs(6,25): warning CA1063: Ensure that 'B.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(6, 25, "B", "Dispose"),
                // Test0.cs(24,26): warning CA1063: Remove 'C.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063CSharpDisposeOverrideResultAt(24, 26, "C", "Dispose"));
        }

        [Fact]
        public async Task CSharp_CA1063_DisposeOverride_Diagnostic_DoubleDisposeOverride()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    public override void Dispose()
    {
        Dispose(true);
    }
}
",
                // Test0.cs(6,25): warning CA1063: Ensure that 'A.Dispose' is declared as public and sealed.
                GetCA1063CSharpDisposeSignatureResultAt(6, 25, "A", "Dispose"),
                // Test0.cs(24,26): warning CA1063: Remove 'B.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063CSharpDisposeOverrideResultAt(24, 26, "B", "Dispose"),
                // Test0.cs(31,26): warning CA1063: Remove 'C.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063CSharpDisposeOverrideResultAt(31, 26, "C", "Dispose"));
        }

        #endregion

        #region CSharp FinalizeOverride Unit Tests

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_OverridesDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
    }
}
",
            GetCA1063CSharpFinalizeImplementationResultAt(24, 6, "C", "Finalize"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    ~C()
    {
    }
}
",
            // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    ~C()
    {
    }
}
",
                // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'B', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'A' also provides a finalizer.
                GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "B", "A"),
                // Test0.cs(29,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
                GetCA1063CSharpFinalizeOverrideResultAt(29, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_NoDiagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasNoFinalizer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}
");
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasFinalizer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}
",
            // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride_InvokesDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
    
    ~C()
    {
        Dispose(false);
    }
}
",
                // Test0.cs(22,14): warning CA1063: Remove the finalizer from type 'B', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'A' also provides a finalizer.
                GetCA1063CSharpFinalizeOverrideResultAt(22, 14, "B", "A"),
                // Test0.cs(35,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
                GetCA1063CSharpFinalizeOverrideResultAt(35, 14, "C", "B"));
        }

        [Fact]
        public async Task CSharp_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotInBaseType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class B : IDisposable
{
    public void Dispose()
    {
    }
}

public class C : B
{
    ~C()
    {
    }
}
",
                // Test0.cs(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063CSharpProvideDisposeBoolResultAt(4, 14, "B"),
                // Test0.cs(6,17): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063CSharpDisposeImplementationResultAt(6, 17, "B", "Dispose"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task CSharp_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotOverriden()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

public class C : B
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
");
        }
        #endregion

        #region CSharp ProvideDisposeBool Unit Tests

        [Fact]
        public async Task CSharp_CA1063_ProvideDisposeBool_Diagnostic_MissingDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_ProvideDisposeBool_NoDiagnostic_SealedClassAndMissingDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsPublic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsProtectedInternal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsNotVirtual()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsSealedOverriden()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsOverriden()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsAbstract()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPublicAndClassIsSealed()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPrivateAndClassIsSealed()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsStatic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_MissingCallSuppressFinalize_HasFinalizer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_NoDiagnostic_MissingCallSuppressFinalize_NoFinalizer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_EmptyDisposeBody()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_CallDisposeWithFalseArgument()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_ConditionalStatement()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_NoDiagnostic_ConditionalStatement_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_DisposeImplementation_NoDiagnostic_EmptyDisposeBodyInSealedClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_FinalizeImplementation_NoDiagnostic_ExpressionBodiedImpl()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_FinalizeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_FinalizeImplementation_Diagnostic_CallDisposeWithTrueArgument()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_FinalizeImplementation_Diagnostic_ConditionalStatement()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1063_FinalizeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_NoDiagnostic_GoodDisposablePattern()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_NoDiagnostic_NotImplementingDisposable()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposable()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class B
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
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
                // Test0.vb(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(4, 14, "B"),
                // Test0.vb(7,28): warning CA1063: Ensure that 'B.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(7, 28, "B", "Dispose"),
                // Test0.vb(7,28): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(7, 28, "B", "Dispose"),
                // Test0.vb(11,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063BasicIDisposableReimplementationResultAt(11, 14, "C", "B"),
                // Test0.vb(15,26): warning CA1063: Ensure that 'C.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(15, 26, "C", "Dispose"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ReimplementingIDisposable_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Class B
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Friend Class C
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
");
        }

        [Fact]
        public async Task Basic_CA1063_IDisposableReimplementation_Diagnostic_ReimplementingIDisposableWithDeepInheritance()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Implements IDisposable

    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Public Class B
    Inherits A
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
                // Test0.vb(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'A' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(4, 14, "A"),
                // Test0.vb(7,28): warning CA1063: Ensure that 'A.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(7, 28, "A", "Dispose"),
                // Test0.vb(7,28): warning CA1063: Modify 'A.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(7, 28, "A", "Dispose"),
                // Test0.vb(15,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063BasicIDisposableReimplementationResultAt(15, 14, "C", "B"),
                // Test0.vb(19,26): warning CA1063: Ensure that 'C.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(19, 26, "C", "Dispose"));
        }

        [Fact]
        public async Task Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInterfaceInheritedFromIDisposable()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
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

End Class
",
                // Test0.vb(10,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(10, 14, "B"),
                // Test0.vb(13,16): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(13, 16, "B", "Dispose"));
        }

        [Fact]
        public async Task Basic_CA1063_IDisposableReimplementation_Diagnostic_ReImplementingIDisposableWithNoDisposeMethod()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public NotInheritable Class C
    Inherits B
    Implements ITest
    Implements IDisposable

    Public Property Test As Integer Implements ITest.Test

End Class
",
                // Test0.vb(10,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(10, 14, "B"),
                // Test0.vb(13,16): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(13, 16, "B", "Dispose"),
                // Test0.vb(17,29): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'C' as it is already implemented by base type 'B'.
                GetCA1063BasicIDisposableReimplementationResultAt(17, 29, "C", "B"));
        }

        [Fact]
        public async Task Basic_CA1063_IDisposableReimplementation_NoDiagnostic_ImplementingInheritedInterfaceWithNoDisposeReimplementation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public NotInheritable Class C
    Inherits B
    Implements ITest

    Public Property Test As Integer Implements ITest.Test

End Class
",
                // Test0.vb(10,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(10, 14, "B"),
                // Test0.vb(13,16): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(13, 16, "B", "Dispose"));
        }

        #endregion

        #region VB DisposeSignature Unit Tests

        [Fact]
        public async Task Basic_CA1063_DisposeSignature_Diagnostic_DisposeProtected()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_Diagnostic_DisposePrivate()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsVirtual()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsOverriden()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeSignature_Diagnostic_DisposeIsOverridenAndSealed()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_RenameDispose_Diagnostic_DisposeNamedD()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeOverride_Diagnostic_SimpleDisposeOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Public Overrides Sub Dispose()
    End Sub
End Class
",
                // Test0.vb(7,28): warning CA1063: Ensure that 'B.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(7, 28, "B", "Dispose"),
                // Test0.vb(25,26): warning CA1063: Remove 'C.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063BasicDisposeOverrideResultAt(25, 26, "C", "Dispose"));
        }

        [Fact]
        public async Task Basic_CA1063_DisposeOverride_Diagnostic_DoubleDisposeOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
    
Public Class C
    Inherits B

    Public Overrides Sub Dispose()
        Dispose(True)
    End Sub
End Class
",
                // Test0.vb(7,28): warning CA1063: Ensure that 'A.Dispose' is declared as public and sealed.
                GetCA1063BasicDisposeSignatureResultAt(7, 28, "A", "Dispose"),
                // Test0.vb(25,26): warning CA1063: Remove 'B.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063BasicDisposeOverrideResultAt(25, 26, "B", "Dispose"),
                // Test0.vb(32,26): warning CA1063: Remove 'C.Dispose', override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
                GetCA1063BasicDisposeOverrideResultAt(32, 26, "C", "Dispose"));
        }

        [Fact]
        public async Task Basic_CA1063_DisposeOverride_Diagnostic_2DisposeImplementationsOverriden()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
    
Public Class C
    Inherits B

    Public Overrides Sub Dispose()
        Dispose(True)
    End Sub

    Public Overrides Sub D()
        Dispose()
    End Sub
End Class
",
            // Test0.vb(7,28): warning CA1063: Ensure that 'A.Dispose' is declared as public and sealed.
            GetCA1063BasicDisposeSignatureResultAt(7, 28, "A", "Dispose"),
            // Test0.vb(22,14): warning CA1063: Remove IDisposable from the list of interfaces implemented by 'B' as it is already implemented by base type 'A'.
            GetCA1063BasicIDisposableReimplementationResultAt(22, 14, "B", "A"),
            // Test0.vb(22,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
            GetCA1063BasicProvideDisposeBoolResultAt(22, 14, "B"),
            // Test0.vb(26,28): warning CA1063: Ensure that 'B.D' is declared as public and sealed.
            GetCA1063BasicDisposeSignatureResultAt(26, 28, "B", "D"),
            // Test0.vb(26,28): warning CA1063: Rename 'B.D' to 'Dispose' and ensure that it is declared as public and sealed.
            GetCA1063BasicRenameDisposeResultAt(26, 28, "B", "D"),
            // Test0.vb(33,26): warning CA1063: Remove 'C.Dispose', override Dispose(bool disposing) and put the dispose logic in the code path where 'disposing' is true.
            GetCA1063BasicDisposeOverrideResultAt(33, 26, "C", "Dispose"),
            // Test0.vb(37,26): warning CA1063: Remove 'C.D', override Dispose(bool disposing) and put the dispose logic in the code path where 'disposing' is true.
            GetCA1063BasicDisposeOverrideResultAt(37, 26, "C", "D"));
        }

        #endregion

        #region VB FinalizeOverride Unit Tests

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task Basic_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
",
                // Test0.vb(12,29): warning CA1063: Modify 'B.Finalize' so that it calls Dispose(false) and then returns.
                GetCA1063BasicFinalizeImplementationResultAt(12, 29, "B", "Finalize"),
                // Test0.vb(20,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
                GetCA1063BasicFinalizeOverrideResultAt(20, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task Basic_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
",
                // Test0.vb(12,29): warning CA1063: Modify 'A.Finalize' so that it calls Dispose(false) and then returns.
                GetCA1063BasicFinalizeImplementationResultAt(12, 29, "A", "Finalize"),
                // Test0.vb(20,14): warning CA1063: Remove the finalizer from type 'B', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'A' also provides a finalizer.
                GetCA1063BasicFinalizeOverrideResultAt(20, 14, "B", "A"),
                // Test0.vb(29,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
                GetCA1063BasicFinalizeOverrideResultAt(29, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task Basic_CA1063_FinalizeOverride_NoDiagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasNoFinalizer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class
");
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task Basic_CA1063_FinalizeOverride_Diagnostic_SimpleFinalizeOverride_InvokesDisposeBool_BaseTypeHasFinalizer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class
",
            // Test0.vb(22,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
            GetCA1063BasicFinalizeOverrideResultAt(22, 14, "C", "B"));
        }

        [Fact, WorkItem(1950, "https://github.com/dotnet/roslyn-analyzers/issues/1950")]
        public async Task Basic_CA1063_FinalizeOverride_Diagnostic_DoubleFinalizeOverride_InvokesDisposeBool()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
End Class
",
                // Test0.vb(22,14): warning CA1063: Remove the finalizer from type 'B', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'A' also provides a finalizer.
                GetCA1063BasicFinalizeOverrideResultAt(22, 14, "B", "A"),
                // Test0.vb(30,14): warning CA1063: Remove the finalizer from type 'C', override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type 'B' also provides a finalizer.
                GetCA1063BasicFinalizeOverrideResultAt(30, 14, "C", "B"));
        }

        [Fact]
        public async Task Basic_CA1063_FinalizeOverride_NoDiagnostic_FinalizeNotInBaseType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class B
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Public Class C
    Inherits B

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
",
                // Test0.vb(4,14): warning CA1063: Provide an overridable implementation of Dispose(bool) on 'B' or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
                GetCA1063BasicProvideDisposeBoolResultAt(4, 14, "B"),
                // Test0.vb(7,16): warning CA1063: Modify 'B.Dispose' so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
                GetCA1063BasicDisposeImplementationResultAt(7, 16, "B", "Dispose"));
        }

        #endregion

        #region VB ProvideDisposeBool Unit Tests

        [Fact]
        public async Task Basic_CA1063_ProvideDisposeBool_Diagnostic_MissingDisposeBool()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_ProvideDisposeBool_Diagnostic_SealedClassAndMissingDisposeBool()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsPublic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsProtectedInternal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsNotVirtual()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_Diagnostic_DisposeBoolIsSealedOverriden()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsOverriden()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsAbstract()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPublicAndClassIsSealed()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeBoolSignature_NoDiagnostic_DisposeBoolIsPrivateAndClassIsSealed()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_MissingCallSuppressFinalize_HasFinalizer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_NoDiagnostic_MissingCallSuppressFinalize_NoFinalizer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_EmptyDisposeBody()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_CallDisposeWithFalseArgument()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_ConditionalStatement()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_NoDiagnostic_ConditionalStatement_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_DisposeImplementation_NoDiagnostic_EmptyDisposeBodyInSealedClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_FinalizeImplementation_Diagnostic_MissingCallDisposeBool()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_FinalizeImplementation_Diagnostic_CallDisposeWithTrueArgument()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_FinalizeImplementation_Diagnostic_ConditionalStatement()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1063_FinalizeImplementation_Diagnostic_CallDisposeBoolTwice()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.IDisposableReimplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, baseTypeName);

        private static DiagnosticResult GetCA1063BasicIDisposableReimplementationResultAt(int line, int column, string typeName, string baseTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.IDisposableReimplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, baseTypeName);

        private static DiagnosticResult GetCA1063CSharpDisposeSignatureResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeSignatureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{ typeName}.{ disposeMethod}");

        private static DiagnosticResult GetCA1063BasicDisposeSignatureResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeSignatureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{ typeName}.{ disposeMethod}");

        private static DiagnosticResult GetCA1063CSharpRenameDisposeResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.RenameDisposeRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{ typeName}.{ disposeMethod}");

        private static DiagnosticResult GetCA1063BasicRenameDisposeResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.RenameDisposeRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063CSharpDisposeOverrideResultAt(int line, int column, string typeName, string method)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeOverrideRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{ typeName}.{method}");

        private static DiagnosticResult GetCA1063BasicDisposeOverrideResultAt(int line, int column, string typeName, string method)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeOverrideRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{ typeName}.{method}");

        private static DiagnosticResult GetCA1063CSharpFinalizeOverrideResultAt(int line, int column, string typeName, string baseTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.FinalizeOverrideRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, baseTypeName);

        private static DiagnosticResult GetCA1063BasicFinalizeOverrideResultAt(int line, int column, string typeName, string baseTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.FinalizeOverrideRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, baseTypeName);

        private static DiagnosticResult GetCA1063CSharpProvideDisposeBoolResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.ProvideDisposeBoolRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCA1063BasicProvideDisposeBoolResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.ProvideDisposeBoolRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCA1063CSharpDisposeBoolSignatureResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeBoolSignatureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063BasicDisposeBoolSignatureResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeBoolSignatureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063CSharpDisposeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeImplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063BasicDisposeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.DisposeImplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063CSharpFinalizeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.FinalizeImplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        private static DiagnosticResult GetCA1063BasicFinalizeImplementationResultAt(int line, int column, string typeName, string disposeMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(ImplementIDisposableCorrectlyAnalyzer.FinalizeImplementationRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments($"{typeName}.{disposeMethod}");

        #endregion
    }
}
