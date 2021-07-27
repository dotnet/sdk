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
        [Fact]
        public async Task UseXmlDocumentSetInnerXmlShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class DoNotUseSetInnerXml
    {
        public void TestMethod(string xml)
        {
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class DoNotUseSetInnerXml
        Public Sub TestMethod(xml As String)
            Dim doc As New XmlDocument() With { _
                 .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
        End Sub
    End Class
End Namespace",
                GetCA3075InnerXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInGetShouldGenerateDiagnostic()
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
            return doc;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Public ReadOnly Property Test() As XmlDocument
        Get
            Dim xml = """"
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
            Return doc
        End Get
    End Property
End Class",
                GetCA3075InnerXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInSetShouldGenerateDiagnostic()
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
                    XmlDocument doc = new XmlDocument() { XmlResolver = null };
                    doc.InnerXml = xml;
                    privateDoc = doc;
                }
                else
                    privateDoc = value;
            }
        }
}",
                GetCA3075InnerXmlCSharpResultAt(15, 21)
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
                Dim doc As New XmlDocument() With { _
                     .XmlResolver = Nothing _
                }
                doc.InnerXml = xml
                privateDoc = doc
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075InnerXmlBasicResultAt(13, 17)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInTryBlockShouldGenerateDiagnostic()
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
        }
        catch (Exception) { throw; }
        finally { }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(13, 13)
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
            Dim doc As New XmlDocument() With { _
                 .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInCatchBlockShouldGenerateDiagnostic()
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
        }
        finally { }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(14, 13)
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
            Dim doc As New XmlDocument() With { _
                 .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
        Finally
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInFinallyBlockShouldGenerateDiagnostic()
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(15, 13)
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
            Dim doc As New XmlDocument() With { _
                 .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(15, 13)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInAsyncAwaitShouldGenerateDiagnostic()
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
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            doc.InnerXml = xml;
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075InnerXmlCSharpResultAt(12, 13)
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
        Dim doc As New XmlDocument() With { _
            .XmlResolver = Nothing _
        }
        doc.InnerXml = xml

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 9)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInDelegateShouldGenerateDiagnostic()
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
        XmlDocument doc = new XmlDocument() { XmlResolver = null };
        doc.InnerXml = xml;
    };
}",
                GetCA3075InnerXmlCSharpResultAt(11, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim xml = """"
    Dim doc As New XmlDocument() With { _
        .XmlResolver = Nothing _
    }
    doc.InnerXml = xml

End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 5)
            );
        }

        [Fact]
        public async Task UseXmlDocumentSetInnerXmlInlineShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class DoNotUseSetInnerXml
    {
        public void TestMethod(string xml)
        {
            XmlDocument doc = new XmlDocument()
            {
                XmlResolver = null,
                InnerXml = xml
            };
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class DoNotUseSetInnerXml
        Public Sub TestMethod(xml As String)
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing, _
                .InnerXml = xml _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075InnerXmlBasicResultAt(10, 17)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentInnerXmlShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class DoNotUseSetInnerXml
    {
        public void TestMethod(string xml)
        {
            XmlDataDocument doc = new XmlDataDocument(){ XmlResolver = null };
            doc.InnerXml = xml;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class DoNotUseSetInnerXml
        Public Sub TestMethod(xml As String)
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing _
            }
            doc.InnerXml = xml
        End Sub
    End Class
End Namespace",
                GetCA3075InnerXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInGetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    public XmlDataDocument Test
    {
        get {
            var xml = """";
            XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
            doc.InnerXml = xml;
            return doc;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(11, 13)
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
            doc.InnerXml = xml
            Return doc
        End Get
    End Property
End Class",
                GetCA3075InnerXmlBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInSetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
XmlDataDocument privateDoc;
public XmlDataDocument GetDoc
        {
            set
            {
                if (value == null)
                {
                    var xml = """";
                    XmlDataDocument doc = new XmlDataDocument() { XmlResolver = null };
                    doc.InnerXml = xml;
                    privateDoc = doc;
                }
                else
                    privateDoc = value;
            }
        }
}",
                GetCA3075InnerXmlCSharpResultAt(15, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private privateDoc As XmlDataDocument
    Public WriteOnly Property GetDoc() As XmlDataDocument
        Set
            If value Is Nothing Then
                Dim xml = """"
                Dim doc As New XmlDataDocument() With { _
                    .XmlResolver = Nothing _
                }
                doc.InnerXml = xml
                privateDoc = doc
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class
",
                GetCA3075InnerXmlBasicResultAt(13, 17)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInTryBlockShouldGenerateDiagnostic()
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
            doc.InnerXml = xml;
        }
        catch (Exception) { throw; }
        finally { }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(13, 13)
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
            doc.InnerXml = xml
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInCatchBlockShouldGenerateDiagnostic()
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
            doc.InnerXml = xml;
        }
        finally { }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(14, 13)
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
            doc.InnerXml = xml
        Finally
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInFinallyBlockShouldGenerateDiagnostic()
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
            doc.InnerXml = xml;
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(15, 13)
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
            doc.InnerXml = xml
        End Try
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(15, 13)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInAsyncAwaitShouldGenerateDiagnostic()
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
            doc.InnerXml = xml;
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}",
                GetCA3075InnerXmlCSharpResultAt(12, 13)
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
        doc.InnerXml = xml

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 9)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInDelegateShouldGenerateDiagnostic()
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
        doc.InnerXml = xml;
    };
}",
                GetCA3075InnerXmlCSharpResultAt(11, 9)
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
    doc.InnerXml = xml

End Sub
End Class",
                GetCA3075InnerXmlBasicResultAt(12, 5)
            );
        }

        [Fact]
        public async Task UseXmlDataDocumentSetInnerXmlInlineShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;
using System.Data;

namespace TestNamespace
{
    public class DoNotUseSetInnerXml
    {
        public void TestMethod(string xml)
        {
            XmlDataDocument doc = new XmlDataDocument()
            {
                XmlResolver = null,
                InnerXml = xml
            };
        }
    }
}",
                GetCA3075InnerXmlCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml
Imports System.Data

Namespace TestNamespace
    Public Class DoNotUseSetInnerXml
        Public Sub TestMethod(xml As String)
            Dim doc As New XmlDataDocument() With { _
                .XmlResolver = Nothing, _
                .InnerXml = xml _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075InnerXmlBasicResultAt(10, 17)
            );
        }

        private static DiagnosticResult GetCA3075InnerXmlCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseSetInnerXml).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075InnerXmlBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseSetInnerXml).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
