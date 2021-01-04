// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForDataSetReadXml,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForDataSetReadXml,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseXmlReaderForDataSetReadXmlTests
    {
        [Fact]
        public async Task TestReadXmlWithStreamParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new FileStream(""xmlFilename"", FileMode.Open));
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXml"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Data
Imports System.IO

Class TestClass
    Public Sub TestMethod()
        Dim dataSet As new DataSet
        dataSet.ReadXml(new FileStream(""xmlFilename"", FileMode.Open))
    End Sub
End Class",
            GetBasicResultAt(9, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlWithStreamAndXmlReadModeParametersDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Data;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new FileStream(""xmlFilename"", FileMode.Open), XmlReadMode.Auto);
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlWithStringParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(""Filename"");
    }
}",
            GetCSharpResultAt(9, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlWithStringXmlReadModeParametersDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(""Filename"", XmlReadMode.Auto);
    }
}",
            GetCSharpResultAt(9, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlWithTextReaderParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new StreamReader(""TestFile.txt""));
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlWithTextReaderAndXmlReadModeParametersDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new StreamReader(""TestFile.txt""), XmlReadMode.Auto);
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXml"));
        }

        [Fact]
        public async Task TestReadXmlSchemaWithStreamParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXmlSchema(new FileStream(""xmlFilename"", FileMode.Open));
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXmlSchema"));
        }

        [Fact]
        public async Task TestReadXmlSchemaWithStringParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXmlSchema(""Filename"");
    }
}",
            GetCSharpResultAt(9, 9, "DataSet", "ReadXmlSchema"));
        }

        [Fact]
        public async Task TestReadXmlSchemaWithTextReaderParameterDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXmlSchema(new StreamReader(""TestFile.txt""));
    }
}",
            GetCSharpResultAt(10, 9, "DataSet", "ReadXmlSchema"));
        }

        [Fact]
        public async Task TestReadXmlWithXmlReaderParameterNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");
        }

        [Fact]
        public async Task TestReadXmlWithXmlReaderAndXmlReadModeParametersNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXml(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)), XmlReadMode.Auto);
    }
}");
        }

        [Fact]
        public async Task TestReadXmlSchemaWithXmlReaderParameterNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass
{
    public void TestMethod()
    {
        new DataSet().ReadXmlSchema(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");
        }

        [Fact]
        public async Task TestReadXmlSerializableWithXmlReaderParameterNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass : DataSet
{
    protected override void ReadXmlSerializable(XmlReader xmlReader)
    {
    }

    public void TestMethod()
    {
        ReadXmlSerializable(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Data
Imports System.IO
Imports System.Xml

Class TestClass
    Inherits DataSet
    Protected Overrides Sub ReadXmlSerializable(xmlReader As XmlReader)
    End Sub
        
    Public Sub TestMethod()
        ReadXmlSerializable(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)))
    End Sub
End Class");
        }

        [Fact]
        public async Task TestDerivedFromANormalClassNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass
{
    protected virtual void ReadXmlSerializable(XmlReader xmlReader)
    {
    }
}

class SubTestClass : TestClass
{
    protected override void ReadXmlSerializable(XmlReader xmlReader)
    {
    }

    public void TestMethod()
    {
        ReadXmlSerializable(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");
        }

        [Fact]
        public async Task TestTwoLevelsOfInheritanceAndOverridesNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass : DataSet
{
    protected override void ReadXmlSerializable(XmlReader xmlReader)
    {
    }
}

class SubTestClass : TestClass
{
    protected override void ReadXmlSerializable(XmlReader xmlReader)
    {
    }

    public void TestMethod()
    {
        ReadXmlSerializable(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");
        }

        [Fact]
        public async Task TestNormalClassReadXmlWithXmlReaderParameterNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;

class TestClass
{
    public void ReadXml (XmlReader reader)
    {
    }

    public void TestMethod()
    {
        var testClass = new TestClass();
        testClass.ReadXml(new XmlTextReader(new FileStream(""xmlFilename"", FileMode.Open)));
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
