// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpReviewUnusedParametersAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpReviewUnusedParametersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicReviewUnusedParametersAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicReviewUnusedParametersFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class ReviewUnusedParametersTests
    {
        #region Unit tests for no analyzer diagnostic
        [Fact]
        [WorkItem(4039, "https://github.com/dotnet/roslyn-analyzers/issues/4039")]
        public async Task NoDiagnosticForUnnamedParameterTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(
#pragma warning disable RS0030 // Do not used banned APIs
@"
public class NeatCode
{
    public void DoSomething(string)
    {
    }
}
", DiagnosticResult.CompilerError("CS1001").WithLocation(4, 35));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task NoDiagnosticSimpleCasesTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class NeatCode
{
    // Used parameter methods
    public void UsedParameterMethod1(string use)
    {
        Console.WriteLine(this);
        Console.WriteLine(use);
    }

    public void UsedParameterMethod2(string use)
    {
        UsedParameterMethod3(ref use);
    }

    public void UsedParameterMethod3(ref string use)
    {
        use = null;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class NeatCode
    ' Used parameter methods
    Public Sub UsedParameterMethod1(use As String)
        Console.WriteLine(Me)
        Console.WriteLine(use)
    End Sub

    Public Sub UsedParameterMethod2(use As String)
        UsedParameterMethod3(use)
    End Sub

    Public Sub UsedParameterMethod3(ByRef use As String)
        use = Nothing
    End Sub
End Class
");
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task NoDiagnosticDelegateTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class NeatCode
{
    // Used parameter methods
    public void UsedParameterMethod1(Action a)
    {
        a();
    }

    public void UsedParameterMethod2(Action a1, Action a2)
    {
        try
        {
            a1();
        }
        catch(Exception)
        {
            a2();
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class NeatCode
	' Used parameter methods
	Public Sub UsedParameterMethod1(a As Action)
		a()
	End Sub

	Public Sub UsedParameterMethod2(a1 As Action, a2 As Action)
		Try
			a1()
		Catch generatedExceptionName As Exception
			a2()
		End Try
	End Sub
End Class
");
        }

        [Fact]
        [WorkItem(8884, "https://github.com/dotnet/roslyn/issues/8884")]
        public async Task NoDiagnosticDelegateTest2_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class NeatCode
{
    // Used parameter methods
    public void UsedParameterMethod1(Action a)
    {
        Action a2 = new Action(() =>
        {
            a();
        });
    }
}");
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task NoDiagnosticDelegateTest2_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class NeatCode
	' Used parameter methods
	Public Sub UsedParameterMethod1(a As Action)
		Dim a2 As New Action(Sub() 
		                         a()
                             End Sub)
	End Sub
End Class
");
        }

        [Fact]
        [WorkItem(8884, "https://github.com/dotnet/roslyn/issues/8884")]
        public async Task NoDiagnosticUsingTest_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void F(int x, IDisposable o)
    {
        using (o)
        {
            int y = x;
        }
    }
}
");
        }

        [Fact]
        [WorkItem(8884, "https://github.com/dotnet/roslyn/issues/8884")]
        public async Task NoDiagnosticUsingTest_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
	Private Sub F(x As Integer, o As IDisposable)
		Using o
			Dim y As Integer = x
		End Using
	End Sub
End Class
");
        }

        [Fact]
        [WorkItem(8884, "https://github.com/dotnet/roslyn/issues/8884")]
        public async Task NoDiagnosticLinqTest_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;
using System.Reflection;

class C
{
    private object F(Assembly assembly)
    {
        var type = (from t in assembly.GetTypes()
                    select t.Attributes).FirstOrDefault();
        return type;
    }
}
");
        }

        [Fact]
        [WorkItem(8884, "https://github.com/dotnet/roslyn/issues/8884")]
        public async Task NoDiagnosticLinqTest_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Linq
Imports System.Reflection

Class C
    Private Function F(assembly As Assembly) As Object
        Dim type = (From t In assembly.DefinedTypes() Select t.Attributes).FirstOrDefault()
        Return type
    End Function
End Class
");
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task NoDiagnosticSpecialCasesTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

public abstract class Derived : Base, I
{
    // Override
    public override void VirtualMethod(int param)
    {
    }

    // Abstract
    public abstract void AbstractMethod(int param);

    // Implicit interface implementation
    public void Method1(int param)
    {
    }

    // Explicit interface implementation
    void I.Method2(int param)
    {
    }

    // Event handlers
    public void MyEventHandler(object o, EventArgs e)
    {
    }

    public void MyEventHandler2(object o, MyEventArgs e)
    {
    }

    public class MyEventArgs : EventArgs { }
}

public class Base
{
    // Virtual
    public virtual void VirtualMethod(int param)
    {
    }
}

public interface I
{
    void Method1(int param);
    void Method2(int param);
}

public class ClassWithExtern
{
    [DllImport(""Dependency.dll"")]
    public static extern void DllImportMethod(int param);

    public static extern void ExternalMethod(int param);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.InteropServices

Public MustInherit Class Derived
    Inherits Base
    Implements I
    ' Override
    Public Overrides Sub VirtualMethod(param As Integer)
    End Sub

    ' Abstract
    Public MustOverride Sub AbstractMethod(param As Integer)

    ' Explicit interface implementation - VB has no implicit interface implementation.
    Public Sub Method1(param As Integer) Implements I.Method1
    End Sub

    ' Explicit interface implementation
    Private Sub I_Method2(param As Integer) Implements I.Method2
    End Sub

    ' Event handlers
    Public Sub MyEventHandler(o As Object, e As EventArgs)
    End Sub

    Public Sub MyEventHandler2(o As Object, e As MyEventArgs)
    End Sub

    Public Class MyEventArgs
        Inherits EventArgs
    End Class
End Class

Public Class Base
    ' Virtual
    Public Overridable Sub VirtualMethod(param As Integer)
    End Sub
End Class

Public Interface I
    Sub Method1(param As Integer)
    Sub Method2(param As Integer)
End Interface

Public Class ClassWithExtern
    <DllImport(""Dependency.dll"")>
    Public Shared Sub DllImportMethod(param As Integer)
    End Sub

    Public Declare Function DeclareFunction Lib ""Dependency.dll"" (param As Integer) As Integer
End Class
");
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task NoDiagnosticForMethodsWithSpecialAttributesTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
#define CONDITION_1

using System;
using System.Diagnostics;
using System.Runtime.Serialization;

public class ConditionalMethodsClass
{
    [Conditional(""CONDITION_1"")]
    private static void ConditionalMethod(int a)
    {
        AnotherConditionalMethod(a);
    }

    [Conditional(""CONDITION_2"")]
    private static void AnotherConditionalMethod(int b)
    {
        Console.WriteLine(b);
    }
}

public class SerializableMethodsClass
{
    [OnSerializing]
    private void OnSerializingCallback(StreamingContext context)
    {
        Console.WriteLine(this);
    }

    [OnSerialized]
    private void OnSerializedCallback(StreamingContext context)
    {
        Console.WriteLine(this);
    }

    [OnDeserializing]
    private void OnDeserializingCallback(StreamingContext context)
    {
        Console.WriteLine(this);
    }

    [OnDeserialized]
    private void OnDeserializedCallback(StreamingContext context)
    {
        Console.WriteLine(this);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
#Const CONDITION_1 = 5

Imports System
Imports System.Diagnostics
Imports System.Runtime.Serialization

Public Class ConditionalMethodsClass
    <Conditional(""CONDITION_1"")> _
    Private Shared Sub ConditionalMethod(a As Integer)
        AnotherConditionalMethod(a)
    End Sub

    <Conditional(""CONDITION_2"")> _
    Private Shared Sub AnotherConditionalMethod(b As Integer)
        Console.WriteLine(b)
    End Sub
End Class

Public Class SerializableMethodsClass
    <OnSerializing> _
    Private Sub OnSerializingCallback(context As StreamingContext)
        Console.WriteLine(Me)
    End Sub

    <OnSerialized> _
    Private Sub OnSerializedCallback(context As StreamingContext)
        Console.WriteLine(Me)
    End Sub

    <OnDeserializing> _
    Private Sub OnDeserializingCallback(context As StreamingContext)
        Console.WriteLine(Me)
    End Sub

    <OnDeserialized> _
    Private Sub OnDeserializedCallback(context As StreamingContext)
        Console.WriteLine(Me)
    End Sub
End Class
");
        }

        [Fact, WorkItem(1218, "https://github.com/dotnet/roslyn-analyzers/issues/1218")]
        public async Task NoDiagnosticForMethodsUsedAsDelegatesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C1
{
    private Action<object> _handler;

    public void Handler(object o1)
    {
    }

    public void SetupHandler()
    {
        _handler = Handler;
    }
}

public class C2
{
    public void Handler(object o1)
    {
    }

    public void TakesHandler(Action<object> handler)
    {
        handler(null);
    }

    public void SetupHandler()
    {
        TakesHandler(Handler);
    }
}

public class C3
{
    private Action<object> _handler;

    public C3()
    {
        _handler = Handler;
    }

    public void Handler(object o1)
    {
    }
}");
        }

        [Fact, WorkItem(1218, "https://github.com/dotnet/roslyn-analyzers/issues/1218")]
        public async Task NoDiagnosticForMethodsUsedAsDelegatesBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class C1
    Private _handler As Action(Of Object)

    Public Sub Handler(o As Object)
    End Sub

    Public Sub SetupHandler()
        _handler = AddressOf Handler
    End Sub
End Class

Module M2
    Sub Handler(o As Object)
    End Sub

    Sub TakesHandler(handler As Action(Of Object))
        handler(Nothing)
    End Sub

    Sub SetupHandler()
        TakesHandler(AddressOf Handler)
    End Sub
End Module

Class C3
    Private _handler As Action(Of Object)

    Sub New()
        _handler = AddressOf Handler
    End Sub

    Sub Handler(o As Object)
    End Sub
End Class
");
        }

        [Fact, WorkItem(1218, "https://github.com/dotnet/roslyn-analyzers/issues/1218")]
        public async Task NoDiagnosticForObsoleteMethods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C1
{
    [Obsolete]
    public void ObsoleteMethod(object o1)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C1
    <Obsolete>
    Public Sub ObsoleteMethod(o1 as Object)
    End Sub
End Class");
        }

        [Fact, WorkItem(1218, "https://github.com/dotnet/roslyn-analyzers/issues/1218")]
        public async Task NoDiagnosticMethodJustThrowsNotImplemented()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class MyAttribute: Attribute
{
    public int X;

    public MyAttribute(int x)
    {
        X = x;
    }
}
public class C1
{
    public int Prop1
    {
        get
        {
            throw new NotImplementedException();
        }
        set
        {
            throw new NotImplementedException();
        }
    }

    public void Method1(object o1)
    {
        throw new NotImplementedException();
    }

    public void Method2(object o1) => throw new NotImplementedException();

    [MyAttribute(0)]
    public void Method3(object o1)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C1
    Property Prop1 As Integer
        Get
            Throw New NotImplementedException()
        End Get
        Set(ByVal value As Integer)
            Throw New NotImplementedException()
        End Set
    End Property

    Public Sub Method1(o1 As Object)
        Throw New NotImplementedException()
    End Sub
End Class");
        }

        [Fact, WorkItem(1218, "https://github.com/dotnet/roslyn-analyzers/issues/1218")]
        public async Task NoDiagnosticMethodJustThrowsNotSupported()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C1
{
    public int Prop1
    {
        get
        {
            throw new NotSupportedException();
        }
        set
        {
            throw new NotSupportedException();
        }
    }

    public void Method1(object o1)
    {
        throw new NotSupportedException();
    }

    public void Method2(object o1) => throw new NotSupportedException();
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C1
    Property Prop1 As Integer
        Get
            Throw New NotSupportedException()
        End Get
        Set(ByVal value As Integer)
            Throw New NotSupportedException()
        End Set
    End Property

    Public Sub Method1(o1 As Object)
        Throw New NotSupportedException()
    End Sub
End Class");
        }

        [Fact]
        public async Task NoDiagnosticsForIndexer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    public int this[int i]
    {
        get { return 0; }
        set { }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Property Item(i As Integer) As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
");
        }

        [Fact]
        public async Task NoDiagnosticsForPropertySetter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    public int Property
    {
        get { return 0; }
        set { }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Property Property1 As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
");
        }
        [Fact]
        public async Task NoDiagnosticsForFirstParameterOfExtensionMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
static class C
{
    static void ExtensionMethod(this int i) { }
    static int ExtensionMethod(this int i, int anotherParam) { return anotherParam; }
}
");
        }

        [Fact]
        public async Task NoDiagnosticsForSingleStatementMethodsWithDefaultParameters()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void SomeMethod(string p1, string p2 = null)
    {
        throw new NotImplementedException();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class C
    Public Sub Test(p1 As String, Optional p2 As String = Nothing)
        Throw New NotImplementedException()
    End Sub
End Class");
        }

        [Fact]
        [WorkItem(2589, "https://github.com/dotnet/roslyn-analyzers/issues/2589")]
        [WorkItem(2593, "https://github.com/dotnet/roslyn-analyzers/issues/2593")]
        public async Task NoDiagnosticDiscardParameterNames()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M(int _, int _1, int _4)
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    ' _ is not an allowed identifier in VB.
    Public Sub M(_1 As Integer, _2 As Integer, _4 As Integer)
    End Sub
End Class
");
        }

        [Fact]
        [WorkItem(2466, "https://github.com/dotnet/roslyn-analyzers/issues/2466")]
        public async Task NoDiagnosticUsedLocalFunctionParameters()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M()
    {
        LocalFunction(0);
        return;

        void LocalFunction(int x)
        {
            Console.WriteLine(x);
        }
    }
}
");
        }

        [Theory]
        [WorkItem(1375, "https://github.com/dotnet/roslyn-analyzers/issues/1375")]
        [InlineData("public", "dotnet_code_quality.api_surface = private", false)]
        [InlineData("private", "dotnet_code_quality.api_surface = internal, public", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = internal, private", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = Friend, Private", false)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = internal, private", false)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = private", false)]
        [InlineData("public", "dotnet_code_quality.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.api_surface = internal, public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = all", true)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = public, private", true)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = public", true)]
        public async Task EditorConfigConfiguration_ApiSurfaceOption_AsAdditionalDocument(string accessibility, string editorConfigText, bool expectDiagnostic)
        {
            var paramName = expectDiagnostic ? "[|unused|]" : "unused";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void M(int {paramName})
    {{
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Sub M({paramName} As Integer)
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        [Theory]
        [WorkItem(1375, "https://github.com/dotnet/roslyn-analyzers/issues/1375")]
        [InlineData("public", "dotnet_code_quality.api_surface = private", false)]
        [InlineData("private", "dotnet_code_quality.api_surface = internal, public", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = internal, private", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = Friend, Private", false)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = internal, private", false)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = private", false)]
        [InlineData("public", "dotnet_code_quality.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.api_surface = internal, public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = all", true)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = public, private", true)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = public", true)]
        public async Task EditorConfigConfiguration_ApiSurfaceOption_AsAnalyzerConfigDocument(string accessibility, string editorConfigText, bool expectDiagnostic)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void M(int unused)
    {{
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]{Environment.NewLine}{editorConfigText}") },
                },
            };

            if (expectDiagnostic)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithSpan(4, 23, 4, 29).WithArguments("unused", "M"));
            }

            await csTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Sub M(unused As Integer)
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]{Environment.NewLine}{editorConfigText}") },
                },
            };

            if (expectDiagnostic)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic().WithSpan(3, 18, 3, 24).WithArguments("unused", "M"));
            }

            await vbTest.RunAsync();
        }

        [Theory]
        [WorkItem(1375, "https://github.com/dotnet/roslyn-analyzers/issues/1375")]
        [InlineData("public", "dotnet_code_quality.api_surface = private", false)]
        [InlineData("private", "dotnet_code_quality.api_surface = internal, public", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = internal, private", false)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = Friend, Private", false)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = internal, private", false)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = private", false)]
        [InlineData("public", "dotnet_code_quality.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.api_surface = internal, public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.CA1801.api_surface = all", true)]
        [InlineData("public", "dotnet_code_quality.Usage.api_surface = public, private", true)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1801.api_surface = public", true)]
        public async Task EditorConfigConfiguration_ApiSurfaceOption_AsAnalyzerConfigDocumentAndAdditionalDocument(string accessibility, string editorConfigText, bool expectDiagnostic)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void M(int unused)
    {{
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            if (expectDiagnostic)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithSpan(4, 23, 4, 29).WithArguments("unused", "M"));
            }

            await csTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Sub M(unused As Integer)
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            if (expectDiagnostic)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic().WithSpan(3, 18, 3, 24).WithArguments("unused", "M"));
            }

            await vbTest.RunAsync();
        }

        [Fact, WorkItem(3106, "https://github.com/dotnet/roslyn-analyzers/issues/3106")]
        public async Task EventArgsNotInheritingFromSystemEventArgs_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
// Reproduce UWP some specific EventArgs
namespace Windows.UI.Xaml
{
    public class RoutedEventArgs {}
    public class SizeChangedEventArgs : RoutedEventArgs {}
    public class WindowCreatedEventArgs {}
}

namespace SomeNamespace
{
    public class MyCustomEventArgs {}
}

public class C
{
    private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e) {}
    private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e) {}
    private void OnWindowCreated(object sender, Windows.UI.Xaml.WindowCreatedEventArgs e) {}

    private void OnSomething(object sender, SomeNamespace.MyCustomEventArgs e) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
' Reproduce UWP some specific EventArgs
Namespace Windows.UI.Xaml
    Public Class RoutedEventArgs
    End Class

    Public Class SizeChangedEventArgs
        Inherits RoutedEventArgs
    End Class

    Public Class WindowCreatedEventArgs
    End Class
End Namespace

Namespace SomeNamespace
    Public Class MyCustomEventArgs
    End Class
End Namespace

Public Class C
    Private Sub Page_Loaded(ByVal sender As Object, ByVal e As Windows.UI.Xaml.RoutedEventArgs)
    End Sub

    Private Sub OnSizeChanged(ByVal sender As Object, ByVal e As Windows.UI.Xaml.SizeChangedEventArgs)
    End Sub

    Private Sub OnWindowCreated(ByVal sender As Object, ByVal e As Windows.UI.Xaml.WindowCreatedEventArgs)
    End Sub

    Private Sub OnSomething(ByVal sender As Object, ByVal e As SomeNamespace.MyCustomEventArgs)
    End Sub
End Class");
        }

        [Fact, WorkItem(3039, "https://github.com/dotnet/roslyn-analyzers/issues/3039")]
        public async Task SerializationConstructorParameters_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

public class C
{
    protected C(SerializationInfo info, StreamingContext context)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.Serialization

Public Class C
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
    End Sub
End Class");
        }

        [Fact, WorkItem(3039, "https://github.com/dotnet/roslyn-analyzers/issues/3039")]
        public async Task GetObjectDataParameters_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

public class C
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.Serialization

Public Class C
    Public Sub GetObjectData(ByVal info As SerializationInfo, ByVal context As StreamingContext)
    End Sub
End Class");
        }

        [Fact]
        [WorkItem(2846, "https://github.com/dotnet/roslyn-analyzers/issues/2846")]
        public async Task CA1801_MethodThrowArrowExpression_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Class1
{
    public int Method1(int value) => throw new NotImplementedException();
}
");
        }

        [Fact, WorkItem(4052, "https://github.com/dotnet/roslyn-analyzers/issues/4052")]
        public async Task CA1801_TopLevelStatements_NoDiagnostic()
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

        [Fact, WorkItem(4462, "https://github.com/dotnet/roslyn-analyzers/issues/4462")]
        public async Task CA1801_CSharp_ImplicitRecord()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
public record Person(string Name, int Age = 0);

public record Person2(string Name, int Age = 0) {}",
            }.RunAsync();
        }

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task CSharp_DiagnosticForSimpleCasesTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    public C(int param)
    {
    }

    public void UnusedParamMethod(int param)
    {
    }

    public static void UnusedParamStaticMethod(int param1)
    {
    }

    public void UnusedDefaultParamMethod(int defaultParam = 1)
    {
    }

    public void UnusedParamsArrayParamMethod(params int[] paramsArr)
    {
    }

    public void MultipleUnusedParamsMethod(int param1, int param2)
    {
    }

    private void UnusedRefParamMethod(ref int param1)
    {
    }

    public void UnusedErrorTypeParamMethod({|CS0246:UndefinedType|} param1)
    {
    }
}
",
                GetCSharpUnusedParameterResultAt(6, 18, "param", ".ctor"),
                GetCSharpUnusedParameterResultAt(10, 39, "param", "UnusedParamMethod"),
                GetCSharpUnusedParameterResultAt(14, 52, "param1", "UnusedParamStaticMethod"),
                GetCSharpUnusedParameterResultAt(18, 46, "defaultParam", "UnusedDefaultParamMethod"),
                GetCSharpUnusedParameterResultAt(22, 59, "paramsArr", "UnusedParamsArrayParamMethod"),
                GetCSharpUnusedParameterResultAt(26, 48, "param1", "MultipleUnusedParamsMethod"),
                GetCSharpUnusedParameterResultAt(26, 60, "param2", "MultipleUnusedParamsMethod"),
                GetCSharpUnusedParameterResultAt(30, 47, "param1", "UnusedRefParamMethod"),
                GetCSharpUnusedParameterResultAt(34, 58, "param1", "UnusedErrorTypeParamMethod"));
        }

        [Fact]
        [WorkItem(459, "https://github.com/dotnet/roslyn-analyzers/issues/459")]
        public async Task Basic_DiagnosticForSimpleCasesTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Sub New(param As Integer)
    End Sub

    Public Sub UnusedParamMethod(param As Integer)
    End Sub

    Public Shared Sub UnusedParamStaticMethod(param1 As Integer)
    End Sub

    Public Sub UnusedDefaultParamMethod(Optional defaultParam As Integer = 1)
    End Sub

    Public Sub UnusedParamsArrayParamMethod(ParamArray paramsArr As Integer())
    End Sub

    Public Sub MultipleUnusedParamsMethod(param1 As Integer, param2 As Integer)
    End Sub

    Private Sub UnusedRefParamMethod(ByRef param1 As Integer)
    End Sub

    Public Sub UnusedErrorTypeParamMethod(param1 As {|BC30002:UndefinedType|})
    End Sub
End Class
",
                GetBasicUnusedParameterResultAt(3, 20, "param", ".ctor"),
                GetBasicUnusedParameterResultAt(6, 34, "param", "UnusedParamMethod"),
                GetBasicUnusedParameterResultAt(9, 47, "param1", "UnusedParamStaticMethod"),
                GetBasicUnusedParameterResultAt(12, 50, "defaultParam", "UnusedDefaultParamMethod"),
                GetBasicUnusedParameterResultAt(15, 56, "paramsArr", "UnusedParamsArrayParamMethod"),
                GetBasicUnusedParameterResultAt(18, 43, "param1", "MultipleUnusedParamsMethod"),
                GetBasicUnusedParameterResultAt(18, 62, "param2", "MultipleUnusedParamsMethod"),
                GetBasicUnusedParameterResultAt(21, 44, "param1", "UnusedRefParamMethod"),
                GetBasicUnusedParameterResultAt(24, 43, "param1", "UnusedErrorTypeParamMethod"));
        }

        [Fact]
        public async Task DiagnosticsForNonFirstParameterOfExtensionMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
static class C
{
    static void ExtensionMethod(this int i, int anotherParam) { }
}
",
                GetCSharpUnusedParameterResultAt(4, 49, "anotherParam", "ExtensionMethod"));
        }

        [Fact]
        [WorkItem(2466, "https://github.com/dotnet/roslyn-analyzers/issues/2466")]
        public async Task DiagnosticForUnusedLocalFunctionParameters_01()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M()
    {
        LocalFunction(0);
        return;

        void LocalFunction(int x)
        {
        }
    }
}",
                GetCSharpUnusedParameterResultAt(11, 32, "x", "LocalFunction"));
        }

        [Fact]
        [WorkItem(2466, "https://github.com/dotnet/roslyn-analyzers/issues/2466")]
        public async Task DiagnosticForUnusedLocalFunctionParameters_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M()
    {
        // Flag unused parameter even if LocalFunction is unused.
        void LocalFunction(int x)
        {
        }
    }
}",
                GetCSharpUnusedParameterResultAt(9, 32, "x", "LocalFunction"));
        }

        [Fact]
        public async Task DiagnosticForMethodsInNestedTypes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void OuterM(int [|x|])
    {
    }

    public class NestedType
    {
        public void InnerM(int [|y|])
        {
        }
    }
}");
        }

        [Fact, WorkItem(4462, "https://github.com/dotnet/roslyn-analyzers/issues/4462")]
        public async Task CA1801_CSharp_Record()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
public record OtherPerson
{
    public string Name { get; init; }

    public OtherPerson(string name, int [|age|] = 0)
        => Name = name;
}",
            }.RunAsync();
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCSharpUnusedParameterResultAt(int line, int column, string parameterName, string methodName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(parameterName, methodName);

        private static DiagnosticResult GetBasicUnusedParameterResultAt(int line, int column, string parameterName, string methodName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(parameterName, methodName);

        #endregion
    }
}
