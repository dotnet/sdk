// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPrefixEnumValuesWithTypeNameAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotPrefixEnumValuesWithTypeNameAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.ApiDesignGuidelines
{
    public class DoNotPrefixEnumValuesWithTypeNameTests
    {
        [Fact]
        public async Task CSharp_NoDiagnostic_NoPrefixAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                { 
                    enum State
                    {
                        Ok = 0,
                        Error = 1,
                        Unknown = 2
                    };
                }");
        }

        [Fact]
        public async Task Basic_NoDiagnostic_NoPrefixAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
                Class A
                    Private Enum State
                        Ok = 0
                        Err = 1
                        Unknown = 2
                    End Enum
                End Class");
        }

        [Fact]
        public async Task CSharp_Diagnostic_EachValuePrefixedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2
                    };
                }",
                GetCSharpResultAt(6, 25, "State"),
                GetCSharpResultAt(7, 25, "State"),
                GetCSharpResultAt(8, 25, "State"));
        }

        [Fact]
        public async Task Basic_Diagnostic_EachValuePrefixedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
                Class A
                    Private Enum State
                        StateOk = 0
                        StateErr = 1
                        StateUnknown = 2
                    End Enum
                End Class
                ",
                GetBasicResultAt(4, 25, "State"),
                GetBasicResultAt(5, 25, "State"),
                GetBasicResultAt(6, 25, "State"));
        }

        [Fact]
        public async Task CSharp_NoDiagnostic_HalfOfValuesPrefixedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                {
                    enum State
                    {
                        Ok = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        Invalid = 3
                    };
                }");
        }

        [Fact]
        public async Task CSharp_Diagnostic_ThreeOfFourValuesPrefixedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        Invalid = 3
                    };
                }",
                GetCSharpResultAt(6, 25, "State"),
                GetCSharpResultAt(7, 25, "State"),
                GetCSharpResultAt(8, 25, "State"));
        }

        [Fact]
        public async Task CSharp_Diagnostic_PrefixCaseDiffersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                {
                    enum State
                    {
                        stateOk = 0
                    };
                }",
                GetCSharpResultAt(6, 25, "State"));
        }

        [Fact]
        public async Task CSharp_NoDiagnostic_EmptyEnumAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                class A
                {
                    enum State
                    {
                    };
                }");
        }

        [Theory]
        // No data
        [InlineData("")]
        // Invalid option
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = invalid")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AnyEnumValue, AllEnumValues")]
        // Valid options
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AnyEnumValue")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AllEnumValues")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = Heuristic")]
        public async Task AllValuesPrefixed_DiagnosticAsync(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        StateInvalid = 3
                    }
                }"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(6, 25, "State"),
                        GetCSharpResultAt(7, 25, "State"),
                        GetCSharpResultAt(8, 25, "State"),
                        GetCSharpResultAt(9, 25, "State"),
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                Class A
                    Enum State
                        StateOk = 0
                        StateError = 1
                        StateUnknown = 2
                        StateInvalid = 3
                    End Enum
                End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(4, 25, "State"),
                        GetBasicResultAt(5, 25, "State"),
                        GetBasicResultAt(6, 25, "State"),
                        GetBasicResultAt(7, 25, "State"),
                    }
                }
            }.RunAsync();
        }

        [Theory]
        // No data
        [InlineData("")]
        // Invalid option
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = invalid")]
        // Valid options
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AnyEnumValue")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AllEnumValues")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = Heuristic")]
        public async Task OneOfFourValuesPrefixed_DiagnosticAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        Error = 1,
                        Unknown = 2,
                        Invalid = 3
                    }
                }"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (editorConfigText.EndsWith("AnyEnumValue", StringComparison.OrdinalIgnoreCase))
            {
                csharpTest.ExpectedDiagnostics.Add(GetCSharpResultAt(6, 25, "State"));
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                Class A
                    Enum State
                        StateOk = 0
                        [Error] = 1
                        Unknown = 2
                        Invalid = 3
                    End Enum
                End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (editorConfigText.EndsWith("AnyEnumValue", StringComparison.OrdinalIgnoreCase))
            {
                vbTest.ExpectedDiagnostics.Add(GetBasicResultAt(4, 25, "State"));
            }

            await vbTest.RunAsync();
        }

        [Theory]
        // No data
        [InlineData("")]
        // Invalid option
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = invalid")]
        // Valid options
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AnyEnumValue")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = AllEnumValues")]
        [InlineData("dotnet_code_quality.CA1712.enum_values_prefix_trigger = Heuristic")]
        public async Task ThreeOfFourValuesPrefixed_DiagnosticAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        Invalid = 3
                    }
                }"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (!editorConfigText.EndsWith("AllEnumValues", StringComparison.OrdinalIgnoreCase))
            {
                csharpTest.ExpectedDiagnostics.AddRange(
                    new[]
                    {
                        GetCSharpResultAt(6, 25, "State"),
                        GetCSharpResultAt(7, 25, "State"),
                        GetCSharpResultAt(8, 25, "State"),
                    });
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
                Class A
                    Enum State
                        StateOk = 0
                        StateError = 1
                        StateUnknown = 2
                        Invalid = 3
                    End Enum
                End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (!editorConfigText.EndsWith("AllEnumValues", StringComparison.OrdinalIgnoreCase))
            {
                vbTest.ExpectedDiagnostics.AddRange(
                    new[]
                    {
                        GetBasicResultAt(4, 25, "State"),
                        GetBasicResultAt(5, 25, "State"),
                        GetBasicResultAt(6, 25, "State"),
                    });
            }

            await vbTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
