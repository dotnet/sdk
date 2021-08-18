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
        private static DiagnosticResult GetCA3075DataSetReadXmlCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXml");
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075DataSetReadXmlBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("ReadXml");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseDataSetReadXmlShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataSetReadXml
    {
        public void TestMethod1214(string path)
        {
            DataSet ds = new DataSet();
            ds.ReadXml(path);
        }
    }
}
",
                GetCA3075DataSetReadXmlCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class UseXmlReaderForDataSetReadXml
        Public Sub TestMethod1214(path As String)
            Dim ds As New DataSet()
            ds.ReadXml(path)
        End Sub
    End Class
End Namespace",
                GetCA3075DataSetReadXmlBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInGetShouldGenerateDiagnosticAsync()
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
            ds.ReadXml(src);
            return ds;
        }
    }
}",
                GetCA3075DataSetReadXmlCSharpResultAt(11, 13)
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
            ds.ReadXml(src)
            Return ds
        End Get
    End Property
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInSetShouldGenerateDiagnosticAsync()
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
                    ds.ReadXml(src);
                    privateDoc = ds;
                }
                else
                    privateDoc = value;
            }
        }
}",
                GetCA3075DataSetReadXmlCSharpResultAt(15, 21)
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
                ds.ReadXml(src)
                privateDoc = ds
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInTryBlockShouldGenerateDiagnosticAsync()
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
                ds.ReadXml(src);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075DataSetReadXmlCSharpResultAt(13, 17)
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
            ds.ReadXml(src)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInCatchBlockShouldGenerateDiagnosticAsync()
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
                ds.ReadXml(src);
            }
            finally { }
        }
    }",
                GetCA3075DataSetReadXmlCSharpResultAt(14, 17)
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
            ds.ReadXml(src)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInFinallyBlockShouldGenerateDiagnosticAsync()
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
                ds.ReadXml(src);
            }
        }
    }",
                GetCA3075DataSetReadXmlCSharpResultAt(15, 17)
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
            ds.ReadXml(src)
        End Try
    End Sub
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInAsyncAwaitShouldGenerateDiagnosticAsync()
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
                ds.ReadXml(src);
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                GetCA3075DataSetReadXmlCSharpResultAt(12, 17)
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
        ds.ReadXml(src)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlInDelegateShouldGenerateDiagnosticAsync()
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
        ds.ReadXml(src);
    };
}",
                GetCA3075DataSetReadXmlCSharpResultAt(11, 9)
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
    ds.ReadXml(src)

End Sub
End Class",
                GetCA3075DataSetReadXmlBasicResultAt(10, 5)
            );
        }

        [Fact]
        public async Task UseDataSetReadXmlWithXmlReaderShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class UseXmlReaderForDataSetReadXml
    {
        public void TestMethod1214Ok(XmlReader reader)
        {
            DataSet ds = new DataSet();
            ds.ReadXml(reader);
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
    Public Class UseXmlReaderForDataSetReadXml
        Public Sub TestMethod1214Ok(reader As XmlReader)
            Dim ds As New DataSet()
            ds.ReadXml(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}