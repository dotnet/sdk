// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseWeakKDFAlgorithm,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseWeakKDFAlgorithmTests
    {
        [Fact]
        public async Task TestMD5Diagnostic()
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
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.MD5);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(8, 34, "Rfc2898DeriveBytes"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestSHA1Diagnostic()
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
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA1);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(8, 34, "Rfc2898DeriveBytes"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestNoHashAlgorithmNameDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
    }
}",
            GetCSharpResultAt(8, 34, "Rfc2898DeriveBytes"));
        }

        [Fact]
        public async Task TestDerivedClassOfRfc2898DeriveBytesDiagnostic()
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

class DerivedClass : Rfc2898DeriveBytes
{
    public DerivedClass (byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm) : base(password, salt, iterations, hashAlgorithm)
    {
    }
}

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var derivedClass = new DerivedClass(password, salt, iterations, HashAlgorithmName.MD5);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(15, 28, "DerivedClass"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestDerivedClassOfRfc2898DeriveBytesNewPropertyDiagnostic()
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

class DerivedClass : Rfc2898DeriveBytes
{
    public DerivedClass (byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm) : base(password, salt, iterations, hashAlgorithm)
    {
    }

    public HashAlgorithmName HashAlgorithm { get; set;}
}

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var derivedClass = new DerivedClass(password, salt, iterations, HashAlgorithmName.MD5);
        derivedClass.HashAlgorithm = HashAlgorithmName.SHA256;
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(17, 28, "DerivedClass"),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestNormalClassNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public TestClass (byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
    }

    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var subClass = new TestClass(password, salt, iterations, HashAlgorithmName.MD5);
    }
}");
        }

        [Fact]
        public async Task TestSHA256NoDiagnostic()
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
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestDerivedClassOfRfc2898DeriveBytesNoDiagnostic()
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

class DerivedClass : Rfc2898DeriveBytes
{
    public DerivedClass (byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm) : base(password, salt, iterations, hashAlgorithm)
    {
    }
}

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var derivedClass = new DerivedClass(password, salt, iterations, HashAlgorithmName.SHA256);
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestDerivedClassOfRfc2898DeriveBytesNewPropertyNoDiagnostic()
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

class DerivedClass : Rfc2898DeriveBytes
{
    public DerivedClass (byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm) : base(password, salt, iterations, hashAlgorithm)
    {
    }

    public HashAlgorithmName HashAlgorithm { get; set;}
}

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
    {
        var derivedClass = new DerivedClass(password, salt, iterations, HashAlgorithmName.SHA256);
        derivedClass.HashAlgorithm = HashAlgorithmName.MD5;
    }
}",
                    },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
