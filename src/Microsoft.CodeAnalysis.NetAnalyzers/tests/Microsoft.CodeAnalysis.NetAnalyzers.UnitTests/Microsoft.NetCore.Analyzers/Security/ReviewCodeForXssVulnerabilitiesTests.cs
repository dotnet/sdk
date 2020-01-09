// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForXssVulnerabilitiesTests : TaintedDataAnalyzerTestBase
    {
        public ReviewCodeForXssVulnerabilitiesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => ReviewCodeForXssVulnerabilities.Rule;

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ReviewCodeForXssVulnerabilities();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ReviewCodeForXssVulnerabilities();
        }

        [Fact]
        public void DocSample2_CSharp_Violation_Diagnostic()
        {
            VerifyCSharp(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Response.Write(""<HTML>"" + input + ""</HTML>"");
    }
}",
                GetCSharpResultAt(9, 9, 8, 24, "void HttpResponse.Write(string s)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void DocSample2_CSharp_Solution_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];

        // Example usage of System.Web.HttpServerUtility.HtmlEncode().
        Response.Write(""<HTML>"" + Server.HtmlEncode(input) + ""</HTML>"");
    }
}");
        }

        [Fact]
        public void DocSample2_VB_Violation_Diagnostic()
        {
            VerifyBasic(@"
Imports System

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Me.Request.Form(""in"")
        Me.Response.Write(""<HTML>"" + input + ""</HTML>"")
    End Sub
End Class
",
                GetBasicResultAt(9, 9, 8, 31, "Sub HttpResponse.Write(s As String)", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"));
        }

        [Fact]
        public void DocSample2_VB_Solution_NoDiagnostic()
        {
            VerifyBasic(@"
Imports System

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Me.Request.Form(""in"")

        ' Example usage of System.Web.HttpServerUtility.HtmlEncode().
        Me.Response.Write(""<HTML>"" + Me.Server.HtmlEncode(input) + ""</HTML>"")
    End Sub
End Class
");
        }

        [Fact]
        public void Simple_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Response.Write(""<HTML><TITLE>test</TITLE><BODY>Hello world!</BODY></HTML>"");
    }
}");
        }

        [Fact]
        public void Int32_Parse_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        string integer = Int32.Parse(input).ToString();
        Response.Write(""<HTML>"" + integer + ""</HTML>"");
    }
}");
        }

        [Fact]
        public void HttpServerUtility_HtmlEncode_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        string encoded = Server.HtmlEncode(input);
        Response.Write(""<HTML>"" + encoded + ""</HTML>"");
    }
}");
        }
    }
}
