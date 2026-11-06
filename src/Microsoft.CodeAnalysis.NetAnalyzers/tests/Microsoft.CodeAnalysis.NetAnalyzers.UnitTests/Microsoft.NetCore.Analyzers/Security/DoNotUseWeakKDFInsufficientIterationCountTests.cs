// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseWeakKDFInsufficientIterationCount,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseWeakKDFInsufficientIterationCount,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
    public class DoNotUseWeakKDFInsufficientIterationCountTests
    {
        private const int SufficientIterationCount = 100000;

        [TestMethod]
        public async Task PropertyWriteInEnumerator_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class Steps
                {
                    private readonly Rfc2898DeriveBytes derive;

                    public Steps(Rfc2898DeriveBytes derive) => this.derive = derive;

                    public Enumerator GetEnumerator()
                    {
                        derive.IterationCount = 1;
                        return new Enumerator();
                    }

                    public struct Enumerator
                    {
                        public bool MoveNext() => false;
                        public int Current => 0;
                    }
                }

                class TestClass
                {
                    void Method(Rfc2898DeriveBytes derive)
                    {
                        foreach (int item in new Steps(derive)) { }
                        derive.GetBytes(16);
                    }
                }
                """,
                GetCSharpResultAt(27, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task PropertyWriteInUsingDeclaration_CSharp_NoDiagnosticAsync()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System.Security.Cryptography;

                    ref struct ResetIterations
                    {
                        private readonly Rfc2898DeriveBytes derive;

                        public ResetIterations(Rfc2898DeriveBytes derive) => this.derive = derive;

                        public void Dispose() => derive.IterationCount = 100000;
                    }

                    class TestClass
                    {
                        void Method(Rfc2898DeriveBytes derive)
                        {
                            {
                                using var reset = new ResetIterations(derive);
                            }

                            derive.GetBytes(16);
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8,
            };

            await VerifyCS.RunTestAsync(test);
        }

        [TestMethod]
        public async Task PropertyWriteInLockScope_CSharp_NoDiagnosticAsync()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System.Security.Cryptography;

                    namespace System.Threading
                    {
                        public sealed class Lock
                        {
                            private readonly Rfc2898DeriveBytes derive;

                            public Lock(Rfc2898DeriveBytes derive) => this.derive = derive;

                            public Scope EnterScope()
                            {
                                derive.IterationCount = 100000;
                                return new Scope();
                            }

                            public ref struct Scope
                            {
                                public void Dispose() { }
                            }
                        }
                    }

                    class TestClass
                    {
                        void Method(Rfc2898DeriveBytes derive)
                        {
                            lock (new System.Threading.Lock(derive)) { }
                            derive.GetBytes(16);
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp13,
            };

            await VerifyCS.RunTestAsync(test);
        }

        [TestMethod]
        public async Task DerivedPropertyInitializer_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class Derived : Rfc2898DeriveBytes
                {
                    public Derived() : base("password", new byte[8]) { }

                    public new int IterationCount { get; set; } = GetIterations(null);

                    static int GetIterations(Rfc2898DeriveBytes unused) => 100000;
                }

                class TestClass
                {
                    void Method()
                    {
                        new Derived().GetBytes(16);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task DerivedPropertyInitializer_VB_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Security.Cryptography

                Public Class Derived
                    Inherits Rfc2898DeriveBytes

                    Public Sub New()
                        MyBase.New("password", New Byte(7) {})
                    End Sub

                    Public Shadows Property IterationCount As Integer = GetIterations(Nothing)

                    Private Shared Function GetIterations(unused As Rfc2898DeriveBytes) As Integer
                        Return 100000
                    End Function
                End Class

                Public Class TestClass
                    Public Sub Method()
                        Dim derived = New Derived()
                        derived.GetBytes(16)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task DefaultIterationConstructorOnInfeasibleBranch_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class TestClass
                {
                    void Method(Rfc2898DeriveBytes derive, string password, byte[] salt)
                    {
                        int mode = 0;
                        if (mode == 1)
                        {
                            derive = new Rfc2898DeriveBytes(password, salt);
                        }

                        derive.GetBytes(16);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task DefaultIterationConstructorOnInfeasibleBranch_VB_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Security.Cryptography

                Public Class TestClass
                    Public Sub Method(derive As Rfc2898DeriveBytes, password As String, salt As Byte())
                        Dim mode = 0
                        If mode = 1 Then
                            derive = New Rfc2898DeriveBytes(password, salt)
                        End If

                        derive.GetBytes(16)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task DeriveBytesParameterWithoutConstructor_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class TestClass
                {
                    void Method(Rfc2898DeriveBytes derive)
                    {
                        derive.GetBytes(16);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task DeriveBytesCreatedWithDefaultIterations_VB_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Security.Cryptography

                Public Class TestClass
                    Public Sub Method(password As String, salt As Byte())
                        Dim derive = New Rfc2898DeriveBytes(password, salt)
                        derive.GetBytes(16)
                    End Sub
                End Class
                """,
                GetBasicResultAt(6, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndByteArrayParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, byte[] salt, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestAssignIterationCountDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, byte[] salt, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
                        rfc2898DeriveBytes.IterationCount = 100;
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(10, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestAssignIterationsParameterMaybeChangedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

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
                }
                """,
            GetCSharpResultAt(18, 9, DoNotUseWeakKDFInsufficientIterationCount.MaybeUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestAssignIterationCountPropertyMaybeChangedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

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
                }
                """,
            GetCSharpResultAt(18, 9, DoNotUseWeakKDFInsufficientIterationCount.MaybeUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestPassRfc2898DeriveBytesAsParameterInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

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
                }
                """,
            GetCSharpResultAt(15, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestReturnRfc2898DeriveBytesInterproceduralDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

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
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, int saltSize, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndByteArrayAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, byte[] salt, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersLowIterationsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(byte[] password, byte[] salt, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndIntAndIntParametersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, int saltSize, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize, 100);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
        }

        [TestMethod]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Security.Cryptography;

                            class TestClass
                            {
                                public void TestMethod(byte[] password, byte[] salt, HashAlgorithmName hashAlgorithm, int cb)
                                {
                                    var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100, hashAlgorithm);
                                    rfc2898DeriveBytes.GetBytes(cb);
                                }
                            }
                            """,
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndByteArrayAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Security.Cryptography;

                            class TestClass
                            {
                                public void TestMethod(string password, byte[] salt, HashAlgorithmName hashAlgorithm, int cb)
                                {
                                    var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100, hashAlgorithm);
                                    rfc2898DeriveBytes.GetBytes(cb);
                                }
                            }
                            """,
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestConstructorWithStringAndIntAndIntAndHashAlgorithmNameParametersDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Security.Cryptography;

                            class TestClass
                            {
                                public void TestMethod(string password, int saltSize, HashAlgorithmName hashAlgorithm, int cb)
                                {
                                    var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltSize, 100, hashAlgorithm);
                                    rfc2898DeriveBytes.GetBytes(cb);
                                }
                            }
                            """,
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule),
                    },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestAssignIterationCountNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(string password, byte[] salt, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
                        rfc2898DeriveBytes.IterationCount = 100000;
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TestPassRfc2898DeriveBytesAsParameterInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
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
                }
                """);
        }

        [TestMethod]
        public async Task TestReturnRfc2898DeriveBytesInterproceduralNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
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
                }
                """);
        }

        [TestMethod]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersUnassignedIterationsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(byte[] password, byte[] salt, int iterations, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, iterations);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TestConstructorWithByteArrayAndByteArrayAndIntParametersHighIterationsNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class TestClass
                {
                    public void TestMethod(byte[] password, byte[] salt, int iterations, int cb)
                    {
                        var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 100000);
                        rfc2898DeriveBytes.GetBytes(cb);
                    }
                }
                """);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [DataRow("""
            dotnet_code_quality.CA5387.excluded_symbol_names = TestMethod
                                  dotnet_code_quality.CA5388.excluded_symbol_names = TestMethod
            """)]
        [DataRow("""
            dotnet_code_quality.CA5387.excluded_symbol_names = TestMet*
                                  dotnet_code_quality.CA5388.excluded_symbol_names = TestMet*
            """)]
        [DataRow("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Security.Cryptography;

                            class TestClass
                            {
                                public void TestMethod(string password, byte[] salt, int cb)
                                {
                                    var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt);
                                    rfc2898DeriveBytes.IterationCount = 100;
                                    rfc2898DeriveBytes.GetBytes(cb);
                                }
                            }
                            """

                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}

                        """) }
                },
            };

            if (editorConfigText.Length == 0)
            {
                test.ExpectedDiagnostics.Add(GetCSharpResultAt(10, 9, DoNotUseWeakKDFInsufficientIterationCount.DefinitelyUseWeakKDFInsufficientIterationCountRule));
            }

            await test.RunAsync(CancellationToken.None);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(SufficientIterationCount);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(SufficientIterationCount);
    }
}
