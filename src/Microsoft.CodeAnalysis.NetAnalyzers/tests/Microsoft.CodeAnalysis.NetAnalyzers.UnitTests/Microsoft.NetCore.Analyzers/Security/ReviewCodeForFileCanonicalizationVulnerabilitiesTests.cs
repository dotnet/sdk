// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForFileCanonicalizationVulnerabilitiesTests : TaintedDataAnalyzerTestBase
    {
        public ReviewCodeForFileCanonicalizationVulnerabilitiesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => ReviewCodeForFilePathInjectionVulnerabilities.Rule;

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ReviewCodeForFilePathInjectionVulnerabilities();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ReviewCodeForFilePathInjectionVulnerabilities();
        }

        [Fact]
        public void DocSample1_CSharp_Violation_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.IO;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string userInput = Request.Params[""UserInput""];
        // Assume the following directory structure:
        //   wwwroot\currentWebDirectory\user1.txt
        //   wwwroot\currentWebDirectory\user2.txt
        //   wwwroot\secret\allsecrets.txt
        // There is nothing wrong if the user inputs:
        //   user1.txt
        // However, if the user input is: 
        //   ..\secret\allsecrets.txt
        // Then an attacker can now see all the secrets.

        // Avoid this:
        using (File.Open(userInput, FileMode.Open))
        {
            // Read a file with the name supplied by user
            // Input through request's query string and display 
            // The content to the webpage. 
        }
    }
}",
                GetCSharpResultAt(21, 16, 9, 28, "FileStream File.Open(string path, FileMode mode)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Params", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void DocSample1_VB_Violation_Diagnostic()
        {
            VerifyBasicWithDependencies(@"
Imports System
Imports System.IO

Partial Public Class WebForm 
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs)
        Dim userInput As String = Me.Request.Params(""UserInput"")
        ' Assume the following directory structure:
        '   wwwroot\currentWebDirectory\user1.txt
        '   wwwroot\currentWebDirectory\user2.txt
        '   wwwroot\secret\allsecrets.txt
        ' There is nothing wrong if the user inputs:
        '   user1.txt
        ' However, if the user input is: 
        '   ..\secret\allsecrets.txt
        ' Then an attacker can now see all the secrets.

        ' Avoid this:
        Using File.Open(userInput, FileMode.Open)
            ' Read a file with the name supplied by user
            ' Input through request's query string and display 
            ' The content to the webpage. 
        End Using
    End Sub
End Class",
                GetBasicResultAt(21, 15, 9, 35, "Function File.Open(path As String, mode As FileMode) As FileStream", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Params As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"));
        }

        [Fact]
        public void File_ReadAllText_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        File.ReadAllText(input);
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "string File.ReadAllText(string path)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void FileInfo_Constructor_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        new FileInfo(input);
    }
}",
                GetCSharpResultAt(12, 9, 11, 24, "FileInfo.FileInfo(string fileName)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public void File_ReadAllText_NoDiagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string input = Request.Form[""in""];
        input = File.ReadAllText(""file.txt"");
    }
}");
        }
    }
}
