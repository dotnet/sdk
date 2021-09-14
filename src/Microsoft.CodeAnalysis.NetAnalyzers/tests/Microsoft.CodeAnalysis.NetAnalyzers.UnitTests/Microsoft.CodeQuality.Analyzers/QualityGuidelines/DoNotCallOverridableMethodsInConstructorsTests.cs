// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotCallOverridableMethodsInConstructorsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotCallOverridableMethodsInConstructorsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class DoNotCallOverridableMethodsInConstructorsTests
    {
        [Fact]
        public async Task CA2214VirtualMethodCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    C()
    {
        SomeMethod();
    }

    protected virtual void SomeMethod() { }
}
",
            GetCA2214CSharpResultAt(6, 9));
        }

        [Fact]
        public async Task CA2214VirtualMethodCSharpWithScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    C()
    {
        [|SomeMethod()|];
    }

    protected virtual void SomeMethod() { }
}
");
        }

        [Fact]
        public async Task CA2214VirtualMethodBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Sub New()
        SomeMethod()
    End Sub
    Overridable Sub SomeMethod()
    End Sub
End Class
",
            GetCA2214BasicResultAt(4, 9));
        }

        [Fact]
        public async Task CA2214VirtualMethodBasicwithScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Sub New()
        [|SomeMethod()|]
    End Sub
    Overridable Sub SomeMethod()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2214AbstractMethodCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
abstract class C
{
    C()
    {
        SomeMethod();
    }

    protected abstract void SomeMethod();
}
",
            GetCA2214CSharpResultAt(6, 9));
        }

        [Fact]
        public async Task CA2214AbstractMethodBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
MustInherit Class C
    Public Sub New()
        SomeMethod()
    End Sub
    MustOverride Sub SomeMethod()
End Class
",
            GetCA2214BasicResultAt(4, 9));
        }

        [Fact]
        public async Task CA2214MultipleInstancesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
abstract class C
{
    C()
    {
        SomeMethod();
        SomeOtherMethod();
    }

    protected abstract void SomeMethod();
    protected virtual void SomeOtherMethod() { }
}
",
            GetCA2214CSharpResultAt(6, 9),
            GetCA2214CSharpResultAt(7, 9));
        }

        [Fact]
        public async Task CA2214MultipleInstancesBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
MustInherit Class C
    Public Sub New()
        SomeMethod()
        SomeOtherMethod()
    End Sub
    MustOverride Sub SomeMethod()
    Overridable Sub SomeOtherMethod()
    End Sub
End Class
",
           GetCA2214BasicResultAt(4, 9),
           GetCA2214BasicResultAt(5, 9));
        }

        [Fact]
        public async Task CA2214NotTopLevelCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
abstract class C
{
    C()
    {
        if (true)
        {
            SomeMethod();
        }

        if (false)
        {
            SomeMethod(); // also check unreachable code
        }
    }

    protected abstract void SomeMethod();
}
",
            GetCA2214CSharpResultAt(8, 13),
            GetCA2214CSharpResultAt(13, 13));
        }

        [Fact]
        public async Task CA2214NotTopLevelBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
MustInherit Class C
    Public Sub New()
        If True Then
            SomeMethod()
        End If

        If False Then
            SomeMethod() ' also check unreachable code
        End If
    End Sub
    MustOverride Sub SomeMethod()
End Class
",
            GetCA2214BasicResultAt(5, 13),
            GetCA2214BasicResultAt(9, 13));
        }

        [Fact]
        public async Task CA2214NoDiagnosticsOutsideConstructorCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
abstract class C
{
    protected abstract void SomeMethod();

    void Method()
    {
        SomeMethod();
    }
}
");
        }

        [Fact]
        public async Task CA2214NoDiagnosticsOutsideConstructorBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
MustInherit Class C
    MustOverride Sub SomeMethod()

    Sub Method()
        SomeMethod()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2214SpecialInheritanceCSharp_Web()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
abstract class C : System.Web.UI.Control
{
    C()
    {
        // no diagnostics because we inherit from System.Web.UI.Control
        SomeMethod();
        OnLoad(null);
    }

    protected abstract void SomeMethod();
}

abstract class F : System.ComponentModel.Component
{
    F()
    {
        // no diagnostics because we inherit from System.ComponentModel.Component
        SomeMethod();
    }

    protected abstract void SomeMethod();
}
"
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA2214SpecialInheritanceCSharp_WinForms()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWinForms,
                TestState =
                {
                    Sources =
                    {
                        @"
abstract class D : System.Windows.Forms.Control
{
    D()
    {
        // no diagnostics because we inherit from System.Windows.Forms.Control
        SomeMethod();
        OnPaint(null);
    }

    protected abstract void SomeMethod();
}

class ControlBase : System.Windows.Forms.Control
{
}

class E : ControlBase
{
    E()
    {
        OnGotFocus(null); // no diagnostics when we're not an immediate descendant of a special class
    }
}

abstract class F : System.ComponentModel.Component
{
    F()
    {
        // no diagnostics because we inherit from System.ComponentModel.Component
        SomeMethod();
    }

    protected abstract void SomeMethod();
}
"
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA2214SpecialInheritanceBasic_WinForms()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWinForms,
                TestState =
                {
                    Sources =
                    {
                        @"
MustInherit Class D
    Inherits System.Windows.Forms.Control
    Public Sub New()
        ' no diagnostics because we inherit from System.Windows.Forms.Control
        SomeMethod()
        OnPaint(Nothing)
    End Sub
    MustOverride Sub SomeMethod()
End Class

Class ControlBase
    Inherits System.Windows.Forms.Control
End Class

Class E
    Inherits ControlBase
    Public Sub New()
        OnGotFocus(Nothing) ' no diagnostics when we're not an immediate descendant of a special class
    End Sub
End Class

MustInherit Class F
    Inherits System.ComponentModel.Component
    Public Sub New()
        ' no diagnostics because we inherit from System.ComponentModel.Component
        SomeMethod()
    End Sub
    MustOverride Sub SomeMethod()
End Class
"
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA2214SpecialInheritanceBasic_Web()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
MustInherit Class C
    Inherits System.Web.UI.Control
    Public Sub New()
        ' no diagnostics because we inherit from System.Web.UI.Control
        SomeMethod()
        OnLoad(Nothing)
    End Sub
    MustOverride Sub SomeMethod()
End Class

MustInherit Class F
    Inherits System.ComponentModel.Component
    Public Sub New()
        ' no diagnostics because we inherit from System.ComponentModel.Component
        SomeMethod()
    End Sub
    MustOverride Sub SomeMethod()
End Class
"
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA2214VirtualOnOtherClassesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class D
{
    public virtual void SomeMethod() {}
}

class C
{
    public C(object obj, D d)
    {
        if (obj.Equals(d))
        {
            d.SomeMethod();
        }
    }
}
");
        }

        [Fact]
        public async Task CA2214VirtualOnOtherClassesBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class D
    Public Overridable Sub SomeMethod()
    End Sub
End Class

Class C
    Public Sub New(obj As Object, d As D)
        If obj.Equals(d) Then
            d.SomeMethod()
        End If
    End Sub
End Class
");
        }

        [Fact, WorkItem(1652, "https://github.com/dotnet/roslyn-analyzers/issues/1652")]
        public async Task CA2214VirtualInvocationsInLambdaCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

internal abstract class A
{
    private readonly Lazy<int> _lazyField;
    protected A()
    {
        _lazyField = new Lazy<int>(() => M());
    }

    protected abstract int M();
}
");
        }

        [Fact, WorkItem(1652, "https://github.com/dotnet/roslyn-analyzers/issues/1652")]
        public async Task CA2214VirtualInvocationsInLambdaBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend MustInherit Class A
    Private ReadOnly _lazyField As Lazy(Of Integer)

    Protected Sub New()
        _lazyField = New Lazy(Of Integer)(Function() M())
    End Sub

    Protected MustOverride Function M() As Integer
End Class
");
        }

        [Fact, WorkItem(4142, "https://github.com/dotnet/roslyn-analyzers/issues/4142")]
        public async Task CA2214_VirtualInvocationsInLambda()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    private readonly Lazy<Task> _initialization;

    protected C()
    {
        Task RunInit() => this.InitializeAsync(this.DisposeCts.Token);
        this._initialization = new Lazy<Task>(() => Task.Run(RunInit, this.DisposeCts.Token), isThreadSafe: true);
    }

    protected CancellationTokenSource DisposeCts { get; } = new CancellationTokenSource();

    protected Task Initialization => this._initialization.Value;

    protected virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Content doesn't matter
    }
}");
        }

        private static DiagnosticResult GetCA2214CSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA2214BasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}