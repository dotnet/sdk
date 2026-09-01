// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseSharedAccessProtocolHttpsOnly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
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

            await csharpTest.RunAsync(CancellationToken.None);
        }

        protected async Task VerifyCSharpWithDependenciesAsync(string source, string editorConfigText, params DiagnosticResult[] expected)
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

            await csharpTest.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterDiagnosticAsync()
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

        [TestMethod]
        public async Task TestPropertyInitializerGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public string SAS { get; } = new CloudFile(null).GetSharedAccessSignature(null, null, null, SharedAccessProtocol.HttpsOrHttp, null);
}",
            GetCSharpResultAt(8, 34));
        }

        [TestMethod]
        public async Task TestFieldInitializerGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public string SAS = new CloudFile(null).GetSharedAccessSignature(null, null, null, SharedAccessProtocol.HttpsOrHttp, null);
}",
            GetCSharpResultAt(8, 25));
        }

        [TestMethod]
        public async Task TestPropertyInitializerGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public string SAS { get; } = new CloudFile(null).GetSharedAccessSignature(null, null, null, SharedAccessProtocol.HttpsOnly, null);
}");
        }

        [TestMethod]
        public async Task TestFieldInitializerGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

class TestClass
{
    public string SAS = new CloudFile(null).GetSharedAccessSignature(null, null, null, SharedAccessProtocol.HttpsOnly, null);
}");
        }

        [TestMethod]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithoutProtocolsParameterNoDiagnosticAsync()
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

        [TestMethod]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterNoDiagnosticAsync()
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

        [TestMethod]
        public async Task TestGetSharedAccessSignatureNotFromCloudStorageAccountWithProtocolsParameterOfTypeIntNoDiagnosticAsync()
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

        [TestMethod]
        public async Task TestGetSharedAccessSignatureOfANormalTypeNoDiagnosticAsync()
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

        [TestMethod]
        public async Task TestWithoutMicrosoftWindowsAzureNamespaceNoDiagnosticAsync()
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

        [TestMethod]
        public async Task TestMicrosoftWindowsAzureNamespaceNoDiagnosticAsync()
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

        [TestMethod]
        [DataRow("")]
        [DataRow("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [DataRow("dotnet_code_quality.CA5376.excluded_symbol_names = TestMethod")]
        [DataRow("dotnet_code_quality.CA5376.excluded_symbol_names = TestMet*")]
        [DataRow("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
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
#pragma warning disable RS0030 // Do not use banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}
