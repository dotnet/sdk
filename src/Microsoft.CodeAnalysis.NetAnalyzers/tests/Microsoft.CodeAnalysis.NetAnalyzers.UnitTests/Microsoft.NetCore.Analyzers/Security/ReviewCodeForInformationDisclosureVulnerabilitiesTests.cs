// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForInformationDisclosureVulnerabilitiesTests : TaintedDataAnalyzerTestBase
    {
        public ReviewCodeForInformationDisclosureVulnerabilitiesTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => ReviewCodeForInformationDisclosureVulnerabilities.Rule;

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ReviewCodeForInformationDisclosureVulnerabilities();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ReviewCodeForInformationDisclosureVulnerabilities();
        }

        [Fact]
        public void DocSample1_CSharp_Violation_Diagnostic()
        {
            this.VerifyCSharp(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs eventArgs)
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (Exception e)
        {
            this.Response.Write(e.ToString());
        }
    }
}",
                GetCSharpResultAt(15, 13, 15, 33, "void HttpResponse.Write(string s)", "void WebForm.Page_Load(object sender, EventArgs eventArgs)", "string Exception.ToString()", "void WebForm.Page_Load(object sender, EventArgs eventArgs)"));
        }

        [Fact]
        public void DocSample1_VB_Violation_Diagnostic()
        {
            this.VerifyBasic(@"
Imports System

Partial Public Class WebForm 
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Try
            Dim o As Object = Nothing
            o.ToString()
        Catch e As Exception
            Me.Response.Write(e.ToString())
        End Try
    End Sub
End Class",
                GetBasicResultAt(12, 13, 12, 31, "Sub HttpResponse.Write(s As String)", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)", "Function Exception.ToString() As String", "Sub WebForm.Page_Load(sender As Object, eventArgs As EventArgs)"));
        }

        [Fact]
        public void DocSample1_CSharp_Solution_NoDiagnostic()
        {
            this.VerifyCSharp(@"
using System;

public partial class WebForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs eventArgs)
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (Exception e)
        {
            this.Response.Write(""An error occurred. Please try again later."");
        }
    }
}");
        }

        [Fact]
        public void DocSample1_VB_Solution_NoDiagnostic()
        {
            this.VerifyBasic(@"
Imports System

Partial Public Class WebForm 
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, eventArgs As EventArgs)
        Try
            Dim o As Object = Nothing
            o.ToString()
        Catch e As Exception
            Me.Response.Write(""An error occurred. Please try again later."")
        End Try
    End Sub
End Class");
        }

        [Fact]
        public void ExceptionToString_ConsoleOutWriteLine()
        {
            this.VerifyCSharp(@"
using System;

public class Class
{
    public void Blah()
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (Exception e)
        {
            Console.Out.WriteLine(e.ToString());
        }
    }
}
");
        }

        [Fact]
        public void NullReferenceExceptionToString_HttpResponseWrite()
        {
            this.VerifyCSharp(@"
using System;
using System.Web;

public class Class
{
    public void Blah(HttpResponse response)
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (NullReferenceException nre)
        {
            response.Write(nre.ToString());
        }
    }
}
",
                GetCSharpResultAt(16, 13, 16, 28, "void HttpResponse.Write(string s)", "void Class.Blah(HttpResponse response)", "string Exception.ToString()", "void Class.Blah(HttpResponse response)"));
        }

        [Fact]
        public void NullReferenceExceptionMessage_HtmlSelectInnerHtml()
        {
            this.VerifyCSharp(@"
using System;
using System.Web.UI.HtmlControls;

public class Class
{
    public HtmlSelect Select;
    public void Blah()
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (NullReferenceException nre)
        {
            Select.InnerHtml = nre.Message;
        }
    }
}
",
                GetCSharpResultAt(17, 13, 17, 32, "string HtmlSelect.InnerHtml", "void Class.Blah()", "string Exception.Message", "void Class.Blah()"));
        }

        [Fact]
        public void NullReferenceExceptionStackTrace_BulletedListText()
        {
            this.VerifyCSharp(@"
using System;
using System.Web.UI.WebControls;

public class Class
{
    public BulletedList BulletedList;
    public void Blah()
    {
        try
        {
            object o = null;
            o.ToString();
        }
        catch (NullReferenceException nre)
        {
            this.BulletedList.Text = nre.StackTrace;
        }
    }
}
",
                GetCSharpResultAt(17, 13, 17, 38, "string BulletedList.Text", "void Class.Blah()", "string Exception.StackTrace", "void Class.Blah()"));
        }

        [Fact]
        public void TryUsingTryUsingTry_NoDiagnostic()
        {
            this.VerifyCSharp(@"
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

public class Class
{
    public BulletedList BulletedList;

    public async Task DoSomethingNotReallyAsync(Stream stream)
    {
        try
        {
            using (stream)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(stream))
                    {
                        ValidateStreamIsNotMemoryStream(stream);
                        sw.Write(""Hello world!"");
                    }
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            
        }
    }

    private static void ValidateStreamIsNotMemoryStream(Stream stream)
    {
        if (stream is MemoryStream)
        {
            throw new ArgumentException(nameof(stream));
        }
    }
}
");
        }

        [Fact, WorkItem(2457, "https://github.com/dotnet/roslyn-analyzers/issues/2457")]
        public void PredicateAnalysisAssert_PredicatedOnNonBoolEntity()
        {
            this.VerifyCSharp(@"
using System;
using System.Web.UI.WebControls;

public class Class
{
    public int? AProperty { get; set; }

    public void Blah()
    {
        try
        {
            Class c = new Class();
            Second(c);
            object o = null;
            o.ToString();
        }
        catch (NullReferenceException nre)
        {
            Console.WriteLine(nre.StackTrace);
        }
    }

    public void Second(Class c)
    {
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }

        Third(c);
    }

    public void Third(Class c)
    {
        Console.WriteLine(""c was null = {0}, c.AProperty = {1}"", c == null, c == null ? ""(null)"" : c.AProperty.GetValueOrDefault(-1).ToString());
    }
}");
        }
    }
}