// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;

using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpAvoidUninstantiatedInternalClasses,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpAvoidUninstantiatedInternalClassesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicAvoidUninstantiatedInternalClasses,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicAvoidUninstantiatedInternalClassesFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class AvoidUninstantiatedInternalClassesTests
    {
        [Fact]
        public async Task CA1812_CSharp_Diagnostic_UninstantiatedInternalClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C { }
",
                GetCSharpResultAt(1, 16, "C"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_UninstantiatedInternalClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
End Class",
                GetBasicResultAt(1, 14, "C"));
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_UninstantiatedInternalStructAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal struct CInternal { }");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_UninstantiatedInternalStructAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Structure CInternal
End Structure");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_UninstantiatedPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"public class C { }");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_UninstantiatedPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Public Class C
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InstantiatedInternalClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C { }

public class D
{
    private readonly C _c = new C();
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InstantiatedInternalClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
End Class

Public Class D
     Private _c As New C
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_Diagnostic_UninstantiatedInternalClassNestedInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"public class C
{
    internal class D { }
}",
                GetCSharpResultAt(3, 20, "C.D"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_UninstantiatedInternalClassNestedInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Public Class C
    Friend Class D
    End Class
End Class",
                GetBasicResultAt(2, 18, "C.D"));
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InstantiatedInternalClassNestedInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"public class C
{
    private readonly D _d = new D();

    internal class D { }
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InstantiatedInternalClassNestedInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Public Class C
    Private ReadOnly _d = New D

    Friend Class D
    End Class
End Class");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InternalModuleAsync()
        {
            // No static classes in VB.
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Module M
End Module");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InternalAbstractClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal abstract class A { }");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InternalAbstractClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend MustInherit Class A
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InternalDelegateAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    internal delegate void Del();
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InternalDelegateAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace N
    Friend Delegate Sub Del()
End Namespace");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InternalEnumAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"namespace N
{
    internal enum E {}  // C# enums don't care if there are any members.
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InternalEnumAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Namespace N
    Friend Enum E
        None            ' VB enums require at least one member.
    End Enum
End Namespace");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_AttributeClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System;

internal class MyAttribute: Attribute {}
internal class MyOtherAttribute: MyAttribute {}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_AttributeClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System

Friend Class MyAttribute
    Inherits Attribute
End Class

Friend Class MyOtherAttribute
    Inherits MyAttribute
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_TypeContainingAssemblyEntryPointReturningVoidAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@"internal class C
{
    private static void Main() {}
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_TypeContainingAssemblyEntryPointReturningVoidAsync()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@"Friend Class C
    Public Shared Sub Main()
    End Sub
End Class",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_TypeContainingAssemblyEntryPointReturningIntAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@"internal class C
{
    private static int Main() { return 1; }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_TypeContainingAssemblyEntryPointReturningIntAsync()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@"Friend Class C
    Public Shared Function Main() As Integer
        Return 1
    End Function
End Class",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_TypeContainingAssemblyEntryPointReturningTaskAsync()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp7_1,
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@" using System.Threading.Tasks;
internal static class C
{
    private static async Task Main() { await Task.Delay(1); }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_TypeContainingAssemblyEntryPointReturningTaskIntAsync()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp7_1,
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@" using System.Threading.Tasks;
internal static class C
{
    private static async Task<int> Main() { await Task.Delay(1); return 1; }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1812_CSharp_Diagnostic_MainMethodIsNotStaticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C
{
    private void Main() {}
}",
                GetCSharpResultAt(1, 16, "C"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_MainMethodIsNotStaticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
    Private Sub Main()
    End Sub
End Class",
                GetBasicResultAt(1, 14, "C"));
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_MainMethodIsDifferentlyCasedAsync()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
@"Friend Class C
    Private Shared Sub mAiN()
    End Sub
End Class",
                    },
                },
                ExpectedDiagnostics =
                {
                     // error BC30737: No accessible 'Main' method with an appropriate signature was found in 'TestProject'.
                     DiagnosticResult.CompilerError("BC30737"),
                }
            }.RunAsync();
        }

        // The following tests are just to ensure that the messages are formatted properly
        // for types within namespaces.
        [Fact]
        public async Task CA1812_CSharp_Diagnostic_UninstantiatedInternalClassInNamespaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"namespace N
{
    internal class C { }
}",
                GetCSharpResultAt(3, 20, "C"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_UninstantiatedInternalClassInNamespaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Namespace N
    Friend Class C
    End Class
End Namespace",
                GetBasicResultAt(2, 18, "C"));
        }

        [Fact]
        public async Task CA1812_CSharp_Diagnostic_UninstantiatedInternalClassNestedInPublicClassInNamespaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"namespace N
{
    public class C
    {
        internal class D { }
    }
}",
                GetCSharpResultAt(5, 24, "C.D"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_UninstantiatedInternalClassNestedInPublicClassInNamespaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Namespace N
    Public Class C
        Friend Class D
        End Class
    End Class
End Namespace",
                GetBasicResultAt(3, 22, "C.D"));
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_UninstantiatedInternalMef1ExportedClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System;
using System.ComponentModel.Composition;

namespace System.ComponentModel.Composition
{
    public class ExportAttribute: Attribute
    {
    }
}

[Export]
internal class C
{
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_UninstantiatedInternalMef1ExportedClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System
Imports System.ComponentModel.Composition

Namespace System.ComponentModel.Composition
    Public Class ExportAttribute
        Inherits Attribute
    End Class
End Namespace

<Export>
Friend Class C
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_UninstantiatedInternalMef2ExportedClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System;
using System.ComponentModel.Composition;

namespace System.ComponentModel.Composition
{
    public class ExportAttribute: Attribute
    {
    }
}

[Export]
internal class C
{
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_UninstantiatedInternalMef2ExportedClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System
Imports System.ComponentModel.Composition

Namespace System.ComponentModel.Composition
    Public Class ExportAttribute
        Inherits Attribute
    End Class
End Namespace

<Export>
Friend Class C
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_ImplementsIConfigurationSectionHandlerAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System.Configuration;
using System.Xml;

internal class C : IConfigurationSectionHandler
{
    public object Create(object parent, object configContext, XmlNode section)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_ImplementsIConfigurationSectionHandlerAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System.Configuration
Imports System.Xml

Friend Class C
    Implements IConfigurationSectionHandler
    Private Function IConfigurationSectionHandler_Create(parent As Object, configContext As Object, section As XmlNode) As Object Implements IConfigurationSectionHandler.Create
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_DerivesFromConfigurationSectionAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System.Configuration;

namespace System.Configuration
{
    public class ConfigurationSection
    {
    }
}

internal class C : ConfigurationSection
{
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_DerivesFromConfigurationSectionAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System.Configuration

Namespace System.Configuration
    Public Class ConfigurationSection
    End Class
End Namespace

Friend Class C
    Inherits ConfigurationSection
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_DerivesFromSafeHandleAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System;
using System.Runtime.InteropServices;

internal class MySafeHandle : SafeHandle
{
    protected MySafeHandle(IntPtr invalidHandleValue, bool ownsHandle)
        : base(invalidHandleValue, ownsHandle)
    {
    }

    public override bool IsInvalid => true;

    protected override bool ReleaseHandle()
    {
        return true;
    }
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_DerivesFromSafeHandleAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System
Imports System.Runtime.InteropServices

Friend Class MySafeHandle
    Inherits SafeHandle

    Protected Sub New(invalidHandleValue As IntPtr, ownsHandle As Boolean)
        MyBase.New(invalidHandleValue, ownsHandle)
    End Sub

    Public Overrides ReadOnly Property IsInvalid As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_DerivesFromTraceListenerAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System.Diagnostics;

internal class MyTraceListener : TraceListener
{
    public override void Write(string message) { }
    public override void WriteLine(string message) { }
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_DerivesFromTraceListenerAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System.Diagnostics

Friend Class MyTraceListener
    Inherits TraceListener

    Public Overrides Sub Write(message As String)
    End Sub

    Public Overrides Sub WriteLine(message As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_InternalNestedTypeIsInstantiatedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C
{
    internal class C2
    {
    } 
}

public class D
{
    private readonly C.C2 _c2 = new C.C2();
}
");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_InternalNestedTypeIsInstantiatedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
    Friend Class C2
    End Class
End Class

Public Class D
    Private _c2 As new C.C2
End Class");
        }

        [Fact]
        public async Task CA1812_CSharp_Diagnostic_InternalNestedTypeIsNotInstantiatedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C
{
    internal class C2
    {
    } 
}",
                GetCSharpResultAt(3, 20, "C.C2"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_InternalNestedTypeIsNotInstantiatedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
    Friend Class C2
    End Class
End Class",
                GetBasicResultAt(2, 18, "C.C2"));
        }

        [Fact]
        public async Task CA1812_CSharp_Diagnostic_PrivateNestedTypeIsInstantiatedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal class C
{
    private readonly C2 _c2 = new C2();
    private class C2
    {
    } 
}",
                GetCSharpResultAt(1, 16, "C"));
        }

        [Fact]
        public async Task CA1812_Basic_Diagnostic_PrivateNestedTypeIsInstantiatedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Class C
    Private _c2 As New C2
    
    Private Class C2
    End Class
End Class",
                GetBasicResultAt(1, 14, "C"));
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_StaticHolderClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"internal static class C
{
    internal static void F() { }
}");
        }

        [Fact, WorkItem(1370, "https://github.com/dotnet/roslyn-analyzers/issues/1370")]
        public async Task CA1812_CSharp_NoDiagnostic_ImplicitlyInstantiatedFromSubTypeConstructorAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
internal class A
{
    public A()
    {
    }
}

internal class B : A
{
    public B()
    {
    }
}

internal class C<T>
{
}

internal class D : C<int>
{
    static void M()
    {
        var x = new B();
        var y = new D();
    }
}");
        }

        [Fact, WorkItem(1370, "https://github.com/dotnet/roslyn-analyzers/issues/1370")]
        public async Task CA1812_CSharp_NoDiagnostic_ExplicitlyInstantiatedFromSubTypeConstructorAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
internal class A
{
    public A(int x)
    {
    }
}

internal class B : A
{
    public B(int x): base (x)
    {
    }
}

internal class C<T>
{
}

internal class D : C<int>
{
    public D(): base()
    {
    }

    static void M()
    {
        var x = new B(0);
        var y = new D();
    }
}");
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_StaticHolderClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Friend Module C
    Friend Sub F()
    End Sub
End Module");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_EmptyInternalStaticClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("internal static class S { }");
        }

        [Fact]
        public async Task CA1812_CSharp_NoDiagnostic_UninstantiatedInternalClassInFriendlyAssemblyAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""TestProject"")]

internal class C { }"
                );
        }

        [Fact]
        public async Task CA1812_Basic_NoDiagnostic_UninstantiatedInternalClassInFriendlyAssemblyAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"Imports System.Runtime.CompilerServices

<Assembly: InternalsVisibleToAttribute(""TestProject"")>

Friend Class C
End Class"
                );
        }

        [Fact, WorkItem(1370, "https://github.com/dotnet/roslyn-analyzers/issues/1370")]
        public async Task CA1812_Basic_NoDiagnostic_ImplicitlyInstantiatedFromSubTypeConstructorAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"
Friend Class A
    Public Sub New()
    End Sub
End Class

Friend Class B
    Inherits A
    Public Sub New()
    End Sub
End Class

Friend Class C(Of T)
End Class

Friend Class D
    Inherits C(Of Integer)
    Private Shared Sub M()
        Dim x = New B()
        Dim y = New D()
    End Sub
End Class");
        }

        [Fact, WorkItem(1370, "https://github.com/dotnet/roslyn-analyzers/issues/1370")]
        public async Task CA1812_Basic_NoDiagnostic_ExplicitlyInstantiatedFromSubTypeConstructorAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
@"
Friend Class A
    Public Sub New(ByVal x As Integer)
    End Sub
End Class

Friend Class B
    Inherits A

    Public Sub New(ByVal x As Integer)
        MyBase.New(x)
    End Sub
End Class

Friend Class C(Of T)
End Class

Friend Class D
    Inherits C(Of Integer)
    Public Sub New()
        MyBase.New()
    End Sub

    Private Shared Sub M()
        Dim x = New B(0)
        Dim y = New D()
    End Sub
End Class");
        }

        [Fact, WorkItem(1154, "https://github.com/dotnet/roslyn-analyzers/issues/1154")]
        public async Task CA1812_CSharp_GenericInternalClass_InstanciatedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;

public static class X
{
    public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source, Comparison<T> compare)
    {
        return source.OrderBy(new ComparisonComparer<T>(compare));
    }

    public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source, IComparer<T> comparer)
    {
        return source.OrderBy(t => t, comparer);
    }

    private class ComparisonComparer<T> : Comparer<T>
    {
        private readonly Comparison<T> _compare;

        public ComparisonComparer(Comparison<T> compare)
        {
            _compare = compare;
        }

        public override int Compare(T x, T y)
        {
            return _compare(x, y);
        }
    }
}
");
        }

        [Fact, WorkItem(1154, "https://github.com/dotnet/roslyn-analyzers/issues/1154")]
        public async Task CA1812_Basic_GenericInternalClass_InstanciatedNoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Module M
    <Extension()>
    Public Function OrderBy(Of T)(ByVal source As IEnumerable(Of T), compare As Comparison(Of T)) As IEnumerable(Of T)
        Return source.OrderBy(New ComparisonCompare(Of T)(compare))
    End Function

    <Extension()>
    Public Function OrderBy(Of T)(ByVal source As IEnumerable(Of T), comparer As IComparer(Of T)) As IEnumerable(Of T)
        Return source.OrderBy(Function(i) i, comparer)
    End Function

    Private Class ComparisonCompare(Of T)
        Inherits Comparer(Of T)

        Private _compare As Comparison(Of T)

        Public Sub New(compare As Comparison(Of T))
            _compare = compare
        End Sub

        Public Overrides Function Compare(x As T, y As T) As Integer
            Throw New NotImplementedException()
        End Function
    End Class
End Module
");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_CSharp_NoDiagnostic_GenericMethodWithNewConstraintAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

internal class InstantiatedType
{
}

internal static class Factory
{
    internal static T Create<T>()
        where T : new()
    {
        return new T();
    }
}

internal class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(Factory.Create<InstantiatedType>());
    }
}");
        }

        [Fact, WorkItem(1447, "https://github.com/dotnet/roslyn-analyzers/issues/1447")]
        public async Task CA1812_CSharp_NoDiagnostic_GenericMethodWithNewConstraintInvokedFromGenericMethodAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class InstantiatedClass
{
    public InstantiatedClass()
    {
    }
}

internal class InstantiatedClass2
{
    public InstantiatedClass2()
    {
    }
}

internal class InstantiatedClass3
{
    public InstantiatedClass3()
    {
    }
}

internal static class C
{
    private static T Create<T>()
        where T : new()
    {
        return new T();
    }

    public static void M<T>()
        where T : InstantiatedClass, new()
    {
        Create<T>();
    }

    public static void M2<T, T2>()
        where T : T2, new()
        where T2 : InstantiatedClass2
    {
        Create<T>();
    }

    public static void M3<T, T2, T3>()
        where T : T2, new()
        where T2 : T3
        where T3: InstantiatedClass3
    {
        Create<T>();
    }

    public static void M3()
    {
        M<InstantiatedClass>();
        M2<InstantiatedClass2, InstantiatedClass2>();
        M3<InstantiatedClass3, InstantiatedClass3, InstantiatedClass3>();
    }
}");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_Basic_NoDiagnostic_GenericMethodWithNewConstraintAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Module1
    Sub Main()
        Console.WriteLine(Create(Of InstantiatedType)())
    End Sub

    Friend Class InstantiatedType
    End Class

    Friend Function Create(Of T As New)() As T
        Return New T
    End Function
End Module");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_CSharp_NoDiagnostic_GenericTypeWithNewConstraintAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class InstantiatedType
{
}

internal class Factory<T> where T : new()
{
}

internal class Program
{
    public static void Main(string[] args)
    {
        var factory = new Factory<InstantiatedType>();
    }
}");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_Basic_NoDiagnostic_GenericTypeWithNewConstraintAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Module1
    Sub Main()
        Console.WriteLine(New Factory(Of InstantiatedType))
    End Sub

    Friend Class InstantiatedType
    End Class

    Friend Class Factory(Of T As New)
    End Class
End Module");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_CSharp_Diagnostic_NestedGenericTypeWithNoNewConstraintAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

internal class InstantiatedType
{
}

internal class Factory<T> where T : new()
{
}

internal class Program
{
    public static void Main(string[] args)
    {
        var list = new List<Factory<InstantiatedType>>();
    }
}",
                GetCSharpResultAt(4, 16, "InstantiatedType"),
                GetCSharpResultAt(8, 16, "Factory<T>"));
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_Basic_Diagnostic_NestedGenericTypeWithNoNewConstraintAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Module Library
    Friend Class InstantiatedType
    End Class

    Friend Class Factory(Of T As New)
    End Class

    Sub Main()
        Dim a = New List(Of Factory(Of InstantiatedType))
    End Sub
End Module",
                GetBasicResultAt(5, 18, "Library.InstantiatedType"),
                GetBasicResultAt(8, 18, "Library.Factory(Of T)"));
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_CSharp_NoDiagnostic_NestedGenericTypeWithNewConstraintAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

internal class InstantiatedType
{
}

internal class Factory1<T> where T : new()
{
}

internal class Factory2<T> where T : new()
{
}

internal class Program
{
    public static void Main(string[] args)
    {
        var factory = new Factory1<Factory2<InstantiatedType>>();
    }
}");
        }

        [Fact, WorkItem(1158, "https://github.com/dotnet/roslyn-analyzers/issues/1158")]
        public async Task CA1812_Basic_NoDiagnostic_NestedGenericTypeWithNewConstraintAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Module Library
    Friend Class InstantiatedType
    End Class

    Friend Class Factory1(Of T As New)
    End Class

    Friend Class Factory2(Of T As New)
    End Class

    Sub Main()
        Dim a = New Factory1(Of Factory2(Of InstantiatedType))
    End Sub
End Module");
        }

        [Fact, WorkItem(1739, "https://github.com/dotnet/roslyn-analyzers/issues/1739")]
        public async Task CA1812_CSharp_NoDiagnostic_GenericTypeWithRecursiveConstraintAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public abstract class JobStateBase<TState>
    where TState : JobStateBase<TState>, new()
{
    public void SomeFunction ()
    {
        new JobStateChangeHandler<TState>();
    }
}

public class JobStateChangeHandler<TState>
    where TState : JobStateBase<TState>, new()
{
}
");
        }

        [Fact, WorkItem(2751, "https://github.com/dotnet/roslyn-analyzers/issues/2751")]
        public async Task CA1812_CSharp_NoDiagnostic_TypeDeclaredInCoClassAttributeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[CoClass(typeof(CSomeClass))]
internal interface ISomeInterface {}

internal class CSomeClass {}
");
        }

        [Fact, WorkItem(2751, "https://github.com/dotnet/roslyn-analyzers/issues/2751")]
        public async Task CA1812_CSharp_DontFailOnInvalidCoClassUsagesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[{|CS7036:CoClass|}]
internal interface ISomeInterface1 {}

[CoClass({|CS0119:CSomeClass|})]
internal interface ISomeInterface2 {}

[{|CS1729:CoClass(typeof(CSomeClass), null)|}]
internal interface ISomeInterface3 {}

[CoClass(typeof(ISomeInterface3))] // This isn't a class-type
internal interface ISomeInterface4 {}

internal class CSomeClass {}
",
                // Test0.cs(16,16): warning CA1812: CSomeClass is an internal class that is apparently never instantiated. If so, remove the code from the assembly. If this class is intended to contain only static members, make it static (Shared in Visual Basic).
                GetCSharpResultAt(16, 16, "CSomeClass"));
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeTypeName_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace SomeNamespace
{
    internal class MyTextBoxDesigner { }

    [" + attributeFullName + @"(""SomeNamespace.MyTextBoxDesigner, TestProject"")]
    public class MyTextBox { }
}");
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeTypeNameWithFullAssemblyName_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    internal class MyTextBoxDesigner { }

    [" + attributeFullName + @"(""SomeNamespace.MyTextBoxDesigner, TestProject, Version=1.0.0.0, Culture=neutral, PublicKeyToken=123"")]
    public class MyTextBox { }
}");
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeGlobalTypeName_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

internal class MyTextBoxDesigner { }

[" + attributeFullName + @"(""MyTextBoxDesigner, TestProject"")]
public class MyTextBox { }");
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeType_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    internal class MyTextBoxDesigner { }

    [" + attributeFullName + @"(typeof(MyTextBoxDesigner))]
    public class MyTextBox { }
}");
        }

        [Fact, WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        public async Task CA1812_DesignerAttributeTypeNameWithBaseTypeName_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    public class SomeBaseType { }
    internal class MyTextBoxDesigner { }

    [Designer(""SomeNamespace.MyTextBoxDesigner, TestProject"", ""SomeNamespace.SomeBaseType"")]
    public class MyTextBox { }
}");
        }

        [Fact, WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        public async Task CA1812_DesignerAttributeTypeNameWithBaseType_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    public class SomeBaseType { }
    internal class MyTextBoxDesigner { }

    [Designer(""SomeNamespace.MyTextBoxDesigner, TestProject"", typeof(SomeBaseType))]
    public class MyTextBox { }
}");
        }

        [Fact, WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        public async Task CA1812_DesignerAttributeTypeWithBaseType_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    public class SomeBaseType { }
    internal class MyTextBoxDesigner { }

    [Designer(typeof(SomeNamespace.MyTextBoxDesigner), typeof(SomeBaseType))]
    public class MyTextBox { }
}");
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeNestedTypeName_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    [" + attributeFullName + @"(""SomeNamespace.MyTextBox.MyTextBoxDesigner, TestProject"")]
    public class MyTextBox
    {
        internal class MyTextBoxDesigner { }
    }
}",
                // False-Positive: when evaluating the string of the DesignerAttribute the type symbol doesn't exist yet
                GetCSharpResultAt(10, 24, "MyTextBox.MyTextBoxDesigner"));
        }

        [Theory]
        [WorkItem(2957, "https://github.com/dotnet/roslyn-analyzers/issues/2957")]
        [WorkItem(1708, "https://github.com/dotnet/roslyn-analyzers/issues/1708")]
        [InlineData("System.ComponentModel.DesignerAttribute")]
        [InlineData("System.Diagnostics.DebuggerTypeProxyAttribute")]
        public async Task CA1812_DesignerAttributeNestedType_NoDiagnosticAsync(string attributeFullName)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.ComponentModel;

namespace SomeNamespace
{
    [" + attributeFullName + @"(typeof(SomeNamespace.MyTextBox.MyTextBoxDesigner))]
    public class MyTextBox
    {
        internal class MyTextBoxDesigner { }
    }
}");
        }

        [Fact, WorkItem(3199, "https://github.com/dotnet/roslyn-analyzers/issues/3199")]
        public async Task CA1812_AliasingTypeNewConstraint_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

namespace SomeNamespace
{
    public class MyAliasType<T>
        where T : class, new()
    {
        public static void DoSomething() {}
    }

    internal class C {}
}",
                        @"
using MyAliasOfC = SomeNamespace.MyAliasType<SomeNamespace.C>;
using MyAliasOfMyAliasOfC = SomeNamespace.MyAliasType<SomeNamespace.MyAliasType<SomeNamespace.C>>;

public class CC
{
    public void M()
    {
        MyAliasOfC.DoSomething();
        MyAliasOfMyAliasOfC.DoSomething();
    }
}
",
                    },
                },
            }.RunAsync();
        }

        [Fact, WorkItem(1878, "https://github.com/dotnet/roslyn-analyzers/issues/1878")]
        public async Task CA1812_VisualBasic_StaticLikeClass_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Friend NotInheritable Class C1

    Private Sub New()
    End Sub

    Public Shared Function GetSomething(o As Object) As String
        Return o.ToString()
    End Function
End Class

Public Class Helpers
    Private NotInheritable Class C2

        Private Sub New()
        End Sub

        Public Shared Function GetSomething(o As Object) As String
            Return o.ToString()
        End Function
    End Class
End Class

Friend NotInheritable Class C3

    Private Const SomeConstant As String = ""Value""
    Private Shared f As Integer

    Private Sub New()
    End Sub

    Public Shared Sub M()
    End Sub

    Public Shared Property P As Integer

    Public Shared Event ThresholdReached As EventHandler
End Class

Friend Class C4

    Private Sub New()
    End Sub

    Public Shared Function GetSomething(o As Object) As String
        Return o.ToString()
    End Function
End Class

Friend NotInheritable Class C5

    Public Sub New()
    End Sub

    Public Shared Function GetSomething(o As Object) As String
        Return o.ToString()
    End Function
End Class");
        }

        [Fact, WorkItem(1878, "https://github.com/dotnet/roslyn-analyzers/issues/1878")]
        public async Task CA1812_VisualBasic_NotStaticLikeClass_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend NotInheritable Class [|C1|]

    Private Sub New()
    End Sub

    Public Function GetSomething(o As Object) As String
        Return o.ToString()
    End Function
End Class");
        }

        [Fact, WorkItem(4052, "https://github.com/dotnet/roslyn-analyzers/issues/4052")]
        public async Task CA1812_CSharp_TopLevelStatements_NoDiagnosticAsync()
        {
            await new VerifyCS.Test()
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        @"int x = 0;",
                    },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string className)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(className);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string className)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(className);
    }
}
