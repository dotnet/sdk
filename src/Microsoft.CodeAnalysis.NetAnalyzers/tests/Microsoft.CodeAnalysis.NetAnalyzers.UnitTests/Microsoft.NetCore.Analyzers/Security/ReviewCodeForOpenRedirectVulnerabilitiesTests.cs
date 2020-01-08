// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForOpenRedirectVulnerabilitiesTests : TaintedDataAnalyzerTestBase
    {
        public ReviewCodeForOpenRedirectVulnerabilitiesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => ReviewCodeForOpenRedirectVulnerabilities.Rule;

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ReviewCodeForOpenRedirectVulnerabilities();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ReviewCodeForOpenRedirectVulnerabilities();
        }

        [Fact]
        public void DocSample1_CSharp_Violation_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""url""];
        this.Response.Redirect(input);
    }
}",
                GetCSharpResultAt(9, 9, 8, 24, "void HttpResponse.Redirect(string url)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void DocSample1_VB_Violation_Diagnostic()
        {
            VerifyBasic(@"
Imports System

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Dim input As String = Me.Request.Form(""url"")
        Me.Response.Redirect(input)
    End Sub
End Class",
                GetBasicResultAt(9, 9, 8, 31, "Sub HttpResponse.Redirect(url As String)", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)"));
        }

        [Fact]
        public void DocSample1_CSharp_Solution_NoDiagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
        public void DocSample1_VB_Solution_NoDiagnostic()
        {
            VerifyBasic(@"
Imports System

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Dim input As String = Me.Request.Form(""in"")
        If String.IsNullOrWhiteSpace(input) Then
            Me.Response.Redirect(""https://example.org/login.html"")
        End If
    End Sub
End Class");
        }

        [Fact]
        public void HttpResponse_RedirectToRoutePermanent_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
        public void HttpResponseBase_RedirectLocation_NoDiagnostic()
        {
            VerifyCSharpWithDependencies(@"
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