// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerObjectStateFormatter,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerObjectStateFormatter,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseInsecureDeserializerObjectStateFormatterTests
    {
        [Fact]
        public async Task DocSample1_CSharp_Violation_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

public class ExampleClass
{
    public object MyDeserialize(byte[] bytes)
    {
        ObjectStateFormatter formatter = new ObjectStateFormatter();
        return formatter.Deserialize(new MemoryStream(bytes));
    }
}",
                GetCSharpResultAt(10, 16, "object ObjectStateFormatter.Deserialize(Stream inputStream)"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation_DiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System.IO
Imports System.Web.UI

Public Class ExampleClass
    Public Function MyDeserialize(bytes As Byte()) As Object
        Dim formatter As ObjectStateFormatter = New ObjectStateFormatter()
        Return formatter.Deserialize(New MemoryStream(bytes))
    End Function
End Class",
                GetBasicResultAt(8, 16, "Function ObjectStateFormatter.Deserialize(inputStream As Stream) As Object"));
        }

        [Fact]
        public async Task DeserializeStream_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            ObjectStateFormatter formatter = new ObjectStateFormatter();
            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(12, 20, "object ObjectStateFormatter.Deserialize(Stream inputStream)"));
        }

        [Fact]
        public async Task DeserializeString_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

namespace Blah
{
    public class Program
    {
        public object Deserialize(string input)
        {
            ObjectStateFormatter formatter = new ObjectStateFormatter();
            return formatter.Deserialize(input);
        }
    }
}",
            GetCSharpResultAt(12, 20, "object ObjectStateFormatter.Deserialize(string inputString)"));
        }

        [Fact]
        public async Task Deserialize_Reference_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

namespace Blah
{
    public class Program
    {
        public delegate object Des(string s);
        public Des GetDeserializer()
        {
            ObjectStateFormatter formatter = new ObjectStateFormatter();
            return formatter.Deserialize;
        }
    }
}",
                GetCSharpResultAt(13, 20, "object ObjectStateFormatter.Deserialize(string inputString)"));
        }

        [Fact]
        public async Task Serialize_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

namespace Blah
{
    public class Program
    {
        public byte[] Serialize(object o)
        {
            ObjectStateFormatter formatter = new ObjectStateFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, o);
            return stream.ToArray();
        }
    }
}");
        }

        [Fact]
        public async Task Serialize_Reference_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.UI;

namespace Blah
{
    public class Program
    {
        public delegate void Ser(Stream s, object o);
        public Ser GetSerializer()
        {
            ObjectStateFormatter formatter = new ObjectStateFormatter();
            return formatter.Serialize;
        }
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
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
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
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
