// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DataSetDataTableInSerializableTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DataSetDataTableInSerializableTypeTests
    {
        [Fact]
        public async Task Serializable_Field_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS;
    }
}",
                GetIFormatterCSharpResultAt(10, 24, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task DataContract_Field_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    [DataContract]
    public class BlahClass
    {
        [DataMember]
        public DataSet DS;
    }
}",
                GetNonIFormatterCSharpResultAt(11, 24, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task IgnoreDataMemberOnDataTable_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    public class BlahClass
    {
        [IgnoreDataMember]
        public DataTable DT;

        public int I;
    }
}");
        }

        [Fact]
        public async Task IgnoreDataMemberOnNotDataTable_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    public class BlahClass
    {
        public DataTable DT;

        [IgnoreDataMember]
        public int I;
    }
}",
                GetNonIFormatterCSharpResultAt(9, 26, "DataTable", "DataTable BlahClass.DT"));
        }

        [Fact]
        public async Task DataContract_PrivateProperty_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    [DataContract]
    public class BlahClass
    {
        [DataMember]
        private DataSet DS { get; set; }
    }
}",
                GetNonIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task DataContract_KnownType_DataTable_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    [DataContract]
    [KnownType(typeof(DataTable))]
    public class BlahClass
    {
        [DataMember]
        public object DT;
    }
}",
                GetNonIFormatterCSharpResultAt(8, 6, "DataTable", "typeof(System.Data.DataTable)"));
        }

        [Fact]
        public async Task DataContract_InheritedKnownType_DataTable_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Runtime.Serialization;

namespace Blah
{
    [KnownType(typeof(DataTable))]
    public class BlahBase
    {
        public object DT;
    }

    [DataContract]
    public class BlahClass : BlahBase
    {
    }
}",
                GetNonIFormatterCSharpResultAt(7, 6, "DataTable", "typeof(System.Data.DataTable)"));
        }

        [Fact]
        public async Task Serializable_FieldDerivedClass_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    public class MyDataSet : DataSet
    {
    }

    [Serializable]
    public class BlahClass
    {
        public MyDataSet DS;
    }
}",
                GetIFormatterCSharpResultAt(14, 26, "DataSet", "MyDataSet BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PrivateField_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        private DataSet DS;
    }
}",
                GetIFormatterCSharpResultAt(10, 25, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_Property_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyDerived_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    public class MyDataSet : DataSet
    {
    }

    [Serializable]
    public class BlahClass
    {
        public MyDataSet DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(14, 9, "DataSet", "MyDataSet BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyList_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public List<DataSet> DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(11, 9, "DataSet", "List<DataSet> BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyListListList_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public List<List<List<DataSet>>> DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(11, 9, "DataSet", "List<List<List<DataSet>>> BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyArray_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet[] DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet[] BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_Property2DArray_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet[,] DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet[,] BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyArrayArray_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet[][] DS { get; set; }
    }
}",
                GetIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet[][] BlahClass.DS"));
        }

        [Fact]
        public async Task Serializable_PropertyNoExplicitSetter_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Data;

namespace Blah
{
    [Serializable]
    public class BlahClass
    {
        public DataSet DS { get; }
    }
}",
            GetIFormatterCSharpResultAt(10, 9, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task XmlElement_Property_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Xml.Serialization;

namespace Blah
{
    public class BlahClass
    {
        [XmlElement]
        public DataSet DS { get; set; }
    }
}",
            GetNonIFormatterCSharpResultAt(9, 9, "DataSet", "DataSet BlahClass.DS"));
        }

        [Fact]
        public async Task XmlIgnore_Property_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Xml.Serialization;

namespace Blah
{
    [XmlRoot]
    public class BlahClass
    {
        [XmlIgnore]
        public DataSet DS { get; set; }
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
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

        private static DiagnosticResult GetNonIFormatterCSharpResultAt(int line, int column, params string[] arguments)
            => VerifyCS.Diagnostic(DataSetDataTableInSerializableTypeAnalyzer.SerializableContainsDangerousType)
                .WithLocation(line, column)
                .WithArguments(arguments);

        private static DiagnosticResult GetIFormatterCSharpResultAt(int line, int column, params string[] arguments)
    => VerifyCS.Diagnostic(DataSetDataTableInSerializableTypeAnalyzer.RceSerializableContainsDangerousType)
        .WithLocation(line, column)
        .WithArguments(arguments);
    }
}
