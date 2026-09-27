// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXssVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXssVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
    public class ReviewCodeForXssVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForXssVulnerabilities, ReviewCodeForXssVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForXssVulnerabilities.Rule;

        [TestMethod]
        public async Task AutoPropertyInitializer_CSharp_DiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync("""
                using System.Web;
                using System.Web.UI;

                class RequestText : ITextControl
                {
                    public string Text { get; set; } = HttpContext.Current.Request.Form["text"];
                }
                """,
                GetCSharpResultAt(6, 38, 6, 40, "string RequestText.Text", "string RequestText.Text", "NameValueCollection HttpRequest.Form", "string RequestText.Text"));
        }

        [TestMethod]
        public async Task AutoPropertyInitializer_VB_DiagnosticAsync()
        {
            await VerifyVisualBasicWithDependenciesAsync("""
                Imports System.Web
                Imports System.Web.UI

                Public Class RequestText
                    Implements ITextControl

                    Public Property Text As String = HttpContext.Current.Request.Form("text") Implements ITextControl.Text
                End Class
                """,
                GetBasicResultAt(7, 36, 7, 38, "Property RequestText.Text As String", "Property RequestText.Text As String", "Property HttpRequest.Form As NameValueCollection", "Property RequestText.Text As String"));
        }

        [TestMethod]
        public async Task RepeatedWebInputWithoutReachableSinkAsync()
        {
            await VerifyCSharpWithDependenciesAsync(RepeatedWebInputWithoutReachableSink());
        }

        [TestMethod]
        public async Task WebInputWithSinkReachedThroughMethodAsync()
        {
            await VerifyCSharpWithDependenciesAsync(
                WebInputWithSinkReachedThroughMethod,
                GetCSharpResultAt(14, 9, 7, 24, "void HttpResponse.Write(string s)", "void WebForm.UseInput(IDbCommand command, string input)", "NameValueCollection HttpRequest.Form", "void WebForm.Emit(IDbCommand command)"));
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
            }.RunAsync(CancellationToken.None);
        }
    }
}
