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
        public async Task XmlReaderSettingsDefaultAsFieldShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public XmlReaderSettings settings = new XmlReaderSettings();

        public void TestMethod(string path)
        {
            var reader = XmlReader.Create(path, settings);  // analyzer only looks at a code block, so the field's state is unknown
        }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public settings As New XmlReaderSettings()

        Public Sub TestMethod(path As String)
            Dim reader = XmlReader.Create(path, settings)
            ' analyzer only looks at a code block, so the field's state is unknown
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsAsFieldSetDtdProcessingToParseWithNoCreateShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public XmlReaderSettings settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse }; 

        public void TestMethod(){}
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public settings As New XmlReaderSettings() With { _
            .DtdProcessing = DtdProcessing.Parse _
        }

        Public Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsAsFieldDefaultAndDtdProcessingToIgnoreShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public XmlReaderSettings settings = new XmlReaderSettings(); 

        public void TestMethod(string path)
        {
            this.settings.DtdProcessing = DtdProcessing.Prohibit;
            var reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public settings As New XmlReaderSettings()

        Public Sub TestMethod(path As String)
            Me.settings.DtdProcessing = DtdProcessing.Prohibit
            Dim reader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputSetDtdProcessingToParseShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path, XmlReaderSettings settings)
        {
            var reader = XmlReader.Create(path, settings);
        }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String, settings As XmlReaderSettings)
            Dim reader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInGetShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

public class TestClass
{
    XmlReaderSettings settings;
    public XmlReader Test
    {
        get
        {
            var xml = """";
            XmlReader reader = XmlReader.Create(xml, settings);
            return reader;
        }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Public Class TestClass
    Private settings As XmlReaderSettings
    Public ReadOnly Property Test() As XmlReader
        Get
            Dim xml = """"
            Dim reader As XmlReader = XmlReader.Create(xml, settings)
            Return reader
        End Get
    End Property
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInTryShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

class TestClass6a
{
    XmlReaderSettings settings;
    private void TestMethod()
    {
        try
        {
            var xml = """";
            var reader = XmlReader.Create(xml, settings);
        }
        catch (Exception) { throw; }
        finally { }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass6a
    Private settings As XmlReaderSettings
    Private Sub TestMethod()
        Try
            Dim xml = """"
            Dim reader = XmlReader.Create(xml, settings)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInCatchShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

class TestClass6a
{
    XmlReaderSettings settings;
    private void TestMethod()
    {
        try {        }
        catch (Exception) { 
            var xml = """";
            var reader = XmlReader.Create(xml, settings);
        }
        finally { }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass6a
    Private settings As XmlReaderSettings
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim xml = """"
            Dim reader = XmlReader.Create(xml, settings)
        Finally
        End Try
    End Sub
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInFinallyShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

class TestClass6a
{
    XmlReaderSettings settings;
    private void TestMethod()
    {
        try {  }
        catch (Exception) { throw; }
        finally { 
            var xml = """";
            var reader = XmlReader.Create(xml, settings);
        }
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Xml

Class TestClass6a
    Private settings As XmlReaderSettings
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim xml = """"
            Dim reader = XmlReader.Create(xml, settings)
        End Try
    End Sub
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInAsyncAwaitShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Threading.Tasks;
using System.Xml;

class TestClass
{
    XmlReaderSettings settings;
    private async Task TestMethod()
    {
        await Task.Run(() => {
            var xml = """";
            var reader = XmlReader.Create(xml, settings);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod();
    }
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Xml

Class TestClass
    Private settings As XmlReaderSettings
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim xml = """"
        Dim reader = XmlReader.Create(xml, settings)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputInDelegateShouldNotGenerateDiagnosticAsync()
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
        XmlReaderSettings settings = null;
        var reader = XmlReader.Create(xml, settings);
    };
}
");

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Class TestClass

    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim xml = """"
    Dim settings As XmlReaderSettings = Nothing
    Dim reader = XmlReader.Create(xml, settings)

End Sub
End Class");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputSetDtdProcessingToProhibitShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path, XmlReaderSettings settings)
        {
            settings.DtdProcessing = DtdProcessing.Prohibit;
            var reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String, settings As XmlReaderSettings)
            settings.DtdProcessing = DtdProcessing.Prohibit
            Dim reader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsAsInputSetPropertiesToSecureValuesShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path, XmlReaderSettings settings)
        {
            settings.DtdProcessing = DtdProcessing.Parse;
            settings.MaxCharactersFromEntities = (long)1e7;
            settings.XmlResolver = null;
            var reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String, settings As XmlReaderSettings)
            settings.DtdProcessing = DtdProcessing.Parse
            settings.MaxCharactersFromEntities = CLng(10000000.0)
            settings.XmlResolver = Nothing
            Dim reader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task RealCodeSnippetFromCustomerPre452ShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System;
using System.IO;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {         
        public static string TestMethod(string inputRule)
        {
            string outputRule;
            try
            {
                XmlDocument xmlDoc = new XmlDocument();         // CA3075 for not setting secure Xml resolver
                StringReader stringReader = new StringReader(inputRule);
                XmlTextReader textReader = new XmlTextReader(stringReader)
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null
                };
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Auto,
                    IgnoreComments = true,
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null
                };
                XmlReader reader = XmlReader.Create(textReader, settings);
                xmlDoc.Load(reader);
                XmlAttribute enabledAttribute = xmlDoc.CreateAttribute(""enabled"");
                XmlAttributeCollection ruleAttrColl = xmlDoc.DocumentElement.Attributes;
                XmlAttribute nameAttribute = (XmlAttribute)ruleAttrColl.GetNamedItem(""name"");
                ruleAttrColl.Remove(ruleAttrColl[""enabled""]);
                ruleAttrColl.InsertAfter(enabledAttribute, nameAttribute);
                outputRule = xmlDoc.OuterXml;
            }
            catch (XmlException e)
            {
                throw new Exception(""Compliance policy parsing error"", e);
            }
            return outputRule;
        }
    }
}
",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(15, 38)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System
Imports System.IO
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public Shared Function TestMethod(inputRule As String) As String
            Dim outputRule As String
            Try
                Dim xmlDoc As New XmlDocument()
                ' CA3075 for not setting secure Xml resolver
                Dim stringReader As New StringReader(inputRule)
                Dim textReader As New XmlTextReader(stringReader) With { _
                    .DtdProcessing = DtdProcessing.Ignore, _
                    .XmlResolver = Nothing _
                }
                Dim settings As New XmlReaderSettings() With { _
                    .ConformanceLevel = ConformanceLevel.Auto, _
                    .IgnoreComments = True, _
                    .DtdProcessing = DtdProcessing.Ignore, _
                    .XmlResolver = Nothing _
                }
                Dim reader As XmlReader = XmlReader.Create(textReader, settings)
                xmlDoc.Load(reader)
                Dim enabledAttribute As XmlAttribute = xmlDoc.CreateAttribute(""enabled"")
                Dim ruleAttrColl As XmlAttributeCollection = xmlDoc.DocumentElement.Attributes
                Dim nameAttribute As XmlAttribute = DirectCast(ruleAttrColl.GetNamedItem(""name""), XmlAttribute)
                ruleAttrColl.Remove(ruleAttrColl(""enabled""))
                ruleAttrColl.InsertAfter(enabledAttribute, nameAttribute)
                outputRule = xmlDoc.OuterXml
            Catch e As XmlException
                Throw New Exception(""Compliance policy parsing error"", e)
            End Try
            Return outputRule
        End Function
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(11, 31)
            );
        }

        [Fact]
        public async Task RealCodeSnippetFromCustomerPost452ShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System;
using System.IO;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {         
        public static string TestMethod(string inputRule)
        {
            string outputRule;
            try
            {
                XmlDocument xmlDoc = new XmlDocument();         // ok
                StringReader stringReader = new StringReader(inputRule);
                XmlTextReader textReader = new XmlTextReader(stringReader)
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null
                };
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Auto,
                    IgnoreComments = true,
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null
                };
                XmlReader reader = XmlReader.Create(textReader, settings);
                xmlDoc.Load(reader);
                XmlAttribute enabledAttribute = xmlDoc.CreateAttribute(""enabled"");
                XmlAttributeCollection ruleAttrColl = xmlDoc.DocumentElement.Attributes;
                XmlAttribute nameAttribute = (XmlAttribute)ruleAttrColl.GetNamedItem(""name"");
                ruleAttrColl.Remove(ruleAttrColl[""enabled""]);
                ruleAttrColl.InsertAfter(enabledAttribute, nameAttribute);
                outputRule = xmlDoc.OuterXml;
            }
            catch (XmlException e)
            {
                throw new Exception(""Compliance policy parsing error"", e);
            }
            return outputRule;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System
Imports System.IO
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public Shared Function TestMethod(inputRule As String) As String
            Dim outputRule As String
            Try
                Dim xmlDoc As New XmlDocument()
                ' ok
                Dim stringReader As New StringReader(inputRule)
                Dim textReader As New XmlTextReader(stringReader) With { _
                    .DtdProcessing = DtdProcessing.Ignore, _
                    .XmlResolver = Nothing _
                }
                Dim settings As New XmlReaderSettings() With { _
                    .ConformanceLevel = ConformanceLevel.Auto, _
                    .IgnoreComments = True, _
                    .DtdProcessing = DtdProcessing.Ignore, _
                    .XmlResolver = Nothing _
                }
                Dim reader As XmlReader = XmlReader.Create(textReader, settings)
                xmlDoc.Load(reader)
                Dim enabledAttribute As XmlAttribute = xmlDoc.CreateAttribute(""enabled"")
                Dim ruleAttrColl As XmlAttributeCollection = xmlDoc.DocumentElement.Attributes
                Dim nameAttribute As XmlAttribute = DirectCast(ruleAttrColl.GetNamedItem(""name""), XmlAttribute)
                ruleAttrColl.Remove(ruleAttrColl(""enabled""))
                ruleAttrColl.InsertAfter(enabledAttribute, nameAttribute)
                outputRule = xmlDoc.OuterXml
            Catch e As XmlException
                Throw New Exception(""Compliance policy parsing error"", e)
            End Try
            Return outputRule
        End Function
    End Class
End Namespace"
            );
        }
    }
}
