// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForOpenRedirectVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForOpenRedirectVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForOpenRedirectVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForOpenRedirectVulnerabilities, ReviewCodeForOpenRedirectVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForOpenRedirectVulnerabilities.Rule;

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

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""url""];
        this.Response.Redirect(input);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, 8, 24, "void HttpResponse.Redirect(string url)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
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

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Dim input As String = Me.Request.Form(""url"")
        Me.Response.Redirect(input)
    End Sub
End Class",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(9, 9, 8, 31, "Sub HttpResponse.Redirect(url As String)", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        if (String.IsNullOrWhiteSpace(input))
        {
            this.Response.Redirect(""https://example.org/login.html"");
        }
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_Solution_NoDiagnosticAsync()
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

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Dim input As String = Me.Request.Form(""in"")
        If String.IsNullOrWhiteSpace(input) Then
            Me.Response.Redirect(""https://example.org/login.html"")
        End If
    End Sub
End Class"
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task HttpResponse_RedirectToRoutePermanent_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.Response.RedirectToRoutePermanent(input);
    }
}",
                GetCSharpResultAt(10, 9, 9, 24, "void HttpResponse.RedirectToRoutePermanent(string routeName)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpResponseBase_RedirectLocation_NoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new HttpResponseWrapper(this.Response).RedirectLocation = input;
    }
}",
                GetCSharpResultAt(10, 9, 9, 24, "string HttpResponseWrapper.RedirectLocation", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }
    }
}