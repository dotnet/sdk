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
        private static DiagnosticResult GetCA3075LoadCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("Load");
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075LoadBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("Load");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseXmlDocumentLoadShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            var doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(path);
        }
    }
}
",
                GetCA3075LoadCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim doc = New XmlDocument()
            doc.XmlResolver = Nothing
            doc.Load(path)
        End Sub
    End Class
End Namespace",
                GetCA3075LoadBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInGetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    public XmlDocument Test
    {
        get {
            var xml = """";
            var doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(xml);
            return doc;
        }
    }
}",
                GetCA3075LoadCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Public ReadOnly Property Test() As XmlDocument
        Get
            Dim xml = """"
            Dim doc = New XmlDocument()
            doc.XmlResolver = Nothing
            doc.Load(xml)
            Return doc
        End Get
    End Property
End Class",
                GetCA3075LoadBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInSetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    XmlDocument privateDoc;
    public XmlDocument GetDoc
    {
        set
        {
            if (value == null)
            {
                var xml = """";
                var doc = new XmlDocument();
                doc.XmlResolver = null;
                doc.Load(xml);
                privateDoc = doc;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075LoadCSharpResultAt(16, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private privateDoc As XmlDocument
    Public WriteOnly Property GetDoc() As XmlDocument
        Set
            If value Is Nothing Then
                Dim xml = """"
                Dim doc = New XmlDocument()
                doc.XmlResolver = Nothing
                doc.Load(xml)
                privateDoc = doc
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075LoadBasicResultAt(12, 17)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
  using System;
  using System.Xml;

    class TestClass
    {
        private void TestMethod()
        {
            try
            {
                var xml = """";
                var doc = new XmlDocument();
                doc.XmlResolver = null;
                doc.Load(xml);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075LoadCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
            Dim xml = """"
            Dim doc = New XmlDocument()
            doc.XmlResolver = Nothing
            doc.Load(xml)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Xml;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception)
            {
                var xml = """";
                var doc = new XmlDocument();
                doc.XmlResolver = null;
                doc.Load(xml);
            }
            finally { }
        }
    }",
                GetCA3075LoadCSharpResultAt(15, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim xml = """"
            Dim doc = New XmlDocument()
            doc.XmlResolver = Nothing
            doc.Load(xml)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

class TestClass
{
    private void TestMethod()
    {
        try { }
        catch (Exception) { throw; }
        finally
        {
            var xml = """";
            var doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(xml);
        }
    }
}",
                GetCA3075LoadCSharpResultAt(16, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim xml = """"
            Dim doc = New XmlDocument()
            doc.XmlResolver = Nothing
            doc.Load(xml)
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(14, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Threading.Tasks;
using System.Xml;

class TestClass
{
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var xml = """";
            var doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(xml);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075LoadCSharpResultAt(13, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
                Dim xml = """"
                Dim doc = New XmlDocument()
                doc.XmlResolver = Nothing
                doc.Load(xml)
            End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadInDelegateShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    delegate void Del();

    Del d = delegate () {
        var xml = """";
        var doc = new XmlDocument();
        doc.XmlResolver = null;
        doc.Load(xml);
    };
}",
                GetCA3075LoadCSharpResultAt(12, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim xml = """"
    Dim doc = New XmlDocument()
    doc.XmlResolver = Nothing
    doc.Load(xml)

End Sub
End Class",
                GetCA3075LoadBasicResultAt(11, 5)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            new XmlDataDocument().Load(path);
        }
    }
}
",
                GetCA3075LoadCSharpResultAt(10, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Call New XmlDataDocument().Load(path)
        End Sub
    End Class
End Namespace",
                GetCA3075LoadBasicResultAt(7, 18)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInGetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    public XmlDataDocument Test
    {
        get
        {
            var xml = """";
            XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
            doc.Load(xml);
            return doc;
        }
    }
}",
                GetCA3075LoadCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Public ReadOnly Property Test() As XmlDataDocument
        Get
            Dim xml = """"
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.Load(xml)
            Return doc
        End Get
    End Property
End Class",
                GetCA3075LoadBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
  using System;
  using System.Xml;

    class TestClass
    {
        private void TestMethod()
        {
            try
            {
                var xml = """";
                XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                doc.Load(xml);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075LoadCSharpResultAt(13, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
            Dim xml = """"
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.Load(xml)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Xml;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception)
            {
                var xml = """";
                XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                doc.Load(xml);
            }
            finally { }
        }
    }",
                GetCA3075LoadCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim xml = """"
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.Load(xml)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
    using System.Xml;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception) { throw; }
            finally
            {
                var xml = """";
                XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                doc.Load(xml);
            }
        }
    }",
                GetCA3075LoadCSharpResultAt(15, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim xml = """"
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.Load(xml)
        End Try
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(15, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
 using System.Threading.Tasks;
 using System.Xml;

    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => {
                var xml = """";
                XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                doc.Load(xml);
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                GetCA3075LoadCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim xml = """"
        Dim doc As New XmlDataDocument() With { _
            .XmlResolver = Nothing _
        }
        doc.Load(xml)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075LoadBasicResultAt(12, 9)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadInDelegateShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    delegate void Del();

    Del d = delegate () {
        var xml = """";
        XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
        doc.Load(xml);
    };
}",
                GetCA3075LoadCSharpResultAt(11, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim xml = """"
    Dim doc As New XmlDataDocument() With { _
        .XmlResolver = Nothing _
    }
    doc.Load(xml)

End Sub
End Class",
                GetCA3075LoadBasicResultAt(12, 5)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadWithXmlReaderParameterShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            new XmlDocument(){ XmlResolver = null }.Load(reader); 
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Call New XmlDocument() With { _
                .XmlResolver = Nothing _
            }.Load(reader)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadWithXmlReaderParameterShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            var doc = new XmlDataDocument(){XmlResolver = null};
            doc.Load(reader); 
        }
    }
}"

            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Dim doc = New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.Load(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}