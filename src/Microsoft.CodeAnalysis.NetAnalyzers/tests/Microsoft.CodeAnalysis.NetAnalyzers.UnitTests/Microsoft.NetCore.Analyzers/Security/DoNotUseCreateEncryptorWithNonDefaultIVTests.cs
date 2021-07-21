// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseCreateEncryptorWithNonDefaultIV,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PropertySetAnalysis)]
    public class DoNotUseCreateEncryptorWithNonDefaultIVTests
    {
        [Fact]
        public async Task Test_CreateEncryptorWithoutParameter_NonDefaultIV_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] rgbIV)
    {
        var aesCng  = new AesCng();
        aesCng.IV = rgbIV;
        aesCng.CreateEncryptor();
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 9, DoNotUseCreateEncryptorWithNonDefaultIV.DefinitelyUseCreateEncryptorWithNonDefaultIVRule, "CreateEncryptor"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Test_CreateEncryptorWithoutParameter_NonDefaultIV_DefinitelyNotNull_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        byte[] rgbIV = new byte[] { 1, 2, 3};
        var aesCng  = new AesCng();
        aesCng.IV = rgbIV;
        aesCng.CreateEncryptor();
    }
}",

                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(11, 9, DoNotUseCreateEncryptorWithNonDefaultIV.DefinitelyUseCreateEncryptorWithNonDefaultIVRule, "CreateEncryptor"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Test_CreateEncryptorWithoutParameter_MaybeNonDefaultIV_MaybeDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] rgbIV)
    {
        var aesCng  = new AesCng();
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            aesCng.IV = rgbIV;
        }

        aesCng.CreateEncryptor();
    }
}",

                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(17, 9, DoNotUseCreateEncryptorWithNonDefaultIV.MaybeUseCreateEncryptorWithNonDefaultIVRule, "CreateEncryptor"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Test_CreateEncryptorWithByteArrayAndByteArrayParameters_DefinitelyDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] rgbKey, byte[] rgbIV)
    {
        var aesCng  = new AesCng();
        aesCng.CreateEncryptor(rgbKey, rgbIV);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseCreateEncryptorWithNonDefaultIV.DefinitelyUseCreateEncryptorWithNonDefaultIVRule, "CreateEncryptor"),
                    },
                },
            }.RunAsync();
        }
        [Fact]
        public async Task Test_CreateEncryptorWithoutParameter_DefaultIV_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        var aesCng  = new AesCng();
        aesCng.CreateEncryptor();
    }
}",
                    },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
