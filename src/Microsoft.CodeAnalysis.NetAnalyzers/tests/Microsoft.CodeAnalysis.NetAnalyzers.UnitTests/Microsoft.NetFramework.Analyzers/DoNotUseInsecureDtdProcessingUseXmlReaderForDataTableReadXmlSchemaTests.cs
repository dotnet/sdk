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
        private static DiagnosticResult GetCA3075DataTableReadXmlSchemaCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXmlSchema");
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075DataTableReadXmlSchemaBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXmlSchema");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseDataTableReadXmlSchemaShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataTableReadXmlSchema
    {
        public void TestMethod(Stream stream)
        {
            DataTable table = new DataTable();
            table.ReadXmlSchema(stream);
        }
    }
}
",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(13, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class UseXmlReaderForDataTableReadXmlSchema
        Public Sub TestMethod(stream As Stream)
            Dim table As New DataTable()
            table.ReadXmlSchema(stream)
        End Sub
    End Class
End Namespace",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInGetShouldGenerateDiagnosticAsync()
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
            dt.ReadXmlSchema(src);
            return dt;
        }
    }
}",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(11, 13)
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
            dt.ReadXmlSchema(src)
            Return dt
        End Get
    End Property
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInSetShouldGenerateDiagnosticAsync()
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
                dt.ReadXmlSchema(src);
                privateDoc = dt;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(15, 17)
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
                dt.ReadXmlSchema(src)
                privateDoc = dt
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInTryBlockShouldGenerateDiagnosticAsync()
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
                dt.ReadXmlSchema(src);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(13, 17)
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
            dt.ReadXmlSchema(src)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInCatchBlockShouldGenerateDiagnosticAsync()
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
                dt.ReadXmlSchema(src);
            }
            finally { }
        }
    }",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(14, 17)
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
            dt.ReadXmlSchema(src)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInFinallyBlockShouldGenerateDiagnosticAsync()
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
                dt.ReadXmlSchema(src);
            }
        }
    }",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(15, 17)
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
            dt.ReadXmlSchema(src)
        End Try
    End Sub
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInAsyncAwaitShouldGenerateDiagnosticAsync()
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
                dt.ReadXmlSchema(src);
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(12, 17)
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
        dt.ReadXmlSchema(src)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaInDelegateShouldGenerateDiagnosticAsync()
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
        dt.ReadXmlSchema(src);
    };
}",
                GetCA3075DataTableReadXmlSchemaCSharpResultAt(11, 9)
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
    dt.ReadXmlSchema(src)

End Sub
End Class",
                GetCA3075DataTableReadXmlSchemaBasicResultAt(10, 5)
            );
        }

        [Fact]
        public async Task UseDataTableReadXmlSchemaWithXmlReaderShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataTableReadXmlSchema
    {
        public void TestMethod(XmlReader reader)
        {
            DataTable table = new DataTable();
            table.ReadXmlSchema(reader);
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
    Public Class UseXmlReaderForDataTableReadXmlSchema
        Public Sub TestMethod(reader As XmlReader)
            Dim table As New DataTable()
            table.ReadXmlSchema(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}