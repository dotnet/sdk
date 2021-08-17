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
        private DiagnosticResult CA3075ReadXmlSchemaGetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXmlSchema");
#pragma warning restore RS0030 // Do not used banned APIs

        private DiagnosticResult CA3075ReadXmlSchemaGetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXmlSchema");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseDataSetReadXmlSchemaShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataSetReadXmlSchema
    {
        public void TestMethod(string path)
        {
            DataSet ds = new DataSet();
            ds.ReadXmlSchema(path);
        }
    }
}
",
                CA3075ReadXmlSchemaGetCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Namespace TestNamespace
    Public Class UseXmlReaderForDataSetReadXmlSchema
        Public Sub TestMethod(path As String)
            Dim ds As New DataSet()
            ds.ReadXmlSchema(path)
        End Sub
    End Class
End Namespace",
                CA3075ReadXmlSchemaGetBasicResultAt(8, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInGetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
    public DataSet Test
    {
        get {
            var src = """";
            DataSet ds = new DataSet();
            ds.ReadXmlSchema(src);
            return ds;
        }
    }
}",
                CA3075ReadXmlSchemaGetCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Public ReadOnly Property Test() As DataSet
        Get
            Dim src = """"
            Dim ds As New DataSet()
            ds.ReadXmlSchema(src)
            Return ds
        End Get
    End Property
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInSetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
DataSet privateDoc;
public DataSet GetDoc
        {
            set
            {
                if (value == null)
                {
                    var src = """";
                    DataSet ds = new DataSet();
                    ds.ReadXmlSchema(src);
                    privateDoc = ds;
                }
                else
                    privateDoc = value;
            }
        }
}",
                CA3075ReadXmlSchemaGetCSharpResultAt(15, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private privateDoc As DataSet
    Public WriteOnly Property GetDoc() As DataSet
        Set
            If value Is Nothing Then
                Dim src = """"
                Dim ds As New DataSet()
                ds.ReadXmlSchema(src)
                privateDoc = ds
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInTryBlockShouldGenerateDiagnostic()
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
                DataSet ds = new DataSet();
                ds.ReadXmlSchema(src);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                CA3075ReadXmlSchemaGetCSharpResultAt(13, 17)
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
            Dim ds As New DataSet()
            ds.ReadXmlSchema(src)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInCatchBlockShouldGenerateDiagnostic()
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
                DataSet ds = new DataSet();
                ds.ReadXmlSchema(src);
            }
            finally { }
        }
    }",
                CA3075ReadXmlSchemaGetCSharpResultAt(14, 17)
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
            Dim ds As New DataSet()
            ds.ReadXmlSchema(src)
        Finally
        End Try
    End Sub
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInFinallyBlockShouldGenerateDiagnostic()
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
                DataSet ds = new DataSet();
                ds.ReadXmlSchema(src);
            }
        }
    }",
                CA3075ReadXmlSchemaGetCSharpResultAt(15, 17)
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
            Dim ds As New DataSet()
            ds.ReadXmlSchema(src)
        End Try
    End Sub
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInAsyncAwaitShouldGenerateDiagnostic()
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
                DataSet ds = new DataSet();
                ds.ReadXmlSchema(src);
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                CA3075ReadXmlSchemaGetCSharpResultAt(12, 17)
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
        Dim ds As New DataSet()
        ds.ReadXmlSchema(src)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaInDelegateShouldGenerateDiagnostic()
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
        DataSet ds = new DataSet();
        ds.ReadXmlSchema(src);
    };
}",
                CA3075ReadXmlSchemaGetCSharpResultAt(11, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim src = """"
    Dim ds As New DataSet()
    ds.ReadXmlSchema(src)

End Sub
End Class",
                CA3075ReadXmlSchemaGetBasicResultAt(10, 5)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlSchemaWithXmlReaderShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataSetReadXmlSchema
    {
        public void TestMethod(XmlReader reader)
        {
            DataSet ds = new DataSet();
            ds.ReadXmlSchema(reader);
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
    Public Class UseXmlReaderForDataSetReadXmlSchema
        Public Sub TestMethod(reader As XmlReader)
            Dim ds As New DataSet()
            ds.ReadXmlSchema(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}