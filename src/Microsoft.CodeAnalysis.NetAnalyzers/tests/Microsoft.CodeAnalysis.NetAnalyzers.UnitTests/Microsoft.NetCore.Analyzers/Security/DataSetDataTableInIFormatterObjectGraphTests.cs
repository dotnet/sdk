// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Security.CSharpDataSetDataTableInIFormatterSerializableObjectGraphAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DataSetDataTableInIFormatterObjectGraphTests
    {
        [Fact]
        public async Task BinaryFormatter_Cast_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Method(MemoryStream ms)
        {
            BinaryFormatter bf = new BinaryFormatter();
            BlahClass bc = (BlahClass) bf.Deserialize(ms);
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task NetDataContractSerializer_Cast_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Method(MemoryStream ms)
        {
            NetDataContractSerializer ndcs = new NetDataContractSerializer();
            BlahClass bc = (BlahClass) ndcs.Deserialize(ms);
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task ObjectStateFormatter_Cast_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Web.UI;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Method(MemoryStream ms)
        {
            ObjectStateFormatter osf = new ObjectStateFormatter();
            BlahClass bc = (BlahClass) osf.Deserialize(ms);
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task SoapFormatter_Cast_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Soap;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Method(MemoryStream ms)
        {
            SoapFormatter sf = new SoapFormatter();
            BlahClass bc = (BlahClass) sf.Deserialize(ms);
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task BinaryFormatter_As_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Method(MemoryStream ms)
        {
            BinaryFormatter bf = new BinaryFormatter();
            BlahClass bc = bf.Deserialize(ms) as BlahClass;
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task BinaryFormatter_As_PrivateAutoProperty_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        private DataSet DS { get; }

        public BlahClass Method(MemoryStream ms)
        {
            BinaryFormatter bf = new BinaryFormatter();
            BlahClass bc = bf.Deserialize(ms) as BlahClass;
            return bc;
        }
    }
}",
                GetCSharpResultAt(17, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task BinaryFormatter_Cast_ReferenceLoop_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;

        public BlahClass Blah;

        public BlahClass Method(MemoryStream ms)
        {
            BinaryFormatter bf = new BinaryFormatter();
            BlahClass bc = (BlahClass) bf.Deserialize(ms);
            return bc;
        }
    }
}",
                GetCSharpResultAt(19, 28, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task BinaryFormatter_Cast_ReferenceIndirectLoop_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public FooClass Foo;

        public BlahClass Method(MemoryStream ms)
        {
            BinaryFormatter bf = new BinaryFormatter();
            BlahClass bc = (BlahClass) bf.Deserialize(ms);
            return bc;
        }
    }

    [Serializable]
    public class FooClass
    {
        private DataTable DT;
        private BlahClass Blah;
    }
}",
                GetCSharpResultAt(17, 28, "DataTable", "DataTable FooClass.DT"));
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences
                    .DefaultWithSerialization
                    .AddAssemblies(ImmutableArray.Create("System.Web", "System.Runtime.Serialization.Formatters.Soap")),
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
            => VerifyCS.Diagnostic(DataSetDataTableInIFormatterSerializableObjectGraphAnalyzer.ObjectGraphContainsDangerousTypeDescriptor)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
