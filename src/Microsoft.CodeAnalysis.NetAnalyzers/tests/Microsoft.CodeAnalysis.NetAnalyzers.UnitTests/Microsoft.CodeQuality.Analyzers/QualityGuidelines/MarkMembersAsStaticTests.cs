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

        // Method3 is dead code that is never invoked - it should still be flagged.
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
                GetCSharpResultAt(14, 17, "Method3"),
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

        ' Method3 is dead code that is never invoked - it should still be flagged.
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
                GetBasicResultAt(15, 16, "Method3"),
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

        [Fact, WorkItem(3019, "https://github.com/dotnet/roslyn-analyzers/issues/3019")]
        public async Task PrivateMethodOnlyCalledByASkippedMethod_Diagnostic()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using Xunit;

public class Program
{
    [Fact]
    private void M()
    {
        N();
    }

    private void N()
    {
    }
}",
                        XunitApis.CSharp,
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(12, 18, "N")
                    }
                }
            }.RunAsync();
        }

        [Fact, WorkItem(3019, "https://github.com/dotnet/roslyn-analyzers/issues/3019")]
        public async Task PrivateMethodOnlyReferencedByASkippedMethod_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using Xunit;

public class Program
{
    [Fact]
    private void M()
    {
        var x = nameof(N);
    }

    private void [|N|]()
    {
    }
}",
                        XunitApis.CSharp,
                    }
                }
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
    private void [|Validate|]()
    {
        {|CS0156:throw|};
    }
}");
        }

        [Fact, WorkItem(2785, "https://github.com/dotnet/roslyn-analyzers/issues/2785")]
        public async Task CSharp_CustomTestMethodAttribute_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class TestMethodAttribute : Attribute
    {
        public TestMethodAttribute() {}
    }
}

namespace SomeNamespace
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CustomTestAttribute : TestMethodAttribute
    {
        public CustomTestAttribute() {}
    }

    public class C
    {
        [CustomTest]
        public void Test()
        {
            Console.WriteLine();
        }
    }
}");
        }

        [Theory, WorkItem(3123, "https://github.com/dotnet/roslyn-analyzers/issues/3123")]
        [InlineData("System.Web.Mvc.HttpGetAttribute")]
        [InlineData("System.Web.Mvc.HttpPostAttribute")]
        [InlineData("System.Web.Mvc.HttpPutAttribute")]
        [InlineData("System.Web.Mvc.HttpDeleteAttribute")]
        [InlineData("System.Web.Mvc.HttpPatchAttribute")]
        [InlineData("System.Web.Mvc.HttpHeadAttribute")]
        [InlineData("System.Web.Mvc.HttpOptionsAttribute")]
        [InlineData("System.Web.Http.RouteAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpGetAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpPostAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpPutAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpDeleteAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpPatchAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpHeadAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.HttpOptionsAttribute")]
        [InlineData("Microsoft.AspNetCore.Mvc.RouteAttribute")]
        public async Task NoDiagnostic_WebAttributes(string webAttribute)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    [{webAttribute}]
    public void Method1()
    {{
    }}
}}",
                        @"
using System;

namespace System.Web.Mvc
{
    public class HttpGetAttribute : Attribute {}
    public class HttpPostAttribute : Attribute {}
    public class HttpPutAttribute : Attribute {}
    public class HttpDeleteAttribute : Attribute {}
    public class HttpPatchAttribute : Attribute {}
    public class HttpHeadAttribute : Attribute {}
    public class HttpOptionsAttribute : Attribute {}
}

namespace System.Web.Http
{
    public class RouteAttribute : Attribute {}
}

namespace Microsoft.AspNetCore.Mvc
{
    public class HttpGetAttribute : Attribute {}
    public class HttpPostAttribute : Attribute {}
    public class HttpPutAttribute : Attribute {}
    public class HttpDeleteAttribute : Attribute {}
    public class HttpPatchAttribute : Attribute {}
    public class HttpHeadAttribute : Attribute {}
    public class HttpOptionsAttribute : Attribute {}
    public class RouteAttribute : Attribute {}
}"
                    }
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
    <{webAttribute}>
    Public Sub Method1()
    End Sub
End Class",
                        @"
Imports System

Namespace System.Web.Mvc
    Public Class HttpGetAttribute
        Inherits Attribute
    End Class

    Public Class HttpPostAttribute
        Inherits Attribute
    End Class

    Public Class HttpPutAttribute
        Inherits Attribute
    End Class

    Public Class HttpDeleteAttribute
        Inherits Attribute
    End Class

    Public Class HttpPatchAttribute
        Inherits Attribute
    End Class

    Public Class HttpHeadAttribute
        Inherits Attribute
    End Class

    Public Class HttpOptionsAttribute
        Inherits Attribute
    End Class
End Namespace

Namespace System.Web.Http
    Public Class RouteAttribute
        Inherits Attribute
    End Class
End Namespace

Namespace Microsoft.AspNetCore.Mvc
    Public Class HttpGetAttribute
        Inherits Attribute
    End Class

    Public Class HttpPostAttribute
        Inherits Attribute
    End Class

    Public Class HttpPutAttribute
        Inherits Attribute
    End Class

    Public Class HttpDeleteAttribute
        Inherits Attribute
    End Class

    Public Class HttpPatchAttribute
        Inherits Attribute
    End Class

    Public Class HttpHeadAttribute
        Inherits Attribute
    End Class

    Public Class HttpOptionsAttribute
        Inherits Attribute
    End Class

    Public Class RouteAttribute
        Inherits Attribute
    End Class
End Namespace"
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task WebSpecificControllerMethods_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System.Web
{
    public class HttpApplication {}
}

public class C : System.Web.HttpApplication
{
    // The following methods are detected as event handler and so won't
    // trigger a diagnostic
    protected void Application_Start(object sender, EventArgs e) { }
    protected void Application_AuthenticateRequest(object sender, EventArgs e) { }
    protected void Application_BeginRequest(object sender, EventArgs e) { }
    protected void Application_EndRequest(object sender, EventArgs e) { }
    protected void Application_Error(object sender, EventArgs e) { }
    protected void Application_End(object sender, EventArgs e) { }
    protected void Application_Init(object sender, EventArgs e) { }
    protected void Session_End(object sender, EventArgs e) { }
    protected void Session_Start(object sender, EventArgs e) { }

    // The following controller methods can't be made static
    protected void Application_Start() { }
    protected void Application_End() { }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace System.Web
    Public Class HttpApplication
    End Class
End Namespace

Public Class C
    Inherits System.Web.HttpApplication

    Protected Sub Application_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_AuthenticateRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_BeginRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_EndRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Error(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Init(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Start()
    End Sub

    Protected Sub Application_End()
    End Sub
End Class
");
        }

        [Fact]
        public async Task WebSpecificControllerMethods_WrongEnclosingType_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class SomeClass {}

public class C : SomeClass
{
    protected void Application_Start() { }
    protected void Application_End() { }
}",
                GetCSharpResultAt(8, 20, "Application_Start"),
                GetCSharpResultAt(9, 20, "Application_End"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class SomeClass
End Class

Public Class C
    Inherits SomeClass

    Protected Sub Application_Start()
    End Sub

    Protected Sub Application_End()
    End Sub
End Class
",
                GetBasicResultAt(10, 19, "Application_Start"),
                GetBasicResultAt(13, 19, "Application_End"));
        }

        [Fact]
        public async Task MethodsWithOptionalParameter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
    private int x;

    public int M1(int y = 0)
    {
        return x;
    }

    public int [|M2|](int y = 0)
    {
        return 0;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Class C
    Private x As Integer

    Public Function M1(Optional y As Integer = 0) As Integer
        Return x
    End Function

    Public Function [|M2|](Optional y As Integer = 0) As Integer
        Return 0
    End Function
End Class
");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(symbolName);
    }
}