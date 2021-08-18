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
        ////private static DiagnosticResult GetCA3075XmlReaderCreateWrongOverloadCSharpResultAt(int line, int column)
        ////    => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlReaderCreateWrongOverload).WithLocation(line, column);

        ////private static DiagnosticResult GetCA3075XmlReaderCreateWrongOverloadBasicResultAt(int line, int column)
        ////    => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlReaderCreateWrongOverload).WithLocation(line, column);

        [Fact]
        public async Task UseXmlReaderCreateWrongOverloadShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var reader = XmlTextReader.Create(""doc.xml"");
        }
    }
}");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim reader = XmlTextReader.Create(""doc.xml"")
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInGetShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    
    public XmlReader Test
    {
        get {
            XmlReader reader = XmlTextReader.Create(""doc.xml"");
            return reader;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass

    Public ReadOnly Property Test() As XmlReader
        Get
            Dim reader As XmlReader = XmlTextReader.Create(""doc.xml"")
            Return reader
        End Get
    End Property
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInSetShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass1
{
    XmlReader reader;
    public XmlReader Test
    {
        set
        {
            if (value == null)
                reader = XmlTextReader.Create(""doc.xml"");
            else
                reader = value;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass1
    Private reader As XmlReader
    Public WriteOnly Property Test() As XmlReader
        Set
            If value Is Nothing Then
                reader = XmlTextReader.Create(""doc.xml"")
            Else
                reader = value
            End If
        End Set
    End Property
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInTryShouldNotGenerateDiagnosticAsync()
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
            var reader = XmlTextReader.Create(""doc.xml"");
        }
        catch (Exception) { throw; }
        finally { }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass
    Private Sub TestMethod()
        Try
            Dim reader = XmlTextReader.Create(""doc.xml"")
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInCatchShouldNotGenerateDiagnosticAsync()
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
        try {        }
        catch (Exception) { 
            var reader = XmlTextReader.Create(""doc.xml"");
        }
        finally { }
    }
}"
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
            Dim reader = XmlTextReader.Create(""doc.xml"")
        Finally
        End Try
    End Sub
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInFinallyShouldNotGenerateDiagnosticAsync()
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
        try {        }
        catch (Exception) { throw; }
        finally {
            var reader = XmlTextReader.Create(""doc.xml"");
        }
    }
}"
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
            Dim reader = XmlTextReader.Create(""doc.xml"")
        End Try
    End Sub
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInAsyncAwaitShouldNotGenerateDiagnosticAsync()
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
        await Task.Run(() => { var reader = XmlTextReader.Create(""doc.xml""); });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim reader = XmlTextReader.Create(""doc.xml"")

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateInsecureOverloadInDelegateShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

class TestClass
{
    delegate void Del();

    Del d = delegate () { var reader = XmlTextReader.Create(""doc.xml""); };
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
                            Dim reader = XmlTextReader.Create(""doc.xml"")
                       End Sub
End Class"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateTextReaderOnlyOverloadShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var reader = XmlTextReader.Create(new StringReader(""<root> </root>""));
        }
    }
}"
            );
        }

        [Fact]
        public async Task UseXmlReaderCreateStreamOnlyOverloadShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Text;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var reader = XmlTextReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(""<root> </root>"")));
        }
    }
}"
            );
        }
    }
}
