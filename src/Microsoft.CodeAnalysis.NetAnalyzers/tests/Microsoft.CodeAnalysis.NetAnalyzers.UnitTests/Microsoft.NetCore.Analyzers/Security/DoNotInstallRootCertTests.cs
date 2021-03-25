// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        public async Task TestConstructorWithStoreNameParameterDiagnostic()
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
        public async Task TestConstructorWithStoreNameParameterMaybeChangedDiagnostic()
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
        public async Task TestConstructorWithStoreNameParameterUnassignedMaybeChangedWithRootDiagnostic()
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
        public async Task TestConstructorWithStoreNameAndStoreLocationParametersDiagnostic()
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
        public async Task TestConstructorWithStringParameterDiagnostic()
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
        public async Task TestStringCaseSensitiveDiagnostic()
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
        public async Task TestConstructorWithStringAndStoreLocationParametersDiagnostic()
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
        public async Task TestConstructorWithStoreNameParameterWithoutTemporaryObjectDiagnostic()
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
        public async Task TestConstructorWithStringParameterWithoutTemporaryObjectDiagnostic()
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
        public async Task TestPassX509StoreAsParameterInterproceduralDiagnostic()
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
        public async Task TestGetX509StoreFromLocalFunctionDiagnostic()
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
        public async Task TestReturnX509StoreInterproceduralDiagnostic()
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
        public async Task TestNotCallAddMethodNoDiagnostic()
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
        public async Task TestInstallCertToOtherStoreNoDiagnostic()
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
        public async Task TestInstallCertToNullStoreNoDiagnostic()
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
        public async Task TestCreateAStoreWithoutSettingStoreNameNoDiagnostic()
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
        public async Task TestConstructorWithStoreNameParameterUnassignedNoDiagnostic()
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
        public async Task TestConstructorWithStoreNameParameterUnassignedMaybeChangedWithMyNoDiagnostic()
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
        public async Task TestPassX509StoreAsParameterInterproceduralNoDiagnostic()
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
        public async Task TestLambdaNoDiagnostic()
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
        public async Task TestReturnX509StoreInterproceduralNoDiagnostic()
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
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
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
