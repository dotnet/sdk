// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferConvertToHexStringOverBitConverterAnalyzer,
    Microsoft.NetCore.Analyzers.Performance.PreferConvertToHexStringOverBitConverterFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferConvertToHexStringOverBitConverterAnalyzer,
    Microsoft.NetCore.Analyzers.Performance.PreferConvertToHexStringOverBitConverterFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferConvertToHexStringOverBitConverterTests
    {
        [Fact]
        public async Task DataOnly_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = [|BitConverter.ToString(data).Replace("-", "")|];
                        s = [|BitConverter.ToString(data).Replace("-", string.Empty)|];
                        s = [|BitConverter.ToString(data).Replace("-", null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataOnlyReplaceWithStringComparison_OffersFixer_CS(StringComparison stringComparison)
        {
            string source = $$"""
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = [|BitConverter.ToString(data).Replace("-", "", StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data).Replace("-", string.Empty, StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data).Replace("-", null, StringComparison.{{stringComparison}})|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataOnlyReplaceWithBoolAndCultureInfo_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = [|BitConverter.ToString(data).Replace("-", "", true, null)|];
                        s = [|BitConverter.ToString(data).Replace("-", "", false, null)|];
                        s = [|BitConverter.ToString(data).Replace("-", string.Empty, true, null)|];
                        s = [|BitConverter.ToString(data).Replace("-", string.Empty, false, null)|];
                        s = [|BitConverter.ToString(data).Replace("-", null, true, null)|];
                        s = [|BitConverter.ToString(data).Replace("-", null, false, null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStart_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = [|BitConverter.ToString(data, start).Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start).Replace("-", string.Empty)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartNoSystemImport_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = [|System.BitConverter.ToString(data, start).Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataWithStartReplaceWithStringComparison_OffersFixer_CS(StringComparison stringComparison)
        {
            string source = $$"""
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = [|BitConverter.ToString(data, start).Replace("-", "", StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data, start).Replace("-", string.Empty, StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data, start).Replace("-", null, StringComparison.{{stringComparison}})|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartReplaceWithBoolAndCultureInfo_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = [|BitConverter.ToString(data, start).Replace("-", "", true, null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", "", false, null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", string.Empty, true, null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", string.Empty, false, null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", null, true, null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", null, false, null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start)
                    {
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartAndLength_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", string.Empty)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataWithStartAndLengthReplaceWithStringComparison_OffersFixer_CS(StringComparison stringComparison)
        {
            string source = $$"""
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", string.Empty, StringComparison.{{stringComparison}})|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", null, StringComparison.{{stringComparison}})|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartAndLengthReplaceWithBoolAndCultureInfo_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", true, null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", false, null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", string.Empty, true, null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", string.Empty, false, null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", null, true, null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", null, false, null)|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerAfterReplaceIsPreservedWhenToHexStringLowerIsNotAvailable_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower()|];
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower(null)|];
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLowerInvariant()|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = Convert.ToHexString(data).ToLower();
                        s = Convert.ToHexString(data).ToLower(null);
                        s = Convert.ToHexString(data).ToLowerInvariant();
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerBeforeReplaceIsPreservedWhenToHexStringLowerIsNotAvailable_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = [|BitConverter.ToString(data).ToLower().Replace("-", "")|];
                        s = [|BitConverter.ToString(data).ToLower(null).Replace("-", "")|];
                        s = [|BitConverter.ToString(data).ToLowerInvariant().Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = Convert.ToHexString(data).ToLower();
                        s = Convert.ToHexString(data).ToLower(null);
                        s = Convert.ToHexString(data).ToLowerInvariant();
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerAfterReplaceIsReplacedWithToHexStringLower_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower()|];
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower(null)|];
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLowerInvariant()|];
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLower()|];
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLower(null)|];
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLowerInvariant()|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLower()|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLower(null)|];
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLowerInvariant()|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data, start, length);
                        s = Convert.ToHexStringLower(data, start, length);
                        s = Convert.ToHexStringLower(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource, ReferenceAssemblies.Net.Net90);
        }

        [Fact]
        public async Task ToLowerBeforeReplaceIsReplacedWithToHexStringLower_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(data).ToLower().Replace("-", "")|];
                        s = [|BitConverter.ToString(data).ToLower(null).Replace("-", "")|];
                        s = [|BitConverter.ToString(data).ToLowerInvariant().Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start).ToLower().Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start).ToLower(null).Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start).ToLowerInvariant().Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start, length).ToLower().Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start, length).ToLower(null).Replace("-", "")|];
                        s = [|BitConverter.ToString(data, start, length).ToLowerInvariant().Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data);
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start));
                        s = Convert.ToHexStringLower(data, start, length);
                        s = Convert.ToHexStringLower(data, start, length);
                        s = Convert.ToHexStringLower(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource, ReferenceAssemblies.Net.Net90);
        }

        [Fact]
        public async Task NamedArguments_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(value: data).Replace("-", "")|];
                        s = [|BitConverter.ToString(value: data, startIndex: start).Replace("-", "")|];
                        s = [|BitConverter.ToString(value: data, startIndex: start, length: length).Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexString(data);
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsSwapped_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = [|BitConverter.ToString(startIndex: start, value: data).Replace("-", "")|];
                        s = [|BitConverter.ToString(value: data, length: length, startIndex: start).Replace("-", "")|];
                        s = [|BitConverter.ToString(startIndex: start, value: data, length: length).Replace("-", "")|];
                        s = [|BitConverter.ToString(startIndex: start, length: length, value: data).Replace("-", "")|];
                        s = [|BitConverter.ToString(length: length, value: data, startIndex: start).Replace("-", "")|];
                        s = [|BitConverter.ToString(length: length, startIndex: start, value: data).Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data, int start, int length)
                    {
                        s = Convert.ToHexString(data.AsSpan().Slice(start));
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                        s = Convert.ToHexString(data, start, length);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        // reticulates the splines
                        s = [|BitConverter.ToString(data).Replace("-", "")|];
                    }
                }
                """;

            string fixedSource = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        // reticulates the splines
                        s = Convert.ToHexString(data);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NotAStringReplaceMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;

                public static class StringExtensions
                {
                    public static string MyReplace(this string s, string oldValue, string newValue)
                    {
                        return s.Replace(oldValue, newValue);
                    }
                }

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).MyReplace("-", "");
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task StringReplaceCharMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).Replace('-', ' ');
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task InstanceIsNoInvocation_NoDiagnostic_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        string temp = BitConverter.ToString(data);
                        s = temp.Replace("-", "");
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task NotASystemBitConverterToStringMethod_NoDiagnostic_CS()
        {
            string source = """
                public static class BitConverter
                {
                    public static string ToString(byte[] value)
                    {
                        return string.Empty;
                    }
                }

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).Replace("-", "");
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReplaceOldValueIsNoSingleHyphenStringConstant_NoDiagnostic_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).Replace("not a single hyphen", "");
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReplaceNewValueIsNoEmptyOrNullStringConstant_NoDiagnostic_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).Replace("-", "not empty or null");
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MissingConvertToHexStringSymbol_NoDiagnostic_CS()
        {
            string source = """
                using System;

                class C
                {
                    void M(string s, byte[] data)
                    {
                        s = BitConverter.ToString(data).Replace("-", "");
                    }
                }
                """;

            // .NET Core 3.1 does not have access to Convert.ToHexString
            await VerifyCSharpCodeFixAsync(source, source, ReferenceAssemblies.NetCore.NetCoreApp31);
        }

        [Fact]
        public async Task DataOnly_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = [|BitConverter.ToString(data).Replace("-", "")|]
                        s = [|BitConverter.ToString(data).Replace("-", String.Empty)|]
                        s = [|BitConverter.ToString(data).Replace("-", Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataOnlyReplaceWithStringComparison_OffersFixer_VB(StringComparison stringComparison)
        {
            string source = $"""
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = [|BitConverter.ToString(data).Replace("-", "", StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data).Replace("-", String.Empty, StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data).Replace("-", Nothing, StringComparison.{stringComparison})|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataOnlyReplaceWithBoolAndCultureInfo_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = [|BitConverter.ToString(data).Replace("-", "", true, Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", "", false, Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", String.Empty, true, Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", String.Empty, false, Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", Nothing, true, Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", Nothing, false, Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStart_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = [|BitConverter.ToString(data, start).Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start).Replace("-", String.Empty)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartNoSystemImport_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(s As String, data As Byte(), start As Integer)
                        s = [|System.BitConverter.ToString(data, start).Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start As Integer)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataWithStartReplaceWithStringComparison_OffersFixer_VB(StringComparison stringComparison)
        {
            string source = $"""
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = [|BitConverter.ToString(data, start).Replace("-", "", StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data, start).Replace("-", String.Empty, StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data, start).Replace("-", Nothing, StringComparison.{stringComparison})|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartReplaceWithBoolAndCultureInfo_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = [|BitConverter.ToString(data, start).Replace("-", "", true, Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", "", false, Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", String.Empty, true, Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", String.Empty, false, Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", Nothing, true, Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", Nothing, false, Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartAndLength_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", String.Empty)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [CombinatorialData]
        public async Task DataWithStartAndLengthReplaceWithStringComparison_OffersFixer_VB(StringComparison stringComparison)
        {
            string source = $"""
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", String.Empty, StringComparison.{stringComparison})|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", Nothing, StringComparison.{stringComparison})|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DataWithStartAndLengthReplaceWithBoolAndCultureInfo_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", true, Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "", false, Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", String.Empty, true, Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", String.Empty, false, Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", Nothing, true, Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", Nothing, false, Nothing)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerAfterReplaceIsPreservedWhenToHexStringLowerIsNotAvailable_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower()|]
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower(Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLowerInvariant()|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = Convert.ToHexString(data).ToLower()
                        s = Convert.ToHexString(data).ToLower(Nothing)
                        s = Convert.ToHexString(data).ToLowerInvariant()
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerBeforeReplaceIsPreservedWhenToHexStringLowerIsNotAvailable_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = [|BitConverter.ToString(data).ToLower().Replace("-", "")|]
                        s = [|BitConverter.ToString(data).ToLower(Nothing).Replace("-", "")|]
                        s = [|BitConverter.ToString(data).ToLowerInvariant().Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = Convert.ToHexString(data).ToLower()
                        s = Convert.ToHexString(data).ToLower(Nothing)
                        s = Convert.ToHexString(data).ToLowerInvariant()
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ToLowerAfterReplaceIsReplacedWithToHexStringLower_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower()|]
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLower(Nothing)|]
                        s = [|BitConverter.ToString(data).Replace("-", "").ToLowerInvariant()|]
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLower()|]
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLower(Nothing)|]
                        s = [|BitConverter.ToString(data, start).Replace("-", "").ToLowerInvariant()|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLower()|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLower(Nothing)|]
                        s = [|BitConverter.ToString(data, start, length).Replace("-", "").ToLowerInvariant()|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data, start, length)
                        s = Convert.ToHexStringLower(data, start, length)
                        s = Convert.ToHexStringLower(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource, ReferenceAssemblies.Net.Net90);
        }

        [Fact]
        public async Task ToLowerBeforeReplaceIsReplacedWithToHexStringLower_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(data).ToLower().Replace("-", "")|]
                        s = [|BitConverter.ToString(data).ToLower(Nothing).Replace("-", "")|]
                        s = [|BitConverter.ToString(data).ToLowerInvariant().Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start).ToLower().Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start).ToLower(Nothing).Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start).ToLowerInvariant().Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start, length).ToLower().Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start, length).ToLower(Nothing).Replace("-", "")|]
                        s = [|BitConverter.ToString(data, start, length).ToLowerInvariant().Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data)
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data.AsSpan().Slice(start))
                        s = Convert.ToHexStringLower(data, start, length)
                        s = Convert.ToHexStringLower(data, start, length)
                        s = Convert.ToHexStringLower(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource, ReferenceAssemblies.Net.Net90);
        }

        [Fact]
        public async Task NamedArguments_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(value:=data).Replace("-", "")|]
                        s = [|BitConverter.ToString(value:=data, startIndex:=start).Replace("-", "")|]
                        s = [|BitConverter.ToString(value:=data, startIndex:=start, length:=length).Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexString(data)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsSwapped_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = [|BitConverter.ToString(startIndex:=start, value:=data).Replace("-", "")|]
                        s = [|BitConverter.ToString(value:=data, length:=length, startIndex:=start).Replace("-", "")|]
                        s = [|BitConverter.ToString(startIndex:=start, value:=data, length:=length).Replace("-", "")|]
                        s = [|BitConverter.ToString(startIndex:=start, length:=length, value:=data).Replace("-", "")|]
                        s = [|BitConverter.ToString(length:=length, value:=data, startIndex:=start).Replace("-", "")|]
                        s = [|BitConverter.ToString(length:=length, startIndex:=start, value:=data).Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte(), start as Integer, length as Integer)
                        s = Convert.ToHexString(data.AsSpan().Slice(start))
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                        s = Convert.ToHexString(data, start, length)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        ' reticulates the splines
                        s = [|BitConverter.ToString(data).Replace("-", "")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        ' reticulates the splines
                        s = Convert.ToHexString(data)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NotAStringReplaceMethod_NoDiagnostic_VB()
        {
            string source = """
                Imports System
                Imports System.Runtime.CompilerServices

                Module StringExtensions
                    <Extension>
                    Public Function MyReplace(s As String, oldValue As String, newValue As String) As String
                        return s.Replace(oldValue, newValue)
                    End Function
                End Module

                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).MyReplace("-", "")
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task StringReplaceCharMethod_NoDiagnostic_VB()
        {
            string source = """
                Imports System

                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).Replace("-"c, " "c)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task InstanceIsNoInvocation_NoDiagnostic_VB()
        {
            string source = """
                Imports System
                
                Class C
                    Sub M(s As String, data As Byte())
                        Dim temp = BitConverter.ToString(data)
                        s = temp.Replace("-", "")
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task NotASystemBitConverterToStringMethod_NoDiagnostic_VB()
        {
            string source = """
                Class BitConverter
                    Shared Function ToString(value As byte()) As String
                        return string.Empty
                    End Function
                End Class

                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).Replace("-", "")
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReplaceOldValueIsNoSingleHyphenStringConstant_NoDiagnostic_VB()
        {
            string source = """
                Imports System
                
                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).Replace("not a single hyphen", "")
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReplaceNewValueIsNoEmptyOrNullStringConstant_NoDiagnostic_VB()
        {
            string source = """
                Imports System
                
                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).Replace("-", "not empty or null")
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MissingConvertToHexStringSymbol_NoDiagnostic_VB()
        {
            string source = """
                Imports System
                
                Class C
                    Sub M(s As String, data As Byte())
                        s = BitConverter.ToString(data).Replace("-", "")
                    End Sub
                End Class
                """;

            // .NET Core 3.1 does not have access to Convert.ToHexString
            await VerifyBasicCodeFixAsync(source, source, ReferenceAssemblies.NetCore.NetCoreApp31);
        }

        private static async Task VerifyCSharpCodeFixAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies = null)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        private static async Task VerifyBasicCodeFixAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies = null)
        {
            await new VerifyVB.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }
    }
}
