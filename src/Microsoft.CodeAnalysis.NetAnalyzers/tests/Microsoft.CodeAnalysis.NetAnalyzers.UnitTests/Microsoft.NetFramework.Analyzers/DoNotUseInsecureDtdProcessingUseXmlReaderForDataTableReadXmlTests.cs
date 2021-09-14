// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXml");
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075DataTableReadXmlBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXml");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseDataTableReadXmlShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
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