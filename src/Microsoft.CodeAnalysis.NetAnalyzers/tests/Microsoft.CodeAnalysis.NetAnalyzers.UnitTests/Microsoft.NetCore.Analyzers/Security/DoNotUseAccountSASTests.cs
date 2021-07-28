// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseAccountSAS,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseAccountSASTests
    {
        protected async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            string microsoftWindowsAzureStorageCSharpSourceCode = @"
using System;

namespace Microsoft.WindowsAzure.Storage
{
    public class CloudStorageAccount
    {
        public string GetSharedAccessSignature (SharedAccessAccountPolicy policy)
        {
            return """";
        }

        public void NormalMethod()
        {
        }
    }

    public sealed class SharedAccessAccountPolicy
    {
    }
}

namespace NormalNamespace
{
    public class CloudStorageAccount
    {
        public string GetSharedAccessSignature (SharedAccessAccountPolicy policy)
        {
            return """";
        }
    }

    public sealed class SharedAccessAccountPolicy
    {
    }
}";
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source, microsoftWindowsAzureStorageCSharpSourceCode }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureOfCloudStorageAccountDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;

class TestClass
{
    public void TestMethod(SharedAccessAccountPolicy policy)
    {
        var cloudStorageAccount = new CloudStorageAccount();
        cloudStorageAccount.GetSharedAccessSignature(policy);
    }
}",
            GetCSharpResultAt(10, 9));
        }

        [Fact]
        public async Task TestNormalMethodOfCloudStorageAccountNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.WindowsAzure.Storage;

class TestClass
{
    public void TestMethod()
    {
        var cloudStorageAccount = new CloudStorageAccount();
        cloudStorageAccount.NormalMethod();
    }
}");
        }

        [Fact]
        public async Task TestGetSharedAccessSignatureOfCloudStorageAccountOfNormalNamespaceNoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using NormalNamespace;

class TestClass
{
    public void TestMethod(SharedAccessAccountPolicy policy)
    {
        var cloudStorageAccount = new CloudStorageAccount();
        cloudStorageAccount.GetSharedAccessSignature(policy);
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
