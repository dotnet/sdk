// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForDeserialize,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseXmlReaderForDeserialize,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseXmlReaderForDeserializeTests
    {
        [Fact]
        public async Task TestDeserializeWithStreamParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(Stream stream)
    {
        new XmlSerializer(typeof(TestClass)).Deserialize(stream);
    }
}",
            GetCSharpResultAt(10, 9, "XmlSerializer", "Deserialize"));
        }

        [Fact]
        public async Task TestDeserializeWithTextReaderParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(TextReader textReader)
    {
        new XmlSerializer(typeof(TestClass)).Deserialize(textReader);
    }
}",
            GetCSharpResultAt(10, 9, "XmlSerializer", "Deserialize"));
        }

        [Fact]
        public async Task TestBaseClassInvokesDeserializeWithXmlSerializationReaderParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.Serialization;

class TestClass : XmlSerializer
{
    protected override object Deserialize(XmlSerializationReader xmlSerializationReader)
    {
        return base.Deserialize(xmlSerializationReader);
    }
}",
            GetCSharpResultAt(10, 16, "XmlSerializer", "Deserialize"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Xml.Serialization

Class TestClass
    Inherits XmlSerializer
    Protected Overrides Function Deserialize(xmlSerializationReader As XmlSerializationReader) As Object
        Deserialize = MyBase.Deserialize(xmlSerializationReader)
    End Function
End Class",
            GetBasicResultAt(9, 23, "XmlSerializer", "Deserialize"));
        }

        [Fact]
        public async Task TesDerivedClassInvokesDeserializeWithXmlSerializationReaderParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.Serialization;

class TestClass : XmlSerializer
{
    protected override object Deserialize(XmlSerializationReader xmlSerializationReader)
    {
        return new TestClass();
    }

    public void TestMethod(XmlSerializationReader xmlSerializationReader)
    {
        Deserialize(xmlSerializationReader);
    }
}",
            GetCSharpResultAt(15, 9, "TestClass", "Deserialize"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Xml.Serialization

Class TestClass
    Inherits XmlSerializer
    Protected Overrides Function Deserialize(xmlSerializationReader As XmlSerializationReader) As Object
        Deserialize = new TestClass()
    End Function

    Public Sub TestMethod(xmlSerializationReader As XmlSerializationReader)
        Deserialize(xmlSerializationReader)
    End Sub
End Class",
            GetBasicResultAt(13, 9, "TestClass", "Deserialize"));
        }

        [Fact]
        public async Task TestWithTwoLevelsOfInheritanceAndOverridesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml.Serialization;

class TestClass : XmlSerializer
{
    protected override object Deserialize(XmlSerializationReader xmlSerializationReader)
    {
        return new TestClass();
    }
}

class SubTestClass : TestClass
{
    protected override object Deserialize(XmlSerializationReader xmlSerializationReader)
    {
        return new TestClass();
    }

    public void TestMethod(XmlSerializationReader xmlSerializationReader)
    {
        Deserialize(xmlSerializationReader);
    }
}",
            GetCSharpResultAt(23, 9, "SubTestClass", "Deserialize"));
        }

        [Fact]
        public async Task TestDeserializeWithXmlReaderParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(XmlReader xmlReader)
    {
        new XmlSerializer(typeof(TestClass)).Deserialize(xmlReader);
    }
}");
        }

        [Fact]
        public async Task TestDeserializeWithXmlReaderAndStringParametersNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(XmlReader xmlReader, string str)
    {
        var xmlSerializer = new XmlSerializer(typeof(TestClass));
        new XmlSerializer(typeof(TestClass)).Deserialize(xmlReader, str);
    }
}");
        }

        [Fact]
        public async Task TestDeserializeWithXmlReaderAndXmlDeserializationEventsParametersNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(XmlReader xmlReader, XmlDeserializationEvents xmlDeserializationEvents)
    {
        new XmlSerializer(typeof(TestClass)).Deserialize(xmlReader, xmlDeserializationEvents);
    }
}");
        }

        [Fact]
        public async Task TestDeserializeWithXmlReaderAndStringAndXmlDeserializationEventsParametersNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    public void TestMethod(XmlReader xmlReader, string str, XmlDeserializationEvents xmlDeserializationEvents)
    {
        new XmlSerializer(typeof(TestClass)).Deserialize(xmlReader, str, xmlDeserializationEvents);
    }
}");
        }

        [Fact]
        public async Task TestDerivedFromANormalClassNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    protected virtual object Deserialize (XmlSerializationReader xmlSerializationReader)
    {
        return new TestClass();
    }
}

class SubTestClass : TestClass
{
    protected override object Deserialize(XmlSerializationReader xmlSerializationReader)
    {
        return new SubTestClass();
    }

    public void TestMethod(XmlSerializationReader xmlSerializationReader)
    {
        Deserialize(xmlSerializationReader);
    }
}");
        }

        [Fact]
        public async Task TestNormalClassReadXmlWithXmlReaderParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

class TestClass
{
    public object Deserialize (XmlSerializationReader xmlSerializationReader)
    {
        return new TestClass();
    }

    public void TestMethod(XmlSerializationReader xmlSerializationReader)
    {
        new TestClass().Deserialize(xmlSerializationReader);
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
