// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
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

    public int this[int x] => 42;
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

    Default Public ReadOnly Property Item(x As Integer) As Integer
        Get
            Return 42
        End Get
    End Property
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
        public async Task CSharpNoDiagnostic_ComVisibleAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

public class Test
{
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
        public async Task BasicNoDiagnostic_ComVisibleAttribute()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.InteropServices

Public Class Test
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
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestInitialize", true, false, false)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod", true, false, false)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod", true, false, false)]
        [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanup", true, false, false)]
        [InlineData("Xunit.Fact", false, false, true)]
        [InlineData("Xunit.Theory", false, false, true)]
        [InlineData("CustomxUnit.WpfFact", false, false, true)]
        [InlineData("NUnit.Framework.OneTimeSetUp", false, true, false)]
        [InlineData("NUnit.Framework.OneTimeTearDown", false, true, false)]
        [InlineData("NUnit.Framework.SetUp", false, true, false)]
        [InlineData("NUnit.Framework.TearDown", false, true, false)]
        [InlineData("NUnit.Framework.Test", false, true, false)]
        [InlineData("NUnit.Framework.TestCase(\"asdf\")", false, true, false)]
        [InlineData("NUnit.Framework.TestCaseSource(\"asdf\")", false, true, false)]
        [InlineData("NUnit.Framework.Theory", false, true, false)]
        public async Task NoDiagnostic_TestAttributes(string testAttributeData, bool isMSTest, bool isNUnit, bool isxunit)
        {
            var referenceAssemblies = (isMSTest, isNUnit, isxunit) switch
            {
                (true, false, false) => AdditionalMetadataReferences.DefaultWithMSTest,
                (false, true, false) => AdditionalMetadataReferences.DefaultWithNUnit,
                (false, false, true) => AdditionalMetadataReferences.DefaultWithXUnit,
                _ => throw new InvalidOperationException("Invalid combination of test framework")
            };

            await new VerifyCS.Test
            {
                ReferenceAssemblies = referenceAssemblies,
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
                        !isxunit ? "" : @"
namespace CustomxUnit
{
    using System;
    using Xunit;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WpfFactAttribute : FactAttribute
    {
    }
}",
                    },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = referenceAssemblies,
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
                        !isxunit ? "" : @"
Imports System

Namespace CustomxUnit

    <AttributeUsage(AttributeTargets.Method, AllowMultiple:=False)>
    Public Class WpfFactAttribute
        Inherits Xunit.FactAttribute
    End Class
End Namespace
",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        [WorkItem(4995, "https://github.com/dotnet/roslyn-analyzers/issues/4995")]
        [WorkItem(5110, "https://github.com/dotnet/roslyn-analyzers/issues/5110")]
        public async Task AttributeImplementingNUnitITestBuilder_NoDiagnostic()
        {
            var referenceAssemblies = AdditionalMetadataReferences.DefaultWithNUnit;

            await new VerifyCS.Test
            {
                ReferenceAssemblies = referenceAssemblies,
                TestState =
                {
                    Sources =
                    {
                        $@"
public class Test
{{
    [CustomNUnit.MyTestBuilder]
    public void Method1() {{}}
}}
",
@"
namespace CustomNUnit
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework.Interfaces;
    using NUnit.Framework.Internal;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MyTestBuilderAttribute : Attribute, ITestBuilder
    {
        public IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test suite)
        {
            return Array.Empty<TestMethod>();
        }
    }
}",
                    },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = referenceAssemblies,
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class Test
    <CustomNUnit.MyTestBuilder>
    Public Sub Method1()
    End Sub
End Class
",
@"
Imports System
Imports System.Collections.Generic
Imports NUnit.Framework.Interfaces
Imports NUnit.Framework.Internal

Namespace CustomNUnit
    <AttributeUsage(AttributeTargets.Method, AllowMultiple:=False)>
    Public Class MyTestBuilderAttribute
        Inherits Attribute
        Implements ITestBuilder
        Public Function BuildFrom(method As IMethodInfo, suite As NUnit.Framework.Internal.Test) As IEnumerable(Of TestMethod) Implements ITestBuilder.BuildFrom
            Return Array.Empty(Of TestMethod)()
        End Function
    End Class
End Namespace
",
                    },
                },
            }.RunAsync();
        }

        [Fact, WorkItem(3019, "https://github.com/dotnet/roslyn-analyzers/issues/3019")]
        public async Task PrivateMethodOnlyCalledByASkippedMethod_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithXUnit,
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
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithXUnit,
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

        [Theory, WorkItem(3835, "https://github.com/dotnet/roslyn-analyzers/issues/3835")]
        [InlineData("build_property.UsingMicrosoftNETSdkWeb = true")]
        [InlineData("build_property.ProjectTypeGuids = {349C5851-65DF-11DA-9384-00065B846F21}")]
        [InlineData("build_property.ProjectTypeGuids = {e24c65dc-7377-472B-9ABA-BC803B73C61A}")]
        [InlineData("build_property.ProjectTypeGuids = {349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}")]
        [InlineData("build_property.ProjectTypeGuids = {349c5851-65df-11da-9384-00065b846f21} ; {fae04ec0-301f-11d3-bf4b-00c04f79efbc}")]
        [InlineData("dotnet_code_quality.api_surface = private, internal")]
        public async Task WebSpecificControllerMethods_NoDiagnostic(string editorConfigText)
        {
            var csSource = @"
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
}";

            await new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { csSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfigText}") },
                }
            }.RunAsync();

            var vbSource = @"
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
";
            await new VerifyVB.Test()
            {
                TestState =
                {
                    Sources = { vbSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfigText}") },
                }
            }.RunAsync();
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

        [Fact, WorkItem(3857, "https://github.com/dotnet/roslyn-analyzers/issues/3857")]
        public async Task CA1822_ObsoleteAttribute_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;

public class C
{
    [Obsolete]
    public void M1() {}

    [Obsolete(""Some reason"")]
    public void M2() {}

    [Obsolete(""Some reason"", false)]
    public void M3() {}

    [Obsolete]
    public int P1
    {
        get { return 10; }
        set { Console.WriteLine(""""); }
    }

    public int P2
    {
        [Obsolete]
        get { return 10; }
        set { Console.WriteLine(""""); }
    }

    public int P3
    {
        get { return 10; }
        [Obsolete]
        set { Console.WriteLine(""""); }
    }

    [Obsolete]
    public event EventHandler<EventArgs> E1 { add {} remove {} }
}

[Obsolete]
public class C2
{
    public void M1() {}

    public int P1
    {
        get { return 10; }
        set { Console.WriteLine(""""); }
    }

    public event EventHandler<EventArgs> E1 { add {} remove {} }
}

[Obsolete]
public class C3
{
    public class C4
    {
        public void M1() {}

        public int P1
        {
            get { return 10; }
            set { Console.WriteLine(""""); }
        }

        public event EventHandler<EventArgs> E1 { add {} remove {} }
    }
}",
            }.RunAsync();

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    <Obsolete>
    Public Sub M1()
    End Sub

    <Obsolete(""Some reason"")>
    Public Sub M2()
    End Sub

    <Obsolete(""Some reason"", False)>
    Public Sub M3()
    End Sub

    <Obsolete>
    Public Property P1 As Integer
        Get
            Return 10
        End Get
        Set(ByVal value As Integer)
            Console.WriteLine("""")
        End Set
    End Property

    Public Property P2 As Integer
        <Obsolete>
        Get
            Return 10
        End Get
        Set(ByVal value As Integer)
            Console.WriteLine("""")
        End Set
    End Property

    Public Property P3 As Integer
        Get
            Return 10
        End Get
        <Obsolete>
        Set(ByVal value As Integer)
            Console.WriteLine("""")
        End Set
    End Property

    <Obsolete>
    Public Custom Event CustomEvent As EventHandler(Of EventArgs)
        AddHandler(value As EventHandler(Of EventArgs))
        End AddHandler
        RemoveHandler(value As EventHandler(Of EventArgs))
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Class

<Obsolete>
Public Class C2
    Public Sub M1()
    End Sub

    Public Property P1 As Integer
        Get
            Return 10
        End Get
        Set(ByVal value As Integer)
            Console.WriteLine("""")
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

<Obsolete>
Public Class C3
    Public Class C4
        Public Sub M1()
        End Sub

        Public Property P1 As Integer
            Get
                Return 10
            End Get
            Set(ByVal value As Integer)
                Console.WriteLine("""")
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
End Class
");
        }

        [Theory, WorkItem(3835, "https://github.com/dotnet/roslyn-analyzers/issues/3835")]
        [InlineData("build_property.UsingMicrosoftNETSdkWeb = true")]
        [InlineData("build_property.ProjectTypeGuids = {349C5851-65DF-11DA-9384-00065B846F21}")]
        [InlineData("build_property.ProjectTypeGuids = {e24c65dc-7377-472B-9ABA-BC803B73C61A}")]
        [InlineData("build_property.ProjectTypeGuids = {349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}")]
        [InlineData("build_property.ProjectTypeGuids = {349c5851-65df-11da-9384-00065b846f21} ; {fae04ec0-301f-11d3-bf4b-00c04f79efbc}")]
        [InlineData("dotnet_code_quality.api_surface = private, internal")]
        public async Task TestWebProject(string editorConfigText)
        {
            var csSource = @"
public class Test
{
    public int PublicMethod() => 0;
    protected int ProtectedMethod() => 0;
    internal int [|InternalMethod|]() => 0;
    private int [|PrivateMethod|]() => 0;
}";
            await new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { csSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfigText}") },
                }
            }.RunAsync();

            var vbSource = @"
Public Class Test
    Public Function PublicMethod() As Integer
        Return 0
    End Function

    Protected Function ProtectedMethod() As Integer
        Return 0
    End Function

    Friend Function [|FriendMethod|]() As Integer
        Return 0
    End Function

    Private Function [|PrivateMethod|]() As Integer
        Return 0
    End Function
End Class";
            await new VerifyVB.Test()
            {
                TestState =
                {
                    Sources = { vbSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfigText}") },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(2834, "https://github.com/dotnet/roslyn-analyzers/issues/2834")]
        public async Task FullProperties_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int field;

    public int P1
    {
        get { return field; }
        set { field = value; }
    }

    public int P2
    {
        get { return field; }
    }

    public int P3 => field;

    public int P4
    {
        get => field;
        set => field = value;
    }
}");
        }

        [Fact, WorkItem(2834, "https://github.com/dotnet/roslyn-analyzers/issues/2834")]
        public async Task AutoProperties_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class C1
{
    public string P1 { get; set; }

    public string P2 { get; }

    public string P3
    {
        [DebuggerStepThrough]
        get;
    }

    public string [|P4|] // Because of the error there is no generated field
    {
        [DebuggerStepThrough]
        {|CS8051:set|};
    }

    public string P5
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        get;
    }
}");
        }

        [Fact, WorkItem(2834, "https://github.com/dotnet/roslyn-analyzers/issues/2834")]
        public async Task Properties_StaticField_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C1
{
    private static int s_field;

    public int [|P1|]
    {
        get { return s_field; }
        set { s_field = value; }
    }

    public int [|P2|]
    {
        get { return s_field; }
    }

    public int [|P3|]
    {
        set { s_field = value; }
    }
}");
        }

        [Fact, WorkItem(4304, "https://github.com/dotnet/roslyn-analyzers/pull/4304")]
        public async Task SkippableFactAttribute()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithXUnit,
                TestCode = @"
using Xunit;

public class SkippableFactAttribute : FactAttribute {}

public class C
{
    [SkippableFact]
    public void M() {}
}",
            }.RunAsync();
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaiterPattern_INotifyCompletion_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaiter : INotifyCompletion
{
    public void GetResult()
    {
    }

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
}");
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaiterPattern_ICriticalNotifyCompletion_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaiter : ICriticalNotifyCompletion
{
    public void GetResult()
    {
    }

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
    public void UnsafeOnCompleted(Action continuation) => throw null;
}");
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaitablePattern_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaitable
{
    public DummyAwaiter GetAwaiter() => new DummyAwaiter();
}

public class DummyAwaiter : INotifyCompletion
{
    public void GetResult()
    {
    }

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
}");
        }

        [Fact]
        public async Task InstanceMemberUsedInXml_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Property Language As String
    Private Sub M()
        Dim x =
<Workspace>
    <Project Language=<%= Me.Language %>>
    </Project>
</Workspace>
        End Sub
End Class");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);
    }
}
