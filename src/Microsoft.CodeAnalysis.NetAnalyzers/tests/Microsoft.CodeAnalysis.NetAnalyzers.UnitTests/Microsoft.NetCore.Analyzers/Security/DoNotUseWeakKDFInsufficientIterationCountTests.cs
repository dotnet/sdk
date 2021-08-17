// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseWeakKDFInsufficientIterationCount,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseWeakKDFInsufficientIterationCountTests
    {
        private const int SufficientIterationCount = 100000;

        [Fact]
        public async Task TestConstructorWithStringAndByteArrayParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestAssignIterationCountDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100;
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(10, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestAssignIterationsParameterMaybeChangedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var iterations = 100;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            iterations = 100000;
        }

        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(18, 9, DoNotUseWeakKDFInsufficientIterationCount.MaybeUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestAssignIterationCountPropertyMaybeChangedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            rfc2898DeriveBytes.IterationCount = 100000;
        }

        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(18, 9, DoNotUseWeakKDFInsufficientIterationCount.MaybeUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestPassRfc2898DeriveBytesAsParameterInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100;
        InvokeGetBytes(rfc2898DeriveBytes, cb);
    }

    public void InvokeGetBytes(Rfc2898DeriveBytes rfc2898DeriveBytes, int cb)
    {
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(15, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestReturnRfc2898DeriveBytesInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = GetRfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.GetBytes(cb);
    }

    public Rfc2898DeriveBytes GetRfc2898DeriveBytes(string password, byte[] salt)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100;
    
        return rfc2898DeriveBytes;
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestConstructorWithStringAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, int saltSize, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestConstructorWithStringAndByteArrayAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersLowIterationsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestConstructorWithStringAndIntAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, int saltSize, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize, 100);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [Fact]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
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
    public void TestMethod(byte[] password, byte[] salt, HashAlgorithmName hashAlgorithm, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100, hashAlgorithm);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestConstructorWithStringAndByteArrayAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
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
    public void TestMethod(string password, byte[] salt, HashAlgorithmName hashAlgorithm, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100, hashAlgorithm);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestConstructorWithStringAndIntAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
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
    public void TestMethod(string password, int saltSize, HashAlgorithmName hashAlgorithm, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize, 100, hashAlgorithm);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestAssignIterationCountNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100000;
        rfc2898DeriveBytes.GetBytes(cb);
    }
}");
        }

        [Fact]
        public async Task TestPassRfc2898DeriveBytesAsParameterInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100000;
        InvokeGetBytes(rfc2898DeriveBytes, cb);
    }

    public void InvokeGetBytes(Rfc2898DeriveBytes rfc2898DeriveBytes, int cb)
    {
        rfc2898DeriveBytes.GetBytes(cb);
    }
}");
        }

        [Fact]
        public async Task TestReturnRfc2898DeriveBytesInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = GetRfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.GetBytes(cb);
    }

    public Rfc2898DeriveBytes GetRfc2898DeriveBytes(string password, byte[] salt)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100000;
    
        return rfc2898DeriveBytes;
    }
}");
        }

        [Fact]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersUnassignedIterationsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}");
        }

        [Fact]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersHighIterationsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] password, byte[] salt, int iterations, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100000);
        rfc2898DeriveBytes.GetBytes(cb);
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [InlineData(@"dotnet_code_quality.CA5387.excluded_symbol_names = TestMethod
                      dotnet_code_quality.CA5388.excluded_symbol_names = TestMethod")]
        [InlineData(@"dotnet_code_quality.CA5387.excluded_symbol_names = TestMet*
                      dotnet_code_quality.CA5388.excluded_symbol_names = TestMet*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(string password, byte[] salt, int cb)
    {
        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
        rfc2898DeriveBytes.IterationCount = 100;
        rfc2898DeriveBytes.GetBytes(cb);
    }
}"

                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            if (editorConfigText.Length == 0)
            {
                test.ExpectedDiagnostics.Add(GetCSharpResultAt(10, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
            }

            await test.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(SufficientIterationCount);
    }
}
