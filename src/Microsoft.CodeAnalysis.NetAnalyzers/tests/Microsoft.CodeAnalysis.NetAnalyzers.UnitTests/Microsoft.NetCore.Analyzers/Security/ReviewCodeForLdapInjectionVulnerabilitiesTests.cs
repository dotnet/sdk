// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities.MinimalImplementations;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForLdapInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase
    {
        public ReviewCodeForLdapInjectionVulnerabilitiesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => ReviewCodeForLdapInjectionVulnerabilities.Rule;

        protected override IEnumerable<string> AdditionalCSharpSources => new string[] { AntiXssApis.CSharp };

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ReviewCodeForLdapInjectionVulnerabilities();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ReviewCodeForLdapInjectionVulnerabilities();
        }

        [Fact]
        public void DocSample1_CSharp_Violation_Diagnostic()
        {
            VerifyCSharp(@"
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
                GetCSharpResultAt(16, 38, 9, 27, "DirectorySearcher.DirectorySearcher(string filter)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Params", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void DocSample1_VB_Violation_Diagnostic()
        {
            VerifyBasic(@"
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
                GetBasicResultAt(16, 45, 9, 34, "Sub DirectorySearcher.New(filter As String)", "Sub WebForm.Page_Load(send As Object, e As EventArgs)", "Property HttpRequest.Params As NameValueCollection", "Sub WebForm.Page_Load(send As Object, e As EventArgs)"));
        }

        [Fact]
        public void DirectoryEntry_Path_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
        public void DirectoryEntry_Username_NoDiagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
        public void DirectorySearcher_Filter_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
        public void DirectoryEntry_Path_Sanitized_NoDiagnostic()
        {
            VerifyCSharpWithDependencies(@"
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
    }
}
