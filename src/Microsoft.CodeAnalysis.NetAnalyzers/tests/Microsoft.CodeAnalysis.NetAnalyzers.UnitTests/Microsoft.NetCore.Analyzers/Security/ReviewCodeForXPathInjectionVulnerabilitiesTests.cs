// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXPathInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForXPathInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForXPathInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForXPathInjectionVulnerabilities, ReviewCodeForXPathInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForXPathInjectionVulnerabilities.Rule;

        [Fact]
        public async Task DocSample1_CSharp_Diagnostic_Violation()
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
using System.Xml.XPath;

public partial class WebForm : System.Web.UI.Page
{
    public XPathNavigator AuthorizedOperations { get; set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string operation = Request.Form[""operation""];

        // If an attacker uses this for input:
        //     ' or 'a' = 'a
        // Then the XPath query will be:
        //     authorizedOperation[@username = 'anonymous' and @operationName = '' or 'a' = 'a']
        // and it will return any authorizedOperation node.
        XPathNavigator node = AuthorizedOperations.SelectSingleNode(
            ""//authorizedOperation[@username = 'anonymous' and @operationName = '"" + operation + ""']"");
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(18, 31, 11, 28, "XPathNavigator XPathNavigator.SelectSingleNode(string xpath)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample1_VB_Diagnostic_Violation()
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
Imports System.Xml.XPath

Partial Public Class WebForm
    Inherits System.Web.UI.Page

    Public Property AuthorizedOperations As XPathNavigator

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim operation As String = Me.Request.Form(""operation"")
        
        ' If an attacker uses this for input:
        '     ' or 'a' = 'a
        ' Then the XPath query will be:
        '      authorizedOperation[@username = 'anonymous' and @operationName = '' or 'a' = 'a']
        ' and it will return any authorizedOperation node.
        Dim node As XPathNavigator = AuthorizedOperations.SelectSingleNode( _
            ""//authorizedOperation[@username = 'anonymous' and @operationName = '"" + operation + ""']"")
    End Sub
End Class
",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(18, 38, 11, 35, "Function XPathNavigator.SelectSingleNode(xpath As String) As XPathNavigator", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task XPathNavigator_Select_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml.XPath;

public partial class WebForm : System.Web.UI.Page
{
    public XPathNavigator XPathNavigator { get; set; }
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.XPathNavigator.Select(input);
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "XPathNodeIterator XPathNavigator.Select(string xpath)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task XPathNavigator_Select_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml.XPath;

public partial class WebForm : System.Web.UI.Page
{
    public XPathNavigator XPathNavigator { get; set; }
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.XPathNavigator.Select(""//nodes"");
    }
}");
        }

        [Fact]
        public async Task XmlNode_SelectSingleNode_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    public XmlNode XmlNode { get; set; }
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.XmlNode.SelectSingleNode(input);
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "XmlNode XmlNode.SelectSingleNode(string xpath)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task TemplateControl_XPath_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Web.UI;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    public MyTemplateControl MyTemplateControl { get; set; }
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.MyTemplateControl.UntrustedInputGoesHere(input);
    }
}

public class MyTemplateControl : TemplateControl
{
    public object UntrustedInputGoesHere(string untrustedInput)
    {
        return this.XPath(untrustedInput, (IXmlNamespaceResolver) null);
    }
}
",
                GetCSharpResultAt(21, 16, 12, 24, "object TemplateControl.XPath(string xPathExpression, IXmlNamespaceResolver resolver)", "object MyTemplateControl.UntrustedInputGoesHere(string untrustedInput)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task XmlDataSource_XPath_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Web.UI.WebControls;

public partial class WebForm : System.Web.UI.Page
{
    public XmlDataSource XmlDataSource { get; set; }
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        this.XmlDataSource.XPath = input;
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "string XmlDataSource.XPath", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }
    }
}