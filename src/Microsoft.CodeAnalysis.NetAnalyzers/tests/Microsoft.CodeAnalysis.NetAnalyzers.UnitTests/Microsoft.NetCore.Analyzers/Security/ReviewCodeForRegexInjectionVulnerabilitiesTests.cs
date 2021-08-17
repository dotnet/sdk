// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForRegexInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForRegexInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForRegexInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForRegexInjectionVulnerabilities, ReviewCodeForRegexInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForRegexInjectionVulnerabilities.Rule;

        [Fact]
        public async Task DocSample1_CSharp_Violation_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    public string SearchableText { get; set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string findTerm = Request.Form[""findTerm""];
        Match m = Regex.Match(SearchableText, ""^term="" + findTerm);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(12, 19, 11, 27, "Match Regex.Match(string input, string pattern)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
                    },
                },
            }.RunAsync();
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
Imports System.Text.RegularExpressions

Public Partial Class WebForm
    Inherits System.Web.UI.Page

    Public Property SearchableText As String

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim findTerm As String = Request.Form(""findTerm"")
        Dim m As Match = Regex.Match(SearchableText, ""^term="" + findTerm)
    End Sub
End Class",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(12, 26, 11, 34, "Function Regex.Match(input As String, pattern As String) As Match", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Constructor_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new Regex(input);
    }
}",
                GetCSharpResultAt(11, 9, 10, 24, "Regex.Regex(string pattern)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task Constructor_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new Regex(""^\\d{1,10}$"");
    }
}");
        }

        [Fact]
        public async Task IsMatch_Static_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Regex.IsMatch(""string to check"", input);
    }
}",
                GetCSharpResultAt(11, 9, 10, 24, "bool Regex.IsMatch(string input, string pattern)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IsMatch_Static_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Regex.IsMatch(input, ""^[a-z]{1,128}$"");
    }
}");
        }

        [Fact]
        public async Task IsMatch_Instance_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Text.RegularExpressions;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Regex r = new Regex(""^[a-z]{1,128}$"");
        r.IsMatch(input);
    }
}");
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_Process_Start_fileName_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        new Regex(input);

        return View();
    }
}",
                GetCSharpResultAt(10, 9, 9, 24, "Regex.Regex(string pattern)", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}
