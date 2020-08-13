// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseSharedAccessProtocolHttpsOnly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseSharedAccessProtocolHttpsOnlyTests
    {
        protected async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAzureStorage,
                TestState =
                {
                    Sources = { source }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        protected async Task VerifyCSharpWithDependenciesAsync(string source, string editorConfigText, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAzureStorage,
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles = { (".editorconfig", editorConfigText) }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy, SharedAccessFileHeaders headers, string groupPolicyIdentifier, IPAddressOrRange ipAddressOrRange)
    {
        var cloudFile = new CloudFile(null);
        var protocols = SharedAccessProtocol.HttpsOrHttp;
        cloudFile.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, protocols, ipAddressOrRange); 
    }
}",
            GetCSharpResultAt(12, 9));
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithoutProtocolsParameterNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy, string groupPolicyIdentifier)
    {
        var cloudFile = new CloudFile(null);
        cloudFile.GetSharedAccessSignature(policy, groupPolicyIdentifier);
    }
}");
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy, SharedAccessFileHeaders headers, string groupPolicyIdentifier, IPAddressOrRange ipAddressOrRange)
    {
        var cloudFile = new CloudFile(null);
        var protocols = SharedAccessProtocol.HttpsOnly;
        cloudFile.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, protocols, ipAddressOrRange); 
    }
}");
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterOfTypeIntNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy, SharedAccessFileHeaders headers, string groupPolicyIdentifier, IPAddressOrRange ipAddressOrRange)
    {
        var cloudFile = new CloudFile(null);
        cloudFile.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, {|CS1503:1|}, ipAddressOrRange); 
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
        public async Task TestWithoutMicrosoftWindowsAzureNamespaceNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestMicrosoftWindowsAzureNamespaceNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using Microsoft.WindowsAzure;

namespace Microsoft.WindowsAzure
{
    class A
    {
    }
}

class TestClass
{
    public void TestMethod()
    {
        var a = new A();
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality." + UseSharedAccessProtocolHttpsOnly.DiagnosticId + ".excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new DiagnosticResult[]
                {
                    GetCSharpResultAt(12, 9)
                };
            }

            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public void TestMethod(SharedAccessFilePolicy policy, SharedAccessFileHeaders headers, string groupPolicyIdentifier, IPAddressOrRange ipAddressOrRange)
    {
        var cloudFile = new CloudFile(null);
        var protocols = SharedAccessProtocol.HttpsOrHttp;
        cloudFile.GetSharedAccessSignature(policy, headers, groupPolicyIdentifier, protocols, ipAddressOrRange); 
    }
}", editorConfigText, expected);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
    }
}
