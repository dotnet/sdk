// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Security.CSharpDataSetDataTableInWebSerializableObjectGraphAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DataSetDataTableInWebSerializableObjectGraphTests
    {
        [Fact]
        public async Task WebServiceDirectlyReferences()
        {
            await VerifyWebServicesCSharpAsync(@"
using System;
using System.Data;
using System.Web.Services;

[WebService(Namespace = ""http://contoso.example.com/"")]
public class MyService : WebService
{
    [WebMethod]
    public string MyWebMethod(DataTable dataTable)
    {
        return null;
    }
}
",
                GetCSharpResultAt(10, 31, "DataTable", "DataTable"));
        }

        [Fact]
        public async Task WebServiceIndirectlyReferences()
        {
            await VerifyWebServicesCSharpAsync(@"
using System;
using System.Data;
using System.Web.Services;

[WebService(Namespace = ""http://contoso.example.com/"")]
public class MyService : WebService
{
    [WebMethod]
    public string MyWebMethod(MyType boo)
    {
        return null;
    }
}

public class MyType
{
    public DataSet DS { get; set; }
}
",
                GetCSharpResultAt(10, 31, "DataSet", "DataSet MyType.DS"));
        }

        [Fact]
        public async Task OperationContract()
        {
            await VerifyServiceModelCSharpAsync(@"
using System;
using System.Data;
using System.ServiceModel;

[ServiceContract(Namespace = ""http://contoso.example.com/"")]
public interface IMyContract
{
    [OperationContract]
    string MyMethod(DataTable dataTable);
    [OperationContract]
    string MyOtherMethod(MyClass data);
}

public class MyClass
{
    // Property of type DataSet, automatically serialized and
    // deserialized as part of the overall MyClass payload.
    public DataSet MyDataSet { get; set; }
}
",
                GetCSharpResultAt(10, 21, "DataTable", "DataTable"),
                GetCSharpResultAt(12, 26, "DataSet", "DataSet MyClass.MyDataSet"));
        }

        private static async Task VerifyServiceModelCSharpAsync(string source, params DiagnosticResult[] expected)
        {
#pragma warning disable CA5386 // Avoid hardcoding SecurityProtocolType value
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
#pragma warning restore CA5386 // Avoid hardcoding SecurityProtocolType value
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default.AddAssemblies(
                    ImmutableArray.Create("System.Data", "System.ServiceModel")),
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyWebServicesCSharpAsync(string source, params DiagnosticResult[] expected)
        {
#pragma warning disable CA5386 // Avoid hardcoding SecurityProtocolType value
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
#pragma warning restore CA5386 // Avoid hardcoding SecurityProtocolType value
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default.AddAssemblies(
                    ImmutableArray.Create("System.Data", "System.Web.Services")),
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
            => VerifyCS.Diagnostic(DataSetDataTableInWebSerializableObjectGraphAnalyzer.ObjectGraphContainsDangerousTypeDescriptor)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
