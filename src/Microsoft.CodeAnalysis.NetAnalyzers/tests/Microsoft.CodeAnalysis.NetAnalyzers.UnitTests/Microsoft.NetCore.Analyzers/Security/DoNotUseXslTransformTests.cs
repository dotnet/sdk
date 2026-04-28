// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseXslTransform,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseXslTransformTests
    {
        [Fact]
        public async Task TestConstructXslTransformDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

class TestClass
{
    public void TestMethod()
    {
        new XslTransform();
    }
}",
            GetCSharpResultAt(9, 9));
        }

        [Fact]
        public async Task TestConstructNormalClassNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

class TestClass
{
    public void TestMethod()
    {
        new TestClass();
    }
}");
        }

        [Fact]
        public async Task TestInvokeMethodOfXslTransformNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

class TestClass
{
    public void TestMethod(XslTransform xslTransform)
    {
        xslTransform.Load(""url"");
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}
