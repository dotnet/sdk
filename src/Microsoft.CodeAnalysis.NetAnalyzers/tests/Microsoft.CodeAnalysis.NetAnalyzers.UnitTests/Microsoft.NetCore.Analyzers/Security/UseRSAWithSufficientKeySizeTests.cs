﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseRSAWithSufficientKeySize,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseRSAWithSufficientKeySizeTests
    {
        [Fact]
        public async Task Issue2697Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public RSACryptoServiceProvider TestMethod(string xml)
    {
        var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(xml);
        return rsa;
    }
}");
        }

        [Fact]
        public async Task TestCreateObjectOfRSADerivedClassWithInt32ParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var rsaCng = new RSACng(1024);
    }
}",
            GetCSharpResultAt(8, 22, "RSACng"));
        }

        [Fact]
        public async Task TestConstantDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        const int keySize = 1024;
        var rsaCng = new RSACng(keySize);
    }
}",
            GetCSharpResultAt(9, 22, "RSACng"));
        }

        [Fact]
        public async Task TestCreateWithoutParameterDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var asymmetricAlgorithm = AsymmetricAlgorithm.Create();
    }
}",
            GetCSharpResultAt(8, 35, "RSA"));
        }

        [Fact]
        public async Task TestCreateWithRSAArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var asymmetricAlgorithm = AsymmetricAlgorithm.Create(""RSA"");
    }
}",
            GetCSharpResultAt(8, 35, "RSA"));
        }

        [Fact]
        public async Task TestCreateWithSystemSecurityCryptographyRSAArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var asymmetricAlgorithm = AsymmetricAlgorithm.Create(""System.Security.Cryptography.RSA"");
    }
}",
            GetCSharpResultAt(8, 35, "System.Security.Cryptography.RSA"));
        }

        [Fact]
        public async Task TestCreateWithSystemSecurityCryptographyAsymmetricAlgorithmArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var asymmetricAlgorithm = AsymmetricAlgorithm.Create(""System.Security.Cryptography.AsymmetricAlgorithm"");
    }
}",
            GetCSharpResultAt(8, 35, "System.Security.Cryptography.AsymmetricAlgorithm"));
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"");
    }
}",
            GetCSharpResultAt(8, 28, "RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyRSAArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.RSA"");
    }
}",
            GetCSharpResultAt(8, 28, "System.Security.Cryptography.RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyAsymmetricAlgorithmArgDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.AsymmetricAlgorithm"");
    }
}",
            GetCSharpResultAt(8, 28, "System.Security.Cryptography.AsymmetricAlgorithm"));
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAAndKeySize1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"", 1024);
    }
}",
            GetCSharpResultAt(8, 28, "RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyRSAAndKeySize1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.RSA"", 1024);
    }
}",
            GetCSharpResultAt(8, 28, "System.Security.Cryptography.RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyAsymmetricAlgorithmAndKeySize1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.AsymmetricAlgorithm"", 1024);
    }
}",
            GetCSharpResultAt(8, 28, "System.Security.Cryptography.AsymmetricAlgorithm"));
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAAndObjectArray1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"", new Object[]{1024});
    }
}",
            GetCSharpResultAt(9, 28, "RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyRSAAndObjectArray1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.RSA"", new Object[]{1024});
    }
}",
            GetCSharpResultAt(9, 28, "System.Security.Cryptography.RSA"));
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyAsymmetricAlgorithmAndObjectArray1024ArgsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.AsymmetricAlgorithm"", new Object[]{1024});
    }
}",
            GetCSharpResultAt(9, 28, "System.Security.Cryptography.AsymmetricAlgorithm"));
        }

        [Fact]
        public async Task TestCaseSensitiveDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""system.security.cryptography.asymmetricalgorithm"", new Object[]{1024});
    }
}",
            GetCSharpResultAt(9, 28, "system.security.cryptography.asymmetricalgorithm"));
        }

        [Fact]
        public async Task TestReturnObjectOfRSADerivedClassNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public RSA TestMethod(RSA rsa)
    {
        return rsa;
    }
}");
        }

        [Fact]
        public async Task TestCreateObjectOfRSADerivedClassWithInt32ParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var rsaCng = new RSACng(2048);
    }
}");
        }

        [Fact]
        public async Task TestCreateObjectOfRSADerivedClassWithoutParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var rsaCng = new RSACng();
    }
}");
        }

        [Fact]
        public async Task TestCreateObjectOfRSADerivedClassWithCngKeyParameterNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(CngKey key)
    {
        var rsaCng = new RSACng(key);
    }
}");
        }

        [Fact]
        public async Task TestCreateObjectOfRSADerivedClassWithInt32ParameterUnassignedKeySizeDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(int keySize)
    {
        var rsaCng = new RSACng(keySize);
    }
}");
        }

        [Fact]
        public async Task TestCreateWithECDsaArgNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var asymmetricAlgorithm = AsymmetricAlgorithm.Create(""ECDsa"");
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithECDsaArgNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""ECDsa"");
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithECDsaAndKeySize1024ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""ECDsa"", 1024);
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAAndKeySize2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"", 2048);
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAAndKeySizeArgsUnassignedKeySizeNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(int keySize)
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"", keySize);
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyRSAAndKeySize2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.RSA"", 2048);
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyAsymmetricAlgorithmAndKeySize2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.AsymmetricAlgorithm"", 2048);
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithECDsaAndObjectArray1024ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""ECDsa"", new Object[]{1024});
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithRSAAndObjectArray2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""RSA"", new Object[]{2048});
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyRSAAndObjectArray2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.RSA"", new Object[]{2048});
    }
}");
        }

        [Fact]
        public async Task TestCreateFromNameWithSystemSecurityCryptographyAsymmetricAlgorithmAndObjectArray2048ArgsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var cryptoConfig = CryptoConfig.CreateFromName(""System.Security.Cryptography.AsymmetricAlgorithm"", new Object[]{2048});
    }
}");
        }

        [Fact]
        public async Task TestReturnVoidNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(RSA rsa)
    {
        return;
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
