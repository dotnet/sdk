// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForXPathDocument,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseXmlReaderForXPathDocumentTests
    {
        [Fact]
        public async Task TestStreamParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(Stream stream)
    {
        var obj = new XPathDocument(stream);
    }
}",
            GetCSharpResultAt(10, 19, "XPathDocument", "XPathDocument"));
        }

        [Fact]
        public async Task TestStringParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(string uri)
    {
        var obj = new XPathDocument(uri);
    }
}",
            GetCSharpResultAt(9, 19, "XPathDocument", "XPathDocument"));
        }

        [Fact]
        public async Task TestStringAndXmlSpaceParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(string uri, XmlSpace space)
    {
        var obj = new XPathDocument(uri, space);
    }
}",
            GetCSharpResultAt(10, 19, "XPathDocument", "XPathDocument"));
        }

        [Fact]
        public async Task TestTextReaderParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(TextReader reader)
    {
        var obj = new XPathDocument(reader);
    }
}",
            GetCSharpResultAt(10, 19, "XPathDocument", "XPathDocument"));
        }

        [Fact]
        public async Task TestXmlReaderParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(XmlReader reader)
    {
        var obj = new XPathDocument(reader);
    }
}");
        }

        [Fact]
        public async Task TestXmlReaderAndXmlSpaceParametersNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;
using System.Xml.XPath;

class TestClass
{
    public void TestMethod(XmlReader reader, XmlSpace space)
    {
        var obj = new XPathDocument(reader, space);
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
