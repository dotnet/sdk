// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForCommandExecutionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForCommandExecutionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForCommandExecutionVulnerabilities, ReviewCodeForCommandExecutionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForCommandExecutionVulnerabilities.Rule;

        [Fact]
        public async Task DoNotWarnAboutTaintedArgumentIfDoesNotEnterSinkAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Diagnostics;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string in1 = Request.Form[""in1""];
        string in2 = Request.Form[""in2""];
        Foo(in1, in2);
    }

    protected void Foo(string a, string b)
    {
        Process p = Process.Start(a, """", b, null, """");
    }
}",
                GetCSharpResultAt(16, 21, 9, 22, "Process Process.Start(string fileName, string arguments, string userName, SecureString password, string domain)", "void WebForm.Foo(string a, string b)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_CSharp_fileName_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Diagnostics;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Process p = Process.Start(input);
    }
}",
                GetCSharpResultAt(10, 21, 9, 24, "Process Process.Start(string fileName)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_VB_fileName_DiagnosticAsync()
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
Imports System.Diagnostics

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs as EventArgs)
        Dim input As String = Me.Request.Form(""in"")
        Dim p As Process = Process.Start(input)
    End Sub
End Class
",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(10, 28, 9, 31, "Function Process.Start(fileName As String) As Process", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Process_Start_arguments_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Diagnostics;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Process p = Process.Start(""copy"", input + "" \\\\somewhere\\public"");
    }
}",
                GetCSharpResultAt(12, 21, 11, 24, "Process Process.Start(string fileName, string arguments)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task ProcessStartInfo_Constructor_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Diagnostics;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        ProcessStartInfo i = new ProcessStartInfo(input);
    }
}",
                GetCSharpResultAt(12, 30, 11, 24, "ProcessStartInfo.ProcessStartInfo(string fileName)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task ProcessStartInfo_Arguments_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Diagnostics;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        ProcessStartInfo i = new ProcessStartInfo(""copy"") 
        {
            Arguments = input + "" \\\\somewhere\\public"",
        };
    }
}",
                GetCSharpResultAt(14, 13, 11, 24, "string ProcessStartInfo.Arguments", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_Process_Start_fileName_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        Process p = Process.Start(input);

        return View();
    }
}",
                GetCSharpResultAt(10, 21, 9, 24, "Process Process.Start(string fileName)", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}
