// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ReviewCodeForLdapInjectionVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ReviewCodeForLdapInjectionVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForLdapInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForLdapInjectionVulnerabilities, ReviewCodeForLdapInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForLdapInjectionVulnerabilities.Rule;

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
using System.DirectoryServices;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string userName = Request.Params[""user""];
        string filter = ""(uid="" + userName + "")"";  //searching for the user entry

        // In this example, if we send the * character in the user parameter which will
        // result in the filter variable in the code to be initialized with (uid=*).
        // The resulting LDAP statement will make the server return any object that
        // contains a uid attribute.
        DirectorySearcher searcher = new DirectorySearcher(filter);
        SearchResultCollection results = searcher.FindAll();

        // Iterate through each SearchResult in the SearchResultCollection.
        foreach (SearchResult searchResult in results)
        {
            // ...
        }
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(16, 38, 9, 27, "DirectorySearcher.DirectorySearcher(string filter)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Params", "void WebForm.Page_Load(object sender, EventArgs e)"),
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
Imports System.DirectoryServices

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(send As Object, e As EventArgs)
        Dim userName As String = Me.Request.Params(""user"")
        Dim filter As String = ""(uid="" + userName + "")""    ' searching for the user entry

        ' In this example, if we send the * character in the user parameter which will
        ' result in the filter variable in the code to be initialized with (uid=*).
        ' The resulting LDAP statement will make the server return any object that
        ' contains a uid attribute.
        Dim searcher As DirectorySearcher = new DirectorySearcher(filter)
        Dim results As SearchResultCollection = searcher.FindAll()

        ' Iterate through each SearchResult in the SearchResultCollection.
        For Each searchResult As SearchResult in results
            ' ...
        Next searchResult
    End Sub
End Class",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(16, 45, 9, 34, "Sub DirectorySearcher.New(filter As String)", "Sub WebForm.Page_Load(send As Object, e As EventArgs)", "Property HttpRequest.Params As NameValueCollection", "Sub WebForm.Page_Load(send As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DirectoryEntry_Path_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.DirectoryServices;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new DirectoryEntry(input);
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "DirectoryEntry.DirectoryEntry(string path)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DirectoryEntry_Username_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.DirectoryServices;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new DirectoryEntry(""path"", input, ""password"");
    }
}");
        }

        [Fact]
        public async Task DirectorySearcher_Filter_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.DirectoryServices;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        DirectorySearcher ds = new DirectorySearcher();
        ds.Filter = ""(lastName="" + input + "")"";
    }
}",
                GetCSharpResultAt(13, 9, 11, 24, "string DirectorySearcher.Filter", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DirectoryEntry_Path_Sanitized_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.DirectoryServices;
using System.Web;
using System.Web.UI;
using Microsoft.Security.Application;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        input = Encoder.LdapDistinguishedNameEncode(input);
        new DirectoryEntry(input);
    }
}");
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_DirectoryEntry_Path_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.DirectoryServices;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        new DirectoryEntry(input);

        return View();
    }
}",
                GetCSharpResultAt(10, 9, 9, 24, "DirectoryEntry.DirectoryEntry(string path)", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}
