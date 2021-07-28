// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerNetDataContractSerializerMethods,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerNetDataContractSerializerMethods,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseInsecureDeserializerNetDataContractSerializerMethodsTests
    {
        [Fact]
        public async Task DocSample1_CSharp_Violation_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

public class ExampleClass
{
    public object MyDeserialize(byte[] bytes)
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();
        return serializer.Deserialize(new MemoryStream(bytes));
    }
}",
                GetCSharpResultAt(10, 16, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation_Diagnostic()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System.IO
Imports System.Runtime.Serialization

Public Class ExampleClass
    Public Function MyDeserialize(bytes As Byte()) As Object
        Dim serializer As NetDataContractSerializer = New NetDataContractSerializer()
        Return serializer.Deserialize(New MemoryStream(bytes))
    End Function
End Class",
                GetBasicResultAt(8, 16, "Function NetDataContractSerializer.Deserialize(stream As Stream) As Object"));
        }

        [Fact]
        public async Task Deserialize_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.Deserialize(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(12, 20, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task Deserialize_Reference_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public delegate object Des(Stream s);
        public Des GetDeserializer()
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.Deserialize;
        }
    }
}",
                GetCSharpResultAt(13, 20, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_Stream_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(12, 20, "object XmlObjectSerializer.ReadObject(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_Stream_Reference_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public delegate object Des(Stream s);
        public Des D()
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject;
        }
    }
}",
                GetCSharpResultAt(13, 20, "object XmlObjectSerializer.ReadObject(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_XmlReader_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        public object D(XmlReader xmlReader)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject(xmlReader);
        }
    }
}",
                GetCSharpResultAt(13, 20, "object NetDataContractSerializer.ReadObject(XmlReader reader)"));
        }

        [Fact]
        public async Task ReadObject_XmlReader_Reference_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        public delegate object Des(XmlReader r);
        public Des D()
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject;
        }
    }
}",
                GetCSharpResultAt(14, 20, "object NetDataContractSerializer.ReadObject(XmlReader reader)"));
        }

        [Fact]
        public async Task Serialize_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public byte[] S(object o)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, o);
            return ms.ToArray();
        }
    }
}");
        }

        [Fact]
        public async Task Serialize_Reference_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public delegate void Ser(Stream s, object o);
        public Ser GetSerializer()
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.Serialize;
        }
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyBasicAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
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
