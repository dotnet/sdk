// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ReviewCodeForXmlInjectionVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ReviewCodeForXmlInjectionVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForXmlInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForXmlInjectionVulnerabilities, ReviewCodeForXmlInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForXmlInjectionVulnerabilities.Rule;

        [Fact]
        public async Task DocSample1_CSharp_Violation_Diagnostic()
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
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlElement root = d.CreateElement(""root"");
        d.AppendChild(root);

        XmlElement allowedUser = d.CreateElement(""allowedUser"");
        root.AppendChild(allowedUser);

        allowedUser.InnerXml = ""alice"";

        // If an attacker uses this for input:
        //     some text<allowedUser>oscar</allowedUser>
        // Then the XML document will be:
        //     <root>some text<allowedUser>oscar</allowedUser></root>
        root.InnerXml = input;
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(23, 9, 9, 24, "string XmlElement.InnerXml", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample1_VB_Violation_Diagnostic()
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
Imports System.Xml

Public Partial Class WebForm
    Inherits System.Web.UI.Page

    Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Request.Form(""in"")
        Dim d As XmlDocument = New XmlDocument()
        Dim root As XmlElement = d.CreateElement(""root"")
        d.AppendChild(root)

        Dim allowedUser As XmlElement = d.CreateElement(""allowedUser"")
        root.AppendChild(allowedUser)

        allowedUser.InnerXml = ""alice""

        ' If an attacker uses this for input:
        '     some text<allowedUser>oscar</allowedUser>
        ' Then the XML document will be:
        '     <root>some text<allowedUser>oscar</allowedUser></root>
        root.InnerXml = input
    End Sub
End Class",
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(23, 9, 9, 31, "Property XmlElement.InnerXml As String", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution_NoDiagnostic()
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
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlElement root = d.CreateElement(""root"");
        d.AppendChild(root);

        XmlElement allowedUser = d.CreateElement(""allowedUser"");
        root.AppendChild(allowedUser);

        allowedUser.InnerText = ""alice"";

        // If an attacker uses this for input:
        //     some text<allowedUser>oscar</allowedUser>
        // Then the XML document will be:
        //     <root>&lt;allowedUser&gt;oscar&lt;/allowedUser&gt;some text<allowedUser>alice</allowedUser></root>
        root.InnerText = input;
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DocSample1_VB_Solution_Diagnostic()
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
Imports System.Xml

Public Partial Class WebForm
    Inherits System.Web.UI.Page

    Sub Page_Load(sender As Object, e As EventArgs)
        Dim input As String = Request.Form(""in"")
        Dim d As XmlDocument = New XmlDocument()
        Dim root As XmlElement = d.CreateElement(""root"")
        d.AppendChild(root)

        Dim allowedUser As XmlElement = d.CreateElement(""allowedUser"")
        root.AppendChild(allowedUser)

        allowedUser.InnerText = ""alice""

        ' If an attacker uses this for input:
        '     some text<allowedUser>oscar</allowedUser>
        ' Then the XML document will be:
        '     <root>&lt;allowedUser&gt;oscar&lt;/allowedUser&gt;some text<allowedUser>alice</allowedUser></root>
        root.InnerText = input
    End Sub
End Class",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task XmlAttribute_InnerXml_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlAttribute a = d.CreateAttribute(""attr"");
        a.InnerXml = input;
    }
}",
                GetCSharpResultAt(13, 9, 10, 24, "string XmlAttribute.InnerXml", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task XmlTextWriter_WriteRaw_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlTextWriter t = new XmlTextWriter(new MemoryStream(), Encoding.UTF8);
        t.WriteRaw(input);
    }
}",
                GetCSharpResultAt(14, 9, 12, 24, "void XmlTextWriter.WriteRaw(string data)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task XmlTextWriter_WriteRaw_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlTextWriter t = new XmlTextWriter(new MemoryStream(), Encoding.UTF8);
        t.WriteRaw(""<root/>"");
    }
}");
        }

        [Fact]
        public async Task XmlNotation_InnerXml_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlNotation n = (XmlNotation) d.CreateNode(XmlNodeType.Notation, String.Empty, String.Empty);
        n.InnerXml = input;
    }
}",
                GetCSharpResultAt(13, 9, 10, 24, "string XmlNotation.InnerXml", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task XmlNotation_InnerXml_AntiXssXmlEncode_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml;
using Microsoft.Security.Application;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlNotation n = (XmlNotation) d.CreateNode(XmlNodeType.Notation, String.Empty, String.Empty);
        n.InnerXml = AntiXss.XmlEncode(input);
    }
}");
        }

        [Fact]
        public async Task XmlNotation_InnerXml_AntiXssEncoderXmlEncode_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;
using System.Xml;
using System.Web.Security.AntiXss;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        XmlDocument d = new XmlDocument();
        XmlNotation n = (XmlNotation) d.CreateNode(XmlNodeType.Notation, String.Empty, String.Empty);
        n.InnerXml = AntiXssEncoder.XmlEncode(input);
    }
}");
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_XmlTextWriter_WriteRaw_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        var xtw = new XmlTextWriter(new MemoryStream(), Encoding.UTF8);
        xtw.WriteRaw(input);

        return View();
    }
}",
                GetCSharpResultAt(13, 9, 11, 24, "void XmlTextWriter.WriteRaw(string data)", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}