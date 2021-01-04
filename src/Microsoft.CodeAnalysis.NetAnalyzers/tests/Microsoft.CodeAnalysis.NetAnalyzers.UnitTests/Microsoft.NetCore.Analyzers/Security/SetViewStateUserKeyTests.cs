// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.SetViewStateUserKey,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.SetViewStateUserKey,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class SetViewStateUserKeyTests
    {
        [Fact]
        public async Task TestSubclassWithoutOnInitMethodDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected void TestMethod (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));

            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Web.UI

class TestClass
    Inherits Page
    protected Sub TestMethod (ByVal e As EventArgs)
        ViewStateUserKey = ""ViewStateUserKey""
    End Sub
End Class",
            GetBasicResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestOverrideModifierWithoutSettingViewStateUserKeyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected override void OnInit (EventArgs e)
    {
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));

            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Web.UI

class TestClass
    Inherits Page
    protected Sub OnInit (ByVal e As EventArgs)
    End Sub
End Class",
            GetBasicResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestNewModifierWithoutSettingViewStateUserKeyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected new void OnInit (EventArgs e)
    {
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestNoModifierWithoutSettingViewStateUserKeyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected void OnInit (EventArgs e)
    {
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestOverloadOnInitWithSettingViewStateUserKeyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected internal void OnInit ()
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestStaticMethodWithSettingViewStateUserKeyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected static void OnInit (EventArgs e)
    {
        var testClass = new TestClass();
        testClass.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestSubclassWithSettingPropertyOfLocalObjectDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected override void OnInit (EventArgs e)
    {
        var testClass = new TestClass();
        testClass.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestSubclassWithSettingPropertyOfWrongClassDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class MyType
{
    public string ViewStateUserKey { get; set; }
}

class TestClass : Page
{
    private MyType _field;

    protected override void OnInit (EventArgs e)
    {
        _field.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(10, 7, "TestClass"));
        }

        [Fact]
        public async Task TestSubclassWithSettingWrongPropertyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    public int ViewStateUserKey { get; set; }

    protected override void OnInit (EventArgs e)
    {
        ViewStateUserKey = 123;
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestSettingPropertyOfLocalObjectInPage_InitDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    private void Page_Init (object sender, EventArgs e)
    {
        var testClass = new TestClass();
        testClass.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TesthSettingPropertyOfWrongClassInPage_InitDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class MyType
{
    public string ViewStateUserKey { get; set; }
}

class TestClass : Page
{
    private MyType _field;

    private void Page_Init (object sender, EventArgs e)
    {
        _field.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(10, 7, "TestClass"));
        }

        [Fact]
        public async Task TestWithSettingWrongPropertyInPage_InitDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    public int ViewStateUserKey { get; set; }

    private void Page_Init (object sender, EventArgs e)
    {
        ViewStateUserKey = 123;
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestInPage_InitWithObjectParameterDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    private void Page_Init (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestInPage_InitWithStringReturnTypeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    private string Page_Init (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
        return ViewStateUserKey;
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestNeitherOnInitNorInPage_InitNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected override void OnInit (EventArgs e)
    {
    }

    private void Page_Init (object sender, EventArgs e)
    {
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestNewPageDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    public new System.Web.UI.Page Page { get; set; }

    private void Page_Init (object sender, EventArgs e)
    {
        Page.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestOverridePageDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    public override System.Web.UI.Page Page { get; set; }

    private void Page_Init (object sender, EventArgs e)
    {
        Page.ViewStateUserKey = ""ViewStateUserKey"";
    }
}",
            GetCSharpResultAt(5, 7, "TestClass"));
        }

        [Fact]
        public async Task TestSubclassWithSettingViewStateUserKeyNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected override void OnInit (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestNewModifierNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected new void OnInit (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestWithoutModifierNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected void OnInit (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestOrdinaryClassWithSettingViewStateUserKeyNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

class TestClass
{
    public string ViewStateUserKey { get; set; }

    protected void OnInit (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestSettingViewStateUserKeyInPage_InitNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    private void Page_Init (object sender, EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestBothOnInitAndInPage_InitNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    protected override void OnInit (EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }

    private void Page_Init (object sender, EventArgs e)
    {
        ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        [Fact]
        public async Task TestNotAPage_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass
{
    public Page Page { get; set; }

    protected void OnInit (EventArgs e)
    {
    }

    private void Page_Init (object sender, EventArgs e)
    {
    }
}");
        }

        [Fact]
        public async Task TestInterface_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

interface ITestInterface
{
    Page Page { get; set; }

    void OnInit(EventArgs e);

    void Page_Init(object sender, EventArgs e);
}");
        }

        [Fact]
        public async Task TestSettingViewStateUserKeyOfPageNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.UI;

class TestClass : Page
{
    private void Page_Init (object sender, EventArgs e)
    {
        Page.ViewStateUserKey = ""ViewStateUserKey"";
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyBasicAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
