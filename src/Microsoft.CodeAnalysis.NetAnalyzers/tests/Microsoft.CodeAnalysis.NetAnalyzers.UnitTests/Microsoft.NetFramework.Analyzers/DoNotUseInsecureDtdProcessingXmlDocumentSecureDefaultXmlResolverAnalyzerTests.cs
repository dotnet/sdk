// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlDocumentWithNoSecureResolver).WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs

        [Fact]
        public async Task XmlDocumentDefaultResolversInXmlReaderSettingsPre452ShouldGenerateDiagnosticAsync()
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
        public async Task XmlDocumentDefaultResolversInXmlReaderSettingsPre452ShouldNotGenerateDiagnosticAsync()
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
