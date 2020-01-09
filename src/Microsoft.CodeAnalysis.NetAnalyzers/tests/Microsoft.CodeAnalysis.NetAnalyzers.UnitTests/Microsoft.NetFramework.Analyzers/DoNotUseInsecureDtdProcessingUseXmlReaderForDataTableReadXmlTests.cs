// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingAnalyzerTests
    {
        private static DiagnosticResult GetCA3075DataTableReadXmlCSharpResultAt(int line, int column)
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseInsecureDtdProcessing).WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.DoNotUseDtdProcessingOverloadsMessage, "ReadXml"));
        }

        private DiagnosticResult GetCA3075DataTableReadXmlBasicResultAt(int line, int column)
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseInsecureDtdProcessing).WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.DoNotUseDtdProcessingOverloadsMessage, "ReadXml"));
        }

        [Fact]
        public async Task UseDataTableReadXmlShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataTableReadXml
    {
        public void TestMethod(Stream stream)
        {
            DataTable table = new DataTable();
            table.ReadXml(stream);
        }
    }
}
",
                GetCA3075DataTableReadXmlCSharpResultAt(13, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class UseXmlReaderForDataTableReadXml
        Public Sub TestMethod(stream As Stream)
            Dim table As New DataTable()
            table.ReadXml(stream)
        End Sub
    End Class
End Namespace",
                GetCA3075DataTableReadXmlBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInGetShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Data;

class TestClass
{
    public DataTable Test
    {
        get {
            var src = """";
            DataTable dt = new DataTable();
            dt.ReadXml(src);
            return dt;
        }
    }
}",
                GetCA3075DataTableReadXmlCSharpResultAt(11, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Data

Class TestClass
    Public ReadOnly Property Test() As DataTable
        Get
            Dim src = """"
            Dim dt As New DataTable()
            dt.ReadXml(src)
            Return dt
        End Get
    End Property
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInSetShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Data;

class TestClass
{
    DataTable privateDoc;
    public DataTable GetDoc
    {
        set
        {
            if (value == null)
            {
                var src = """";
                DataTable dt = new DataTable();
                dt.ReadXml(src);
                privateDoc = dt;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075DataTableReadXmlCSharpResultAt(15, 17)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Data

Class TestClass
    Private privateDoc As DataTable
    Public WriteOnly Property GetDoc() As DataTable
        Set
            If value Is Nothing Then
                Dim src = """"
                Dim dt As New DataTable()
                dt.ReadXml(src)
                privateDoc = dt
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
  using System;
  using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try
            {
                var src = """";
                DataTable dt = new DataTable();
                dt.ReadXml(src);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075DataTableReadXmlCSharpResultAt(13, 17)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
            Dim src = """"
            Dim dt As New DataTable()
            dt.ReadXml(src)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
   using System;
   using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception)
            {
                var src = """";
                DataTable dt = new DataTable();
                dt.ReadXml(src);
            }
            finally { }
        }
    }",
                GetCA3075DataTableReadXmlCSharpResultAt(14, 17)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim src = """"
            Dim dt As New DataTable()
            dt.ReadXml(src)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;

class TestClass
{
    private void TestMethod()
    {
        try { }
        catch (Exception) { throw; }
        finally
        {
            var src = """";
            DataTable dt = new DataTable();
            dt.ReadXml(src);
        }
    }
}",
                GetCA3075DataTableReadXmlCSharpResultAt(15, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim src = """"
            Dim dt As New DataTable()
            dt.ReadXml(src)
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Data;

class TestClass
{
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var src = """";
            DataTable dt = new DataTable();
            dt.ReadXml(src);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075DataTableReadXmlCSharpResultAt(12, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Data

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
            Dim src = """"
            Dim dt As New DataTable()
            dt.ReadXml(src)
            End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlInDelegateShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Data;

class TestClass
{
    delegate void Del();

    Del d = delegate () {
        var src = """";
        DataTable dt = new DataTable();
        dt.ReadXml(src);
    };
}",
                GetCA3075DataTableReadXmlCSharpResultAt(11, 9)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Data

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim src = """"
    Dim dt As New DataTable()
    dt.ReadXml(src)

End Sub
End Class",
                GetCA3075DataTableReadXmlBasicResultAt(10, 5)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlWithXmlReaderShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataTableReadXml
    {
        public void TestMethod(XmlReader reader)
        {
            DataTable table = new DataTable();
            table.ReadXml(reader);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class UseXmlReaderForDataTableReadXml
        Public Sub TestMethod(reader As XmlReader)
            Dim table As New DataTable()
            table.ReadXml(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}