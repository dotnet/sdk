// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotAddSchemaByURL,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotAddSchemaByURL,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotAddSchemaByURLTests
    {
        [Fact]
        public async Task TestAddWithStringStringParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Schema;

class TestClass
{
    public void TestMethod()
    {
        XmlSchemaCollection xsc = new XmlSchemaCollection();
        xsc.Add(""urn: bookstore - schema"", ""books.xsd"");
    }
}",
            GetCSharpResultAt(10, 9));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Schema

class TestClass
    public Sub TestMethod
        Dim xsc As New XmlSchemaCollection
        xsc.Add(""urn: bookstore - schema"", ""books.xsd"")
    End Sub
End Class",
            GetBasicResultAt(8, 9));
        }

        [Fact]
        public async Task TestAddWithNullStringParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Schema;

class TestClass
{
    public void TestMethod()
    {
        XmlSchemaCollection xsc = new XmlSchemaCollection();
        xsc.Add(null, ""books.xsd"");
    }
}",
            GetCSharpResultAt(10, 9));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Schema

class TestClass
    public Sub TestMethod
        Dim xsc As New XmlSchemaCollection
        xsc.Add(Nothing, ""books.xsd"")
    End Sub
End Class",
            GetBasicResultAt(8, 9));
        }

        [Fact]
        public async Task TestAddWithXmlSchemaCollectionParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Schema;

class TestClass
{
    public void TestMethod()
    {
        XmlSchemaCollection xsc = new XmlSchemaCollection();
        xsc.Add(xsc);
    }
}");
        }

        [Fact]
        public async Task TestAddWithXmlSchemaParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Schema;

class TestClass
{
    public void TestMethod()
    {
        XmlSchemaCollection xsc = new XmlSchemaCollection();
        xsc.Add(new XmlSchema());
    }
}");
        }

        [Fact]
        public async Task TestNormalAddMethodNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Schema;

class TestClass
{
    public static void Add (string ns, string uri)
    {
    }

    public void TestMethod()
    {
        TestClass.Add(""urn: bookstore - schema"", ""books.xsd"");
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
