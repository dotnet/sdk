// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForDllInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForDllInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForDllInjectionVulnerabilities, ReviewCodeForDllInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForDllInjectionVulnerabilities.Rule;

        [Fact]
        public async Task Assembly_LoadFrom_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Assembly.LoadFrom(input);
    }
}",
                GetCSharpResultAt(15, 9, 14, 24, "Assembly Assembly.LoadFrom(string assemblyFile)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Violation_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Reflection;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        byte[] rawAssembly = Convert.FromBase64String(input);
        Assembly.Load(rawAssembly);
    }
}",
                GetCSharpResultAt(11, 9, 9, 24, "Assembly Assembly.Load(byte[] rawAssembly)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation_DiagnosticAsync()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis,
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Reflection

Public Partial Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Request.Form(""in"")
        Dim rawAssembly As Byte() = Convert.FromBase64String(input)
        Assembly.Load(rawAssembly)
    End Sub
End Class",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(11, 9, 9, 31, "Function Assembly.Load(rawAssembly As Byte()) As Assembly", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Assembly_LoadFrom_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Assembly.LoadFrom(""myassembly.dll"");
    }
}");
        }

        [Fact]
        public async Task AppDomain_ExecuteAssembly_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        AppDomain.CurrentDomain.ExecuteAssembly(input);
    }
}",
                GetCSharpResultAt(15, 9, 14, 24, "int AppDomain.ExecuteAssembly(string assemblyFile)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_AppDomain_ExecuteAssembly_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        AppDomain.CurrentDomain.ExecuteAssembly(input);

        return View();
    }
}",
                GetCSharpResultAt(10, 9, 9, 24, "int AppDomain.ExecuteAssembly(string assemblyFile)", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}
