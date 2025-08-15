// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXssVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXssVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForXssVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForXssVulnerabilities, ReviewCodeForXssVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForXssVulnerabilities.Rule;

        [Fact]
        public async Task DocSample2_CSharp_Violation_DiagnosticAsync()
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
        string input = Request.Form[""in""];
        Response.Write(""<HTML>"" + input + ""</HTML>"");
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, 8, 24, "void HttpResponse.Write(string s)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample2_CSharp_Solution_NoDiagnosticAsync()
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
        string input = Request.Form[""in""];

        // Example usage of System.Web.HttpServerUtility.HtmlEncode().
        Response.Write(""<HTML>"" + Server.HtmlEncode(input) + ""</HTML>"");
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample2_VB_Violation_DiagnosticAsync()
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

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Me.Request.Form(""in"")
        Me.Response.Write(""<HTML>"" + input + ""</HTML>"")
    End Sub
End Class
",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(9, 9, 8, 31, "Sub HttpResponse.Write(s As String)", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample2_VB_Solution_NoDiagnosticAsync()
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

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Me.Request.Form(""in"")

        ' Example usage of System.Web.HttpServerUtility.HtmlEncode().
        Me.Response.Write(""<HTML>"" + Me.Server.HtmlEncode(input) + ""</HTML>"")
    End Sub
End Class
",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Simple_NoDiagnosticAsync()
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
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        Response.Write(""<HTML><TITLE>test</TITLE><BODY>Hello world!</BODY></HTML>"");
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Int32_Parse_NoDiagnosticAsync()
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
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        string integer = Int32.Parse(input).ToString();
        Response.Write(""<HTML>"" + integer + ""</HTML>"");
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task HttpServerUtility_HtmlEncode_NoDiagnosticAsync()
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
using System.Web;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        string encoded = Server.HtmlEncode(input);
        Response.Write(""<HTML>"" + encoded + ""</HTML>"");
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task HttpServerUtility_HtmlEncode_StringWriterOverload_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis,
                TestState =
                {
                    Sources =
                    {
                        SharedCode.WrongSanitizer,
                    }
                },
            }.RunAsync();
        }
    }
}
