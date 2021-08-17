// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingAnalyzerTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlDocumentWithNoSecureResolver).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task XmlDocumentDefaultResolversInXmlReaderSettingsPre452ShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System;
using System.Reflection;
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            XmlReader reader = XmlReader.Create(path, settings);
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
        }
    }
}
",
            GetCSharpResultAt(14, 31)
            );

        }

        [Fact]
        public async Task XmlDocumentDefaultResolversInXmlReaderSettingsPre452ShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System;
using System.Reflection;
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            XmlReader reader = XmlReader.Create(path, settings);
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
        }
    }
}
"
            );
        }
    }
}
