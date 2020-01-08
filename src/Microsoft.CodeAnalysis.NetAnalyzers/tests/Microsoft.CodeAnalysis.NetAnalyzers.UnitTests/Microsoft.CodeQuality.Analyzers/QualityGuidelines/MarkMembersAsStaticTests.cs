// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Test.Utilities.MinimalImplementations;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.MarkMembersAsStaticAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpMarkMembersAsStaticFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.MarkMembersAsStaticAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicMarkMembersAsStaticFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class MarkMembersAsStaticTests
    {
        [Fact]
        public async Task CSharpSimpleMembers()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class MembersTests
{
    internal static int s_field;
    public const int Zero = 0;

    public int Method1(string name)
    {
        return name.Length;
    }

    public void Method2() { }

    public void Method3()
    {
        s_field = 4;
    }

    public int Method4()
    {
        return Zero;
    }
    
    public int Property
    {
        get { return 5; }
    }

    public int Property2
    {
        set { s_field = value; }
    }

    public int MyProperty
    {
        get { return 10; }
        set { System.Console.WriteLine(value); }
    }

    public event System.EventHandler<System.EventArgs> CustomEvent { add {} remove {} }
}",
                GetCSharpResultAt(7, 16, "Method1"),
                GetCSharpResultAt(12, 17, "Method2"),
                GetCSharpResultAt(14, 17, "Method3"),
                GetCSharpResultAt(19, 16, "Method4"),
                GetCSharpResultAt(24, 16, "Property"),
                GetCSharpResultAt(29, 16, "Property2"),
                GetCSharpResultAt(34, 16, "MyProperty"),
                GetCSharpResultAt(40, 56, "CustomEvent"));
        }

        [Fact]
        public async Task BasicSimpleMembers()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class MembersTests
    Shared s_field As Integer
    Public Const Zero As Integer = 0

    Public Function Method1(name As String) As Integer
        Return name.Length
    End Function

    Public Sub Method2()
    End Sub

    Public Sub Method3()
        s_field = 4
    End Sub

    Public Function Method4() As Integer
        Return Zero
    End Function

    Public ReadOnly Property Property1 As Integer
        Get
            Return 5
        End Get
    End Property

    Public WriteOnly Property Property2 As Integer
        Set
            s_field = Value
        End Set
    End Property

    Public Property MyProperty As Integer
        Get 
            Return 10
        End Get
        Set
            System.Console.WriteLine(Value)
        End Set
    End Property

    Public Custom Event CustomEvent As EventHandler(Of EventArgs)
        AddHandler(value As EventHandler(Of EventArgs))
        End AddHandler
        RemoveHandler(value As EventHandler(Of EventArgs))
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Class
",
                GetBasicResultAt(8, 21, "Method1"),
                GetBasicResultAt(12, 16, "Method2"),
                GetBasicResultAt(15, 16, "Method3"),
                GetBasicResultAt(19, 21, "Method4"),
                GetBasicResultAt(23, 30, "Property1"),
                GetBasicResultAt(29, 31, "Property2"),
                GetBasicResultAt(35, 21, "MyProperty"),
                GetBasicResultAt(44, 25, "CustomEvent"));
        }

        [Fact]
        public async Task CSharpSimpleMembers_Internal_DiagnosticsOnlyForInvokedMethods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class MembersTests
{
    internal static int s_field;
    public const int Zero = 0;

    public int Method1(string name)
    {
        return name.Length;
    }

    public void Method2() { }

    public void Method3()
    {
        s_field = 4;
    }

    public int Method4()
    {
        return Zero;
    }
    
    public int Property
    {
        get { return 5; }
    }

    public int Property2
    {
        set { s_field = value; }
    }

    public int MyProperty
    {
        get { return 10; }
        set { System.Console.WriteLine(value); }
    }

    public event System.EventHandler<System.EventArgs> CustomEvent { add {} remove {} }

    public void Common(string arg)
    {
        // Invoked, hence must be flagged.
        Method1(arg);

        // Invoked via delegate - should not be flagged.
        System.Action<System.Action> a = (System.Action m) => m();
        a(Method2);

        // Method3 is dead code that is never invoked - should not be flagged.
        // Method3();

        // Invoked within a lambda - must be flagged.
        System.Func<int> b = () => Method4();

        // Candidate accessors/properties/events are always flagged, regardless of them being used or not.
        int x = Property;
        // int y = Property2;
        MyProperty = 10; // getter not accessed.
    }
}",
                GetCSharpResultAt(7, 16, "Method1"),
                GetCSharpResultAt(19, 16, "Method4"),
                GetCSharpResultAt(24, 16, "Property"),
                GetCSharpResultAt(29, 16, "Property2"),
                GetCSharpResultAt(34, 16, "MyProperty"),
                GetCSharpResultAt(40, 56, "CustomEvent"));
        }

        [Fact]
        public async Task BasicSimpleMembers_Internal_DiagnosticsOnlyForInvokedMethods()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Class MembersTests
    Shared s_field As Integer
    Public Const Zero As Integer = 0

    Public Function Method1(name As String) As Integer
        Return name.Length
    End Function

    Public Sub Method2()
    End Sub

    Public Sub Method3()
        s_field = 4
    End Sub

    Public Function Method4() As Integer
        Return Zero
    End Function

    Public ReadOnly Property Property1 As Integer
        Get
            Return 5
        End Get
    End Property

    Public WriteOnly Property Property2 As Integer
        Set
            s_field = Value
        End Set
    End Property

    Public Property MyProperty As Integer
        Get 
            Return 10
        End Get
        Set
            System.Console.WriteLine(Value)
        End Set
    End Property

    Public Custom Event CustomEvent As EventHandler(Of EventArgs)
        AddHandler(value As EventHandler(Of EventArgs))
        End AddHandler
        RemoveHandler(value As EventHandler(Of EventArgs))
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event

    Public Sub Common(ByVal arg As String)
        ' Invoked, hence must be flagged.
        Method1(arg)

        ' Invoked via delegate - should not be flagged.
        Dim a As System.Action(Of System.Action) = Sub(ByVal m As System.Action) m()
        a(AddressOf Method2)

        ' Method3 is dead code that is never invoked - should not be flagged.
        'Method3()

        ' Invoked within a lambda - must be flagged.
        Dim b As System.Func(Of Integer) = Function() Method4()

        ' Candidate accessors/properties/events are always flagged, regardless of them being used or not.
        Dim x As Integer = Property1
        'Dim y As Integer = Property2
        MyProperty = 10
End Sub

End Class
",
                GetBasicResultAt(8, 21, "Method1"),
                GetBasicResultAt(19, 21, "Method4"),
                GetBasicResultAt(23, 30, "Property1"),
                GetBasicResultAt(29, 31, "Property2"),
                GetBasicResultAt(35, 21, "MyProperty"),
                GetBasicResultAt(44, 25, "CustomEvent"));
        }

        [Fact]
        public async Task CSharpSimpleMembers_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class MembersTests
{
    public MembersTests() { }

    ~MembersTests() { }

    public int x; 

    public int Method1(string name)
    {
        return x;
    }

    public int Method2()
    {
        MembersTests temp = this;
        return temp.x;
    }

    private int backingField;
    public int Prop1 
    { 
        get { return backingField; }
        set { backingField = value; }
    }

    public int AutoProp { get; set; }
    public int GetterOnlyAutoProp { get; }

    public void SomeEventHandler(object sender, System.EventArgs args) { }

    public void SomeNotImplementedMethod() => throw new System.NotImplementedException();

    public void SomeNotSupportedMethod() => throw new System.NotSupportedException();
}

public class Generic<T>
{
    public void Method1() { }
    
    public int Property
    {
        get { return 5; }
    }
}
");
        }

        [Fact]
        public async Task BasicSimpleMembers_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class MembersTests
    Public Sub New()
    End Sub

    Protected Overrides Sub Finalize()
    End Sub

    Public x As Integer

    Public Function Method1(name As String) As Integer
        Return x
    End Function

    Public Function Method2() As Integer
        Dim temp As MembersTests = Me
        Return temp.x
    End Function

    Private backingField As Integer
    Public Property Prop1 As Integer
        Get
            Return backingField
        End Get
        Set 
            backingField = Value
        End Set
    End Property

    Public Property AutoProp As Integer
    Public ReadOnly Property GetterOnlyAutoProp As Integer

    Public Sub SomeEventHandler(sender As Object, args As System.EventArgs)
    End Sub

    Public Sub SomeNotImplementedMethod()
        Throw New System.NotImplementedException()
    End Sub

    Public Sub SomeNotSupportedMethod()
        Throw New System.NotSupportedException()
    End Sub
End Class

Public Class Generic(Of T)
    Public Sub Method1()
    End Sub

    Public ReadOnly Property Property1 As Integer
        Get
            Return 5
        End Get
    End Property
End Class
");
        }

        [Fact]
        public async Task CSharpOverrides_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public abstract class SpecialCasesTest1
{
    public abstract void AbstractMethod();
}

public interface ISpecialCasesTest
{
    int Calculate(int arg);
}

public class SpecialCasesTest2 : SpecialCasesTest1, ISpecialCasesTest
{
    public virtual void VirtualMethod() {}
 
    public override void AbstractMethod() { }
 
    public int Calculate(int arg) { return arg/2; }
}");
        }

        [Fact]
        public async Task BasicOverrides_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public MustInherit Class SpecialCasesTest1
    Public MustOverride Sub AbstractMethod()
End Class

Public Interface ISpecialCasesTest
    Function Calculate(arg As Integer) As Integer
End Interface

Public Class SpecialCasesTest2
    Inherits SpecialCasesTest1
    Implements ISpecialCasesTest

    Public Overridable Sub VirtualMethod()
    End Sub

    Public Overrides Sub AbstractMethod()
    End Sub
    Public Function Calculate(arg As Integer) As Integer Implements ISpecialCasesTest.Calculate
        Return arg / 2
    End Function
End Class
");
        }

        [Fact]
        public async Task CSharpNoDiagnostic_NonTestAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

namespace System.Web.Services
{
    public class WebMethodAttribute : Attribute { }
}

public class Test
{
    [System.Web.Services.WebMethod]
    public void Method1() { }

    [ComVisible(true)]
    public void Method2() { }
}

[ComVisible(true)]
public class ComVisibleClass
{
    public void Method1() { }
}
");
        }

        [Fact]
        public async Task BasicNoDiagnostic_NonTestAttributes()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.InteropServices

Namespace System.Web.Services
    Public Class WebMethodAttribute
        Inherits Attribute
    End Class
End Namespace

Public Class Test
    <System.Web.Services.WebMethod>
    Public Sub Method1()
    End Sub

    <ComVisible(True)>
    Public Sub Method2()
    End Sub
End Class

<ComVisible(True)>
Public Class ComVisibleClass
    Public Sub Method1()
    End Sub
End Class
");
        }

        [Theory]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestInitialize", MSTestAttributes.CSharp, MSTestAttributes.VisualBasic)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod", MSTestAttributes.CSharp, MSTestAttributes.VisualBasic)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod", MSTestAttributes.CSharp, MSTestAttributes.VisualBasic)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanup", MSTestAttributes.CSharp, MSTestAttributes.VisualBasic)]
        [InlineData("Xunit.Fact", XunitApis.CSharp, XunitApis.VisualBasic)]
        [InlineData("Xunit.Theory", XunitApis.CSharp, XunitApis.VisualBasic)]
        [InlineData("CustomxUnit.WpfFact", XunitApis.CSharp, XunitApis.VisualBasic)]
        [InlineData("NUnit.Framework.OneTimeSetUp", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.OneTimeTearDown", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.SetUp", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.TearDown", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.Test", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.TestCase(\"asdf\")", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.TestCaseSource(\"asdf\")", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        [InlineData("NUnit.Framework.Theory", NUnitApis.CSharp, NUnitApis.VisualBasic)]
        public async Task NoDiagnostic_TestAttributes(string testAttributeData, string csharpTestApiDefinitions, string vbTestApiDefinitions)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;

public class Test
{{
    [{testAttributeData}]
    public void Method1() {{}}
}}
",
                        csharpTestApiDefinitions
                    },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System

Public Class Test
    <{testAttributeData}>
    Public Sub Method1()
    End Sub
End Class
",
                        vbTestApiDefinitions
                    },
                },
            }.RunAsync();
        }

        [Fact, WorkItem(1865, "https://github.com/dotnet/roslyn-analyzers/issues/1865")]
        public async Task CSharp_InstanceReferenceInObjectInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public void M()
    {
        var x = new B() { P = true };
    }
}

public class B
{
    public bool P { get; set; }
}",
            // Test0.cs(4,17): warning CA1822: Member M does not access instance data and can be marked as static (Shared in VisualBasic)
            GetCSharpResultAt(4, 17, "M"));
        }

        [Fact, WorkItem(1865, "https://github.com/dotnet/roslyn-analyzers/issues/1865")]
        public async Task Basic_InstanceReferenceInObjectInitializer_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Sub M()
        Dim x = New B With {.P = True}
    End Sub
End Class

Public Class B
    Public Property P As Boolean
End Class
",
            // Test0.vb(3,16): warning CA1822: Member M does not access instance data and can be marked as static (Shared in VisualBasic)
            GetBasicResultAt(3, 16, "M"));
        }

        [Fact, WorkItem(1933, "https://github.com/dotnet/roslyn-analyzers/issues/1933")]
        public async Task CSharpPropertySingleAccessorAccessingInstance_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class MyClass
{
    private static bool StaticThing;
    private bool InstanceThing;

    public bool Thing1
    {
        get { return StaticThing; }
        set
        {
            StaticThing = value;
            InstanceThing = value;
        }
    }

    public bool Thing2
    {
        get { return InstanceThing && StaticThing; }
        set
        {
            StaticThing = value;
        }
    }
}");
        }

        [Fact, WorkItem(1933, "https://github.com/dotnet/roslyn-analyzers/issues/1933")]
        public async Task CSharpEventWithSingleAccessorAccessingInstance_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class MyClass
{
    private static bool StaticThing;
    private bool InstanceThing;

    event EventHandler MyEvent1
    {
        add
        {
            StaticThing = true;
            InstanceThing = true;
        }
        remove
        {
            StaticThing = true;
        }
    }

    event EventHandler MyEvent2
    {
        add
        {
            StaticThing = true;
        }
        remove
        {
            StaticThing = true;
            InstanceThing = true;
        }
    }
}");
        }

        [Fact, WorkItem(2414, "https://github.com/dotnet/roslyn-analyzers/issues/2414")]
        public async Task CSharp_ErrorCase_MethodWithThrowNotInCatch()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

class C
{
    private void Validate()
    {
        {|CS0156:throw|};
    }
}");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
        {
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(symbolName);
        }

        private DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
        {
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(symbolName);
        }
    }
}