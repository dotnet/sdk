// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotInstallRootCert,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PropertySetAnalysis)]
    public class DoNotInstallRootCertTests
    {
        [Fact]
        public async Task TestConstructorWithStoreNameParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.Root; 
        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStoreNameParameterMaybeChangedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.Root; 
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            storeName = StoreName.My;
        }

        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(18, 9, DoNotInstallRootCert.MaybeInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStoreNameParameterUnassignedMaybeChangedWithRootDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(StoreName storeName)
    {
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            storeName = StoreName.Root;
        }

        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(17, 9, DoNotInstallRootCert.MaybeInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStoreNameAndStoreLocationParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.Root; 
        var x509Store = new X509Store(storeName, StoreLocation.CurrentUser);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStringParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = ""Root"";
        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestStringCaseSensitiveDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = ""rooT"";
        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStringAndStoreLocationParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = ""Root"";
        var x509Store = new X509Store(storeName, StoreLocation.CurrentUser);
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStoreNameParameterWithoutTemporaryObjectDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        new X509Store(StoreName.Root).Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(8, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestConstructorWithStringParameterWithoutTemporaryObjectDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        new X509Store(""Root"").Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(8, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestPassX509StoreAsParameterInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.Root; 
        var x509Store = new X509Store(storeName);
        TestMethod2(x509Store); 
    }

    public void TestMethod2(X509Store x509Store)
    {
        x509Store.Add(new X509Certificate2());
    }
}",
            GetCSharpResultAt(15, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestGetX509StoreFromLocalFunctionDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        GetX509Store().Add(new X509Certificate2());

        X509Store GetX509Store() => new X509Store(StoreName.Root);
    }
}",
            GetCSharpResultAt(8, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestReturnX509StoreInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        GetX509Store().Add(new X509Certificate2());
    }

    public X509Store GetX509Store()
    {
        return new X509Store(StoreName.Root);
    }
}",
            GetCSharpResultAt(8, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
        }

        [Fact]
        public async Task TestNotCallAddMethodNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var x509Store = new X509Store(""Root"");
    }
}");
        }

        [Fact]
        public async Task TestInstallCertToOtherStoreNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var x509Store = new X509Store(""My"");
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestInstallCertToNullStoreNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var x509Store = new X509Store(null);
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestCreateAStoreWithoutSettingStoreNameNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var x509Store = new X509Store();
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestConstructorWithStoreNameParameterUnassignedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(StoreName storeName)
    {
        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestConstructorWithStoreNameParameterUnassignedMaybeChangedWithMyNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(StoreName storeName)
    {
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            storeName = StoreName.My;
        }

        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestPassX509StoreAsParameterInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.My; 
        var x509Store = new X509Store(storeName);
        TestMethod2(x509Store); 
    }

    public void TestMethod2(X509Store x509Store)
    {
        x509Store.Add(new X509Certificate2());
    }
}");
        }

        [Fact]
        public async Task TestLambdaNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        GetX509Store().Add(new X509Certificate2());

        X509Store GetX509Store() => new X509Store(StoreName.My);
    }
}");
        }

        [Fact]
        public async Task TestReturnX509StoreInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        GetX509Store().Add(new X509Certificate2());
    }

    public X509Store GetX509Store()
    {
        return new X509Store(StoreName.My);
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [InlineData(@"dotnet_code_quality.CA5380.excluded_symbol_names = TestMethod
                      dotnet_code_quality.CA5381.excluded_symbol_names = TestMethod")]
        [InlineData(@"dotnet_code_quality.CA5380.excluded_symbol_names = TestMet*
                      dotnet_code_quality.CA5381.excluded_symbol_names = TestMet*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        var storeName = StoreName.Root; 
        var x509Store = new X509Store(storeName);
        x509Store.Add(new X509Certificate2());
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetCSharpResultAt(10, 9, DoNotInstallRootCert.DefinitelyInstallRootCertRule));
            }

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
