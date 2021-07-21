// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseContainerLevelAccessPolicy,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseContainerLevelAccessPolicyTests
    {
        private async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAzureStorage,
                TestState =
                {
                    Sources = { source  }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private async Task VerifyCSharpWithDependenciesAsync(string source, string editorConfigText, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAzureStorage,
                TestState =
                {
                    Sources = { source },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task TestGroupPolicyIdentifierOfBlobNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public void TestMethod(SharedAccessBlobPolicy policy, SharedAccessBlobHeaders headers, Nullable<SharedAccessProtocol> protocols, IPAddressOrRange ipAddressOrRange)
    {
        var cloudAppendBlob = new CloudAppendBlob(null);
        string groupPolicyIdentifier = null;
        cloudAppendBlob.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, protocols, ipAddressOrRange);
    }
}",
            GetCSharpResultAt(12, 9));
        }

        [Fact]
        public async Task TestPropertyInitializerGroupPolicyIdentifierOfBlobNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public string SAS { get; } = new CloudAppendBlob(null).GetSharedAccessSignature(null, null, null, null, null);
}",
            GetCSharpResultAt(8, 34));
        }

        [Fact]
        public async Task TestFieldInitializerGroupPolicyIdentifierOfBlobNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public string SAS = new CloudAppendBlob(null).GetSharedAccessSignature(null, null, null, null, null);
}",
            GetCSharpResultAt(8, 25));
        }

        [Fact]
        public async Task TestPropertyInitializerGroupPolicyIdentifierOfBlobNamespaceNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public string SAS { get; } = new CloudAppendBlob(null).GetSharedAccessSignature(null, null, ""foo"", null, null);
}");
        }

        [Fact]
        public async Task TestFieldInitializerGroupPolicyIdentifierOfBlobNamespaceNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public string SAS = new CloudAppendBlob(null).GetSharedAccessSignature(null, null, ""foo"", null, null);
}");
        }

        [Fact]
        public async Task TestAccessPolicyIdentifierOfTableNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Table;

class TestClass
{
    public void TestMethod(SharedAccessTablePolicy policy, string startPartitionKey, string startRowKey, string endPartitionKey, string endRowKey)
    {
        var cloudTable = new CloudTable(null);
        string accessPolicyIdentifier = null;
        cloudTable.GetSharedAccessSignature(policy, accessPolicyIdentifier, startPartitionKey, startRowKey, endPartitionKey, endRowKey);
    }
}",
            GetCSharpResultAt(11, 9));
        }

        [Fact]
        public async Task TestGroupPolicyIdentifierOfFileNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy)
    {
        var cloudFile = new CloudFile(null);
        string groupPolicyIdentifier = null;
        cloudFile.GetSharedAccessSignature(policy, groupPolicyIdentifier);
    }
}",
            GetCSharpResultAt(11, 9));
        }

        [Fact]
        public async Task TestAccessPolicyIdentifierOfQueueNamespaceIsNullDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Queue;

class TestClass
{
    public int a; 
    public void TestMethod(SharedAccessQueuePolicy policy)
    {
        var cloudQueue = new CloudQueue(null);
        string accessPolicyIdentifier = null;
        cloudQueue.GetSharedAccessSignature(policy, accessPolicyIdentifier);
    }
}",
            GetCSharpResultAt(12, 9));
        }

        [Fact]
        public async Task TestWithoutGroupPolicyIdentifierParameterOfBlobNamespaceDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public void TestMethod(SharedAccessBlobPolicy policy)
    {
        var cloudAppendBlob = new CloudAppendBlob(null);
        cloudAppendBlob.GetSharedAccessSignature(policy);
    }
}",
            GetCSharpResultAt(11, 9));
        }

        [Fact]
        public async Task TestWithoutAccessPolicyIdentifierParameterOfTableNamespaceDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Table;

class TestClass
{
    public void TestMethod(SharedAccessTablePolicy policy)
    {
        var cloudTable = new CloudTable(null);
        cloudTable.GetSharedAccessSignature(policy);
    }
}",
            GetCSharpResultAt(10, 9));
        }

        [Fact]
        public async Task TestWithoutGroupPolicyIdentifierParameterOfFileNamespaceDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy)
    {
        var cloudFile = new CloudFile(null);
        cloudFile.GetSharedAccessSignature(policy);
    }
}",
            GetCSharpResultAt(10, 9));
        }

        [Fact]
        public async Task TestWithoutAccessPolicyIdentifierParameterOfQueueNamespaceDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Queue;

class TestClass
{
    public int a; 
    public void TestMethod(SharedAccessQueuePolicy policy)
    {
        var cloudQueue = new CloudQueue(null);
        cloudQueue.GetSharedAccessSignature(policy);
    }
}",
            GetCSharpResultAt(11, 9));
        }

        [Fact]
        public async Task TestGroupPolicyIdentifierOfBlobNamespaceIsNotNullNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

class TestClass
{
    public void TestMethod(SharedAccessBlobPolicy policy, SharedAccessBlobHeaders headers, Nullable<SharedAccessProtocol> protocols, IPAddressOrRange ipAddressOrRange)
    {
        var cloudAppendBlob = new CloudAppendBlob(null);
        string groupPolicyIdentifier = ""123"";
        cloudAppendBlob.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, protocols, ipAddressOrRange);
    }
}");
        }

        [Fact]
        public async Task TestGroupPolicyIdentifierOfFileNamespaceIsNotNullNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy)
    {
        var cloudFile = new CloudFile(null);
        string groupPolicyIdentifier = ""123"";
        cloudFile.GetSharedAccessSignature(policy, groupPolicyIdentifier);
    }
}");
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureOfANormalTypeNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;

class TestClass
{
    public string GetSharedAccessSignature (SharedAccessAccountPolicy policy)
    {
        return """";
    }

    public void TestMethod(SharedAccessAccountPolicy policy)
    {
        GetSharedAccessSignature(policy);
    }
}");
        }

        [Fact]
        public async Task TestAccessPolicyIdentifierOfQueueNamespaceIsNotNullNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Queue;

class TestClass
{
    public void TestMethod(SharedAccessQueuePolicy policy)
    {
        var cloudQueue = new CloudQueue(null);
        string groupPolicyIdentifier = ""123"";
        cloudQueue.GetSharedAccessSignature(policy, groupPolicyIdentifier);
    }
}");
        }

        [Fact]
        public async Task TestAccessPolicyIdentifierOfTableNamespaceIsNotNullNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Table;

class TestClass
{
    public void TestMethod(SharedAccessTablePolicy policy, string startPartitionKey, string startRowKey, string endPartitionKey, string endRowKey)
    {
        var cloudTable = new CloudTable(null);
        string accessPolicyIdentifier = ""123"";
        cloudTable.GetSharedAccessSignature(policy, accessPolicyIdentifier, startPartitionKey, startRowKey, endPartitionKey, endRowKey);
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality.CA5377.excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality.CA5377.excluded_symbol_names = TestMet*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new DiagnosticResult[]
                {
                    GetCSharpResultAt(11, 9)
                };
            }

            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.Table;

class TestClass
{
    public void TestMethod(SharedAccessTablePolicy policy, string startPartitionKey, string startRowKey, string endPartitionKey, string endRowKey)
    {
        var cloudTable = new CloudTable(null);
        string accessPolicyIdentifier = null;
        cloudTable.GetSharedAccessSignature(policy, accessPolicyIdentifier, startPartitionKey, startRowKey, endPartitionKey, endRowKey);
    }
}", editorConfigText, expected);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
