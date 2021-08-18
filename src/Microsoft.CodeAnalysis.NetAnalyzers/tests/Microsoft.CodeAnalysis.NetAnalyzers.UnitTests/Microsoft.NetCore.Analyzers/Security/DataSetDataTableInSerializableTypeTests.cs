// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Security.CSharpDataSetDataTableInSerializableTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DataSetDataTableInSerializableTypeTests
    {
        [Fact]
        public async Task Serializable_Field_DiagnosticAsync()
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
        public async Task DataContract_Field_DiagnosticAsync()
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
        public async Task IgnoreDataMemberOnDataTable_DiagnosticAsync()
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
        public async Task IgnoreDataMemberOnNotDataTable_DiagnosticAsync()
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
        public async Task DataContract_PrivateProperty_DiagnosticAsync()
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
        public async Task DataContract_KnownType_DataTable_DiagnosticAsync()
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
        public async Task DataContract_InheritedKnownType_DataTable_DiagnosticAsync()
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
        public async Task Serializable_FieldDerivedClass_DiagnosticAsync()
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
        public async Task Serializable_PrivateField_DiagnosticAsync()
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
        public async Task Serializable_Property_DiagnosticAsync()
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
        public async Task Serializable_PropertyDerived_DiagnosticAsync()
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
        public async Task Serializable_PropertyList_DiagnosticAsync()
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
        public async Task Serializable_PropertyListListList_DiagnosticAsync()
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
        public async Task Serializable_PropertyArray_DiagnosticAsync()
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
        public async Task Serializable_Property2DArray_DiagnosticAsync()
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
        public async Task Serializable_PropertyArrayArray_DiagnosticAsync()
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
        public async Task Serializable_PropertyNoExplicitSetter_DiagnosticAsync()
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
        public async Task XmlElement_Property_DiagnosticAsync()
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
        public async Task XmlIgnore_Property_DiagnosticAsync()
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

        [Fact]
        public async Task GeneratedCode_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Xml.Serialization;

namespace Blah
{
    [global::System.CodeDom.Compiler.GeneratedCode(""System.Data.Design.TypedDataSetGenerator"", ""2.0.0.0"")]
    [global::System.Serializable()]
    [global::System.ComponentModel.DesignerCategoryAttribute(""code"")]
    [global::System.ComponentModel.ToolboxItem(true)]
    [global::System.Xml.Serialization.XmlSchemaProviderAttribute(""GetTypedDataSetSchema"")]
    [global::System.Xml.Serialization.XmlRootAttribute(""Package"")]
    [global::System.ComponentModel.Design.HelpKeywordAttribute(""vs.data.DataSet"")]
    public class BlahClass : global::System.Data.DataSet {
        private DataTable table;
    }
}",
                GetAutogeneratedIFormatterCSharpResultAt(7, 5, "DataSet", "BlahClass"),
                GetAutogeneratedIFormatterCSharpResultAt(15, 27, "DataTable", "DataTable BlahClass.table"));
        }

        [Fact]
        public async Task OtherGeneratedCode_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Data;
using System.Xml.Serialization;

namespace Blah
{
    //[global::System.Serializable()]
    [global::System.ComponentModel.DesignerCategoryAttribute(""code"")]
    [global::System.ComponentModel.ToolboxItem(true)]
    [global::System.Xml.Serialization.XmlSchemaProviderAttribute(""GetTypedDataSetSchema"")]
    [global::System.Xml.Serialization.XmlRootAttribute(""Package"")]
    [global::System.ComponentModel.Design.HelpKeywordAttribute(""vs.data.DataSet"")]
    public class BlahClass : global::System.Data.DataSet {
        private DataTable table;
    }
}",
                GetNonIFormatterCSharpResultAt(8, 5, "DataSet", "BlahClass"));
        }

#if !NETCOREAPP
        [Fact]
        public async Task MessageContract_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                @"
namespace Blah
{
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute(""System.ServiceModel"", ""4.0.0.0"")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(WrapperName = ""GetSomethingResponse"", WrapperNamespace = ""http://tempuri.org/"", IsWrapped = true)]
    public partial class GetSomethingResponse
    {

        [System.ServiceModel.MessageBodyMemberAttribute(Namespace = ""http://tempuri.org/"", Order = 0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public System.Data.DataSet GetSomethingResult;

        public GetSomethingResponse()
        {
        }

        public GetSomethingResponse(System.Data.DataSet GetSomethingResult)
        {
            this.GetSomethingResult = GetSomethingResult;
        }
    }
}",
                GetNonIFormatterCSharpResultAt(13, 36, "DataSet", "DataSet GetSomethingResponse.GetSomethingResult"));
        }
#endif

        [Fact]
        public async Task TypedTableBase_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
namespace Blah
{
    /// <summary>
    ///Represents the strongly named DataTable class.
    ///</summary>
    [global::System.Serializable()]
    [global::System.Xml.Serialization.XmlSchemaProviderAttribute(""GetTypedTableSchema"")]
    public partial class SomethingDataTable : global::System.Data.TypedTableBase<SomethingRow>
    {
        private global::System.Data.DataColumn columnId;
    }

    /// <summary>
    ///Represents strongly named DataRow class.
    ///</summary>
    public partial class SomethingRow : global::System.Data.DataRow {

        private SomethingDataTable tableSomething;

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Data.Design.TypedDataSetGenerator"", ""4.0.0.0"")]
        internal SomethingRow(global::System.Data.DataRowBuilder rb) :
            base(rb)
        {
        }
    }
}",
                GetAutogeneratedIFormatterCSharpResultAt(7, 5, "DataTable", "SomethingDataTable"));
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
#if !NETCOREAPP
                    AdditionalReferences = { AdditionalMetadataReferences.SystemServiceModel },
#endif
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetNonIFormatterCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DataSetDataTableInSerializableTypeAnalyzer.SerializableContainsDangerousType)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetIFormatterCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DataSetDataTableInSerializableTypeAnalyzer.RceSerializableContainsDangerousType)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetAutogeneratedIFormatterCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DataSetDataTableInSerializableTypeAnalyzer.RceAutogeneratedSerializableContainsDangerousType)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
