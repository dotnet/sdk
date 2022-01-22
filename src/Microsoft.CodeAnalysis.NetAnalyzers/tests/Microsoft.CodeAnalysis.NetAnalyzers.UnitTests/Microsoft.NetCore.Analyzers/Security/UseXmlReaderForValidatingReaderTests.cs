// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForValidatingReader,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseXmlReaderForValidatingReaderTests
    {
        [Fact]
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

        [Fact]
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

        [Fact]
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
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
