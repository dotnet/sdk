// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Security.CSharpDataSetDataTableInSerializableObjectGraphAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DataSetDataTableInSerializableObjectGraphTests
    {
        [Fact]
        public async Task JavaScriptSerializer_Deserialize_Generic_Diagnostic()
        {
            await VerifyCSharpJssAsync(@"
using System;
using System.Data;
using System.Web.Script.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(string input)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Deserialize<BlahClass>(input);
        }
    }
}",
                GetCSharpResultAt(15, 20, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task JavaScriptSerializer_Deserialize_Generic_NoDiagnostic()
        {
            await VerifyCSharpJssAsync(@"
using System;
using System.Web.Script.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public object NotADataTable;

        public BlahClass Method(string input)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Deserialize<BlahClass>(input);
        }
    }
}");
        }

        [Fact]
        public async Task JavaScriptSerializer_Deserialize_NonGeneric_Diagnostic()
        {
            await VerifyCSharpJssAsync(@"
using System;
using System.Data;
using System.Web.Script.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(string input)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return (BlahClass) jss.Deserialize(input, typeof(BlahClass));
        }
    }
}",
                GetCSharpResultAt(15, 55, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task JavaScriptSerializer_Deserialize_NonGeneric_OutOfOrderArguments_Diagnostic()
        {
            await VerifyCSharpJssAsync(@"
using System;
using System.Data;
using System.Web.Script.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(string input)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return (BlahClass) jss.Deserialize(targetType: typeof(BlahClass), input: input);
        }
    }
}",
                GetCSharpResultAt(15, 60, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task JavaScriptSerializer_DeserializeObject_As_Diagnostic()
        {
            await VerifyCSharpJssAsync(@"
using System;
using System.Data;
using System.Web.Script.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(string input)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.DeserializeObject(input) as BlahClass;
        }
    }
}",
                GetCSharpResultAt(15, 20, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task DataContract_Type_Diagnostic()
        {
            await VerifyCSharpAsync(@"
using System;
using System.Data;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(XmlReader reader)
        {
            DataContractSerializer dcs = new DataContractSerializer(typeof(BlahClass));
            return (BlahClass) dcs.ReadObject(reader);
        }
    }
}",
                GetCSharpResultAt(15, 69, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task DataContract_Type_Types_Diagnostic()
        {
            await VerifyCSharpAsync(@"
using System;
using System.Data;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(XmlReader reader)
        {
            DataContractSerializer dcs = new DataContractSerializer(typeof(BlahClass), new[] { typeof(BlahClass) });
            return (BlahClass) dcs.ReadObject(reader);
        }
    }
}",
                GetCSharpResultAt(15, 69, "DataTable", "DataTable BlahClass.DT"),
                GetCSharpResultAt(15, 96, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task XmlSerializer_Constructor_Diagnostic()
        {
            await VerifyCSharpAsync(@"
using System;
using System.Data;
using System.Xml;
using System.Xml.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(XmlReader reader)
        {
            XmlSerializer xs = new XmlSerializer(typeof(BlahClass));
            return (BlahClass) xs.Deserialize(reader);
        }
    }
}",
                GetCSharpResultAt(15, 50, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task XmlSerializer_FromType_Diagnostic()
        {
            await VerifyCSharpAsync(@"
using System;
using System.Data;
using System.Xml;
using System.Xml.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(XmlReader reader)
        {
            XmlSerializer[] xs = XmlSerializer.FromTypes(new[] { typeof(BlahClass) });
            return (BlahClass) xs[0].Deserialize(reader);
        }
    }
}",
                GetCSharpResultAt(15, 66, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task Newtonsoft_JsonSerializer_Deserialize_Casted_Diagnostic()
        {
            await VerifyCSharpNewtonsoftAsync(@"
using System;
using System.Data;
using Newtonsoft.Json;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public BlahClass Method(JsonReader reader)
        {
            JsonSerializer js = new JsonSerializer();
            return (BlahClass) js.Deserialize(reader);
        }
    }
}",
                GetCSharpResultAt(15, 20, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task Newtonsoft_JsonSerializer_Deserialize_TypeSpecified_Diagnostic()
        {
            await VerifyCSharpNewtonsoftAsync(@"
using System;
using System.Data;
using Newtonsoft.Json;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public object Method(JsonReader reader)
        {
            JsonSerializer js = new JsonSerializer();
            return js.Deserialize(reader, typeof(BlahClass));
        }
    }
}",
                GetCSharpResultAt(15, 43, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task Newtonsoft_JsonSerializer_Deserialize_TypeSpecified_OutOfOrderArguments_Diagnostic()
        {
            await VerifyCSharpNewtonsoftAsync(@"
using System;
using System.Data;
using Newtonsoft.Json;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        public object Method(JsonReader reader)
        {
            JsonSerializer js = new JsonSerializer();
            return js.Deserialize(objectType: typeof(BlahClass), reader: reader);
        }
    }
}",
                GetCSharpResultAt(15, 47, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task Newtonsoft_JsonSerializer_Deserialize_Casted_JsonIgnore_NoDiagnostic()
        {
            await VerifyCSharpNewtonsoftAsync(@"
using System;
using System.Data;
using Newtonsoft.Json;

namespace Blah
{
    public class BlahClass
    {
        [JsonIgnore]
        public DataTable DT;

        public BlahClass Method(JsonReader reader)
        {
            JsonSerializer js = new JsonSerializer();
            return (BlahClass) js.Deserialize(reader);
        }
    }
}");
        }

        [Fact]
        public async Task Newtonsoft_JsonConvert_DeserializeObject_Generic_Diagnostic()
        {
            await VerifyCSharpNewtonsoftAsync(@"
using System;
using System.Data;
using Newtonsoft.Json;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT { get; set; }

        public BlahClass Method(string s)
        {
            return JsonConvert.DeserializeObject<BlahClass>(s);
        }
    }
}",
                GetCSharpResultAt(14, 20, "DataTable", "DataTable BlahClass.DT"));
        }

        private static async Task VerifyCSharpAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences
                    .DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        /// <summary>
        /// Tests code using JavaScriptSerializer.
        /// </summary>
        /// <param name="source">Source code to test.</param>
        /// <param name="expected">Expected diagnostics.</param>
        /// <returns>Task of the test run.</returns>
        private static async Task VerifyCSharpJssAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences
                    .DefaultWithSystemWeb
                    .AddAssemblies(ImmutableArray.Create("System.Data")),
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        /// <summary>
        /// Tests code using Newtonsoft Json.NET.
        /// </summary>
        /// <param name="source">Source code to test.</param>
        /// <param name="expected">Expected diagnostics.</param>
        /// <returns>Task of the test run.</returns>
        private static async Task VerifyCSharpNewtonsoftAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences
                    .DefaultWithNewtonsoftJson12
                    .AddAssemblies(ImmutableArray.Create("System.Data")),
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
            => VerifyCS.Diagnostic(DataSetDataTableInSerializableObjectGraphAnalyzer.ObjectGraphContainsDangerousTypeDescriptor)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
