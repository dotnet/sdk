// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotSetSwitch,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotSetSwitch,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
    public class DoNotDisableSchUseStrongCryptoTests
    {
        [TestMethod]
        public async Task UnrelatedSwitchOrBenignValue_CSharp_NoDiagnosticAsync()
        {
            string calls = string.Concat(Enumerable.Repeat("Bytes(1, 2, 3, 4);\n", 64));
            Assert.AreEqual(0, await GetValueContentAnalysisCountAsync($$"""
                using System;

                class TestClass
                {
                    static void Bytes(params byte[] bytes) { }

                    void Method(string name, bool enabled)
                    {
                        {{calls}}
                        AppContext.SetSwitch("unrelated.switch", enabled);
                        AppContext.SetSwitch(name, false);
                    }
                }
                """));
        }

        [TestMethod]
        public async Task UnrelatedSwitchOrBenignValue_VB_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Public Class TestClass
                    Public Sub Method(name As String, enabled As Boolean)
                        AppContext.SetSwitch("unrelated.switch", enabled)
                        AppContext.SetSwitch(name, False)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task KnownSwitchWithComputedValue_CSharp_DiagnosticAsync()
        {
            int count = await GetValueContentAnalysisCountAsync("""
                using System;

                class TestClass
                {
                    void Method()
                    {
                        bool enabled = true;
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", enabled);
                    }
                }
                """,
                GetCSharpResultAt(8, 9, "SetSwitch"));
            Assert.IsGreaterThan(0, count);
        }

        [TestMethod]
        public async Task KnownSwitchWithComputedValue_VB_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Public Class TestClass
                    Public Sub Method()
                        Dim enabled = True
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", enabled)
                    End Sub
                End Class
                """,
                GetBasicResultAt(6, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task DocSample1_CSharp_ViolationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class ExampleClass
                {
                    public void ExampleMethod()
                    {
                        // CA5361 violation
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task DocSample1_VB_ViolationAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""

                Imports System

                Public Class ExampleClass
                    Public Sub ExampleMethod()
                        ' CA5361 violation
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true)
                    End Sub
                End Class
                """,
            GetBasicResultAt(7, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task DocSample1_CSharp_SolutionAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                public class ExampleClass
                {
                    public void ExampleMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task DocSample1_VB_SolutionAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Public Class ExampleClass
                    Public Sub ExampleMethod()
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task TestBoolDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true);
                    }
                }
                """,
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task TestEquationDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", 1 + 2 == 3);
                    }
                }
                """,
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task TestConditionalOperatorDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", 1 == 1 ? true : false);
                    }
                }
                """,
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task TestWithConstantSwitchNameDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        const string constSwitchName = "Switch.System.Net.DontEnableSchUseStrongCrypto";
                        AppContext.SetSwitch(constSwitchName, true);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        [TestMethod]
        public async Task TestBoolNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TestEquationNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", 1 + 2 != 3);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TestConditionalOperatorNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", 1 == 1 ? false : true);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TestSwitchNameNullNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch(null, true);
                    }
                }
                """);
        }

        [TestMethod]
        [TestProperty(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        public async Task TestSwitchNameVariableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        string switchName = "Switch.System.Net.DontEnableSchUseStrongCrypto";
                        AppContext.SetSwitch(switchName, true);
                    }
                }
                """,
            GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        //Ideally, we would generate a diagnostic in this case.
        [TestMethod]
        public async Task TestBoolParseNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class TestClass
                {
                    public void TestMethod()
                    {
                        AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", bool.Parse("true"));
                    }
                }
                """);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [DataRow("dotnet_code_quality.CA5361.excluded_symbol_names = TestMethod")]
        [DataRow("dotnet_code_quality.CA5361.excluded_symbol_names = TestMet*")]
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

                            using System;

                            class TestClass
                            {
                                public void TestMethod()
                                {
                                    AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true);
                                }
                            }
                            """,
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
                test.ExpectedDiagnostics.Add(GetCSharpResultAt(8, 9, "SetSwitch"));
            }

            await test.RunAsync(CancellationToken.None);
        }

        private static async Task<int> GetValueContentAnalysisCountAsync(string source, params DiagnosticResult[] expected)
        {
            int count = 0;
            var test = new CountingCSharpSecurityAnalyzerTest<DoNotSetSwitch>(() =>
                new DoNotSetSwitch { ValueContentAnalysisStarted = () => Interlocked.Increment(ref count) })
            {
                TestCode = source,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
            return Volatile.Read(ref count);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(DoNotSetSwitch.DoNotDisableSchUseStrongCryptoRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(DoNotSetSwitch.DoNotDisableSchUseStrongCryptoRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
