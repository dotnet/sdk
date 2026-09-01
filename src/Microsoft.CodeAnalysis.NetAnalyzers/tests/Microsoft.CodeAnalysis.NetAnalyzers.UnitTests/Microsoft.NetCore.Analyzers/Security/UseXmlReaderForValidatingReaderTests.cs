// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForValidatingReader,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
    public class UseXmlReaderForValidatingReaderTests
    {
        [TestMethod]
        public async Task TestStreamAndXmlNodeTypeAndXmlParseContextParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml;

class TestClass
{
    public void TestMethod(Stream xmlFragment, XmlNodeType fragType, XmlParserContext context)
    {
        var obj = new XmlValidatingReader(xmlFragment, fragType, context);
    }
}",
            GetCSharpResultAt(10, 19, "XmlValidatingReader", "XmlValidatingReader"));
        }

        [TestMethod]
        public async Task TestStringAndXmlNodeTypeAndXmlParseContextParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

class TestClass
{
    public void TestMethod(string xmlFragment, XmlNodeType fragType, XmlParserContext context)
    {
        var obj = new XmlValidatingReader(xmlFragment, fragType, context);
    }
}",
            GetCSharpResultAt(9, 19, "XmlValidatingReader", "XmlValidatingReader"));
        }

        [TestMethod]
        public async Task TestXmlReaderParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

class TestClass
{
    public void TestMethod(XmlReader xmlReader)
    {
        var obj = new XmlValidatingReader(xmlReader);
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
