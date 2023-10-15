// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.NetCore.Analyzers.Performance.CSharpUseStringMethodCharOverloadWithSingleCharacters,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseStringMethodCharOverloadWithSingleCharactersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicUseStringMethodCharOverloadWithSingleCharacters,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicUseStringMethodCharOverloadWithSingleCharactersFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class UseStringMethodCharOverloadWithSingleCharactersTests
    {
        public static IEnumerable<object[]> Methods
        {
            get
            {
                yield return new object[] { nameof(string.StartsWith) };
                yield return new object[] { nameof(string.EndsWith) };
                yield return new object[] { nameof(string.IndexOf) };
                yield return new object[] { nameof(string.LastIndexOf) };
            }
        }

        public static IEnumerable<object[]> StartsWithEndsWithMethods
        {
            get
            {
                yield return new object[] { nameof(string.StartsWith) };
                yield return new object[] { nameof(string.EndsWith) };
            }
        }

        public static IEnumerable<object[]> IndexOfLastIndexOfMethods
        {
            get
            {
                yield return new object[] { nameof(string.IndexOf) };
                yield return new object[] { nameof(string.LastIndexOf) };
            }
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_NotSingleChar(string method)
        {
            var testCode = $$"""
                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}("abc");
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_RegularStringLiteral(string method)
        {
            var testCode = $$"""
                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1866:("a")|};
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_StringComparisonOrdinal(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:("a", StringComparison.Ordinal)|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a');
                    }
                }
                """;

            await VerifyCSAsync(
                testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode,
                (d, _, v) =>
                {
                    v.EqualOrDiff(
                        $"Use 'string.{method}(char)' instead of 'string.{method}(string)' when you have a string with a single char",
                        d.GetMessage(CultureInfo.InvariantCulture));
                });
        }

        [Fact]
        [WorkItem(6930, "https://github.com/dotnet/roslyn-analyzers/issues/6930")]
        public async Task CS_PreservesTrivia()
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod(string str)
                    {
                        if (str != "abc"
                                || 2 == str.IndexOf{|CA1865:(".", StringComparison.Ordinal)|}
                                || str == "test")
                        {
                	        return;
                        }
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod(string str)
                    {
                        if (str != "abc"
                                || 2 == str.IndexOf('.')
                                || str == "test")
                        {
                	        return;
                        }
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(IndexOfLastIndexOfMethods))]
        public async Task CS_IndexOfLastIndexOf_PreservesStartsIndexAndCount(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:("a", 0, 1, StringComparison.Ordinal)|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a', 0, 1);
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_NamedArguments(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:(comparisonType: StringComparison.Ordinal, value: "a")|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a');
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(IndexOfLastIndexOfMethods))]
        public async Task CS_IndexOfLastIndexOf_PreservesStartsIndexAndCount_NamedArguments(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:("a", count: 1, startIndex: 0, comparisonType: StringComparison.Ordinal)|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a', count: 1, startIndex: 0);
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_StringComparisonInvariantCultureAndAsciiChar(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:("a", StringComparison.InvariantCulture)|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a');
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_StringComparisonInvariantCultureAndNonAsciiChar(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1867:("あ", StringComparison.InvariantCulture)|};
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(StartsWithEndsWithMethods))]
        public async Task CS_CultureInfoInvariantCulture(string method)
        {
            var testCode = $$"""
                using System.Globalization;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1865:("a", false, CultureInfo.InvariantCulture)|};
                    }
                }
                """;

            var fixedCode = $$"""
                using System.Globalization;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}('a');
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(StartsWithEndsWithMethods))]
        public async Task CS_CultureInfoAnythingElse(string method)
        {
            var testCode = $$"""
                using System.Globalization;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1867:("a", false, CultureInfo.CurrentCulture)|};
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task CS_StringComparisonAnythingElse(string method)
        {
            var testCode = $$"""
                using System;

                public class TestClass
                {
                    public void TestMethod()
                    {
                        "test".{{method}}{|CA1867:("a", StringComparison.CurrentCulture)|};
                    }
                }
                """;

            await VerifyCSAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_NotSingleChar(string method)
        {
            var testCode = $$"""
                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}("abc")
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        [WorkItem(6930, "https://github.com/dotnet/roslyn-analyzers/issues/6930")]
        public async Task VB_PreservesTrivia()
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod(str As String)
                        If str <> "abc" _
                                Or 2 = str.IndexOf{|CA1865:(".", StringComparison.Ordinal)|} _
                                Or str = "test"
                            Return
                        End If
                    End Sub
                End Class
                """;

            var fixedCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod(str As String)
                        If str <> "abc" _
                                Or 2 = str.IndexOf("."c) _
                                Or str = "test"
                            Return
                        End If
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_RegularStringLiteral(string method)
        {
            var testCode = $$"""
                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1866:("a")|}
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_StringComparisonOrdinal(string method)
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1865:("a", StringComparison.Ordinal)|}
                    End Sub
                End Class
                """;

            var fixedCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}("a"c)
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(IndexOfLastIndexOfMethods))]
        public async Task VB_IndexOfLastIndexOf_PreservesStartsIndexAndCount(string method)
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1865:("a", 0, 1, StringComparison.Ordinal)|}
                    End Sub
                End Class
                """;

            var fixedCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}("a"c, 0, 1)
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_StringComparisonInvariantCultureAndAsciiChar(string method)
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1865:("a", StringComparison.InvariantCulture)|}
                    End Sub
                End Class
                """;

            var fixedCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1865:("a"c)|}
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedCode);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_StringComparisonInvariantCultureAndNonAsciiChar(string method)
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1867:("あ", StringComparison.InvariantCulture)|}
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task VB_StringComparisonAnythingElse(string method)
        {
            var testCode = $$"""
                Imports System

                Public Class TestClass
                    Public Sub TestMethod()
                        Dim a = "test".{{method}}{|CA1867:("a", StringComparison.CurrentCulture)|}
                    End Sub
                End Class
                """;

            await VerifyVBAsync(testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        private static async Task VerifyCSAsync(
            string source,
            ReferenceAssemblies referenceAssemblies,
            string fixedSource = null,
            Action<Diagnostic, DiagnosticResult, IVerifier> diagnosticVerifier = null)
        {
            await new VerifyCS.Test()
            {
                TestCode = source,
                ReferenceAssemblies = referenceAssemblies,
                FixedCode = fixedSource,
                DiagnosticVerifier = diagnosticVerifier,
            }.RunAsync();
        }

        private static async Task VerifyVBAsync(
            string source,
            ReferenceAssemblies referenceAssemblies,
            string fixedSource = null,
            Action<Diagnostic, DiagnosticResult, IVerifier> diagnosticVerifier = null)
        {
            await new VerifyVB.Test()
            {
                TestCode = source,
                ReferenceAssemblies = referenceAssemblies,
                FixedCode = fixedSource,
                DiagnosticVerifier = diagnosticVerifier,
            }.RunAsync();
        }
    }
}
