// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        private static DiagnosticResult GetCA3075LoadXmlCSharpResultAt(int line, int column) =>
            VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("LoadXml");

        private static DiagnosticResult GetCA3075LoadXmlBasicResultAt(int line, int column) =>
            VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("LoadXml");

        [Fact]
        public async Task UseXmlDocumentLoadXmlShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class DoNotUseLoadXml
    {
        public void TestMethod(string xml)
        {
            XmlDocument doc = new XmlDocument(){ XmlResolver = null };
            doc.LoadXml(xml);
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(11, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Module TestClass
    Sub TestMethod(xml as String)
        Dim doc As XmlDocument = New XmlDocument() With { _
            .XmlResolver = Nothing _
        }
        Call doc.LoadXml(xml)
    End Sub
End Module",
                GetCA3075LoadXmlBasicResultAt(10, 14)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInGetShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

class TestClass
{
    public XmlDocument Test
    {
        get {
            var xml = """";
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.LoadXml(xml);
            return doc;
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(11, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Class TestClass
    Public ReadOnly Property Test() As XmlDocument
        Get
            Dim xml = """"
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            Call doc.LoadXml(xml)
            Return doc
        End Get
    End Property
End Class",
                GetCA3075LoadXmlBasicResultAt(11, 18)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInSetShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                XmlDocument doc = new XmlDocument() { XmlResolver = null };
                doc.LoadXml(xml);
                privateDoc = doc;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(15, 17)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Class TestClass
    Private privateDoc As XmlDocument
    Public WriteOnly Property GetDoc() As XmlDocument
        Set
            If value Is Nothing Then
                Dim xml = """"
                Dim doc As New XmlDocument() With { _
                    .XmlResolver = Nothing _
                }
                doc.LoadXml(xml)
                privateDoc = doc
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075LoadXmlBasicResultAt(13, 17)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

class TestClass
{
    private void TestMethod()
    {
        try
        {
            var xml = """";
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        }
        catch (Exception) { throw; }
        finally { }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(13, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
            Dim xml = """"
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.LoadXml(xml)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        }
        finally { }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(14, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim xml = """"
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.LoadXml(xml)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(15, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim xml = """"
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.LoadXml(xml)
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(15, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentLoadXmlInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Xml;

class TestClass
{
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var xml = """";
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075LoadXmlCSharpResultAt(12, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim xml = """"
        Dim doc As New XmlDocument() With { _
            .XmlResolver = Nothing _
        }
        doc.LoadXml(xml)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(12, 9)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class DoNotUseLoadXml
    {
        public void TestMethod1(string xml)
        {
            XmlDataDocument doc = new XmlDataDocument(){ XmlResolver = null };
            doc.LoadXml(xml);
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(11, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class DoNotUseLoadXml
        Public Sub TestMethod1(xml As String)
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.LoadXml(xml)
        End Sub
    End Class
End Namespace",
                GetCA3075LoadXmlBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInSetShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

class TestClass
{
    XmlDataDocument privateDoc;
    public XmlDataDocument SetDoc
    {
        set
        {
            if (value == null)
            {
                var xml = """";
                XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                doc.LoadXml(xml);
                privateDoc = doc;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(15, 17)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Class TestClass
    Private privateDoc As XmlDataDocument
    Public WriteOnly Property SetDoc() As XmlDataDocument
        Set
            If value Is Nothing Then
                Dim xml = """"
                Dim doc As New XmlDataDocument() With { _
                    .XmlResolver = Nothing _
                }
                doc.LoadXml(xml)
                privateDoc = doc
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075LoadXmlBasicResultAt(13, 17)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            doc.LoadXml(xml);
        }
        catch (Exception) { throw; }
        finally { }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(13, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
            Dim xml = """"
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.LoadXml(xml)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            doc.LoadXml(xml);
        }
        finally { }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(14, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
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
            doc.LoadXml(xml)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            doc.LoadXml(xml);
        }
    }
}",
                GetCA3075LoadXmlCSharpResultAt(15, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
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
            doc.LoadXml(xml)
        End Try
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(15, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Xml;

class TestClass
{
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var xml = """";
            XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075LoadXmlCSharpResultAt(12, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim xml = """"
        Dim doc As New XmlDataDocument() With { _
            .XmlResolver = Nothing _
        }
        doc.LoadXml(xml)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(12, 9)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentLoadXmlInDelegateShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Xml;

class TestClass
{
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var xml = """";
            XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
            doc.LoadXml(xml);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075LoadXmlCSharpResultAt(12, 13)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim xml = """"
        Dim doc As New XmlDataDocument() With { _
            .XmlResolver = Nothing _
        }
        doc.LoadXml(xml)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075LoadXmlBasicResultAt(12, 9)
            );
        }
    }
}

