// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseSearchValuesAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseSearchValuesFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class UseSearchValuesTests
    {
        [Fact]
        public async Task TestIndexOfAnyAnalyzer()
        {
            string source =
                """
                using System;

                internal sealed class Test
                {
                    private const string ShortConstStringTypeMember = "foo";
                    private const string LongConstStringTypeMember = "aeiouA";
                    private static string NonConstStringProperty => "aeiouA";
                    private string NonConstStringInstanceField = "aeiouA";
                    private static string NonConstStringStaticField = "aeiouA";
                    private readonly string NonConstStringReadonlyInstanceField = "aeiouA";
                    private static readonly string NonConstStringReadonlyStaticField = "aeiouA";
                    private const char ConstChar = 'A';
                    private static char NonConstChar => 'A';
                    private const byte ConstByte = (byte)'A';
                    private static byte NonConstByte => (byte)'A';
                    private static readonly char[] ShortStaticReadonlyCharArrayField = new[] { 'a', 'e', 'i', 'o', 'u' };
                    private static readonly char[] LongStaticReadonlyCharArrayField = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private static readonly char[] LongStaticReadonlyCharArrayFieldWithNonInlineLiteral = new[] { 'a', 'e', 'i', 'o', 'u', ConstChar };
                    private readonly char[] LongReadonlyCharArrayFieldWithSimpleFieldModification = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private readonly char[] LongReadonlyCharArrayFieldWithSimpleElementModification = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    static readonly char[] LongStaticReadonlyCharArrayFieldWithoutAccessibility = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    public static readonly char[] LongStaticReadonlyCharArrayFieldWithPublicAccessibility = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private static readonly char[] LongStaticReadonlyExplicitCharArrayField = new char[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private readonly char[] InstanceReadonlyCharArrayField = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private char[] InstanceSettableCharArrayField = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private readonly char[] ShortReadonlyCharArrayFieldFromToCharArray = "aeiou".ToCharArray();
                    private readonly char[] LongReadonlyCharArrayFieldFromToCharArray = "aeiouA".ToCharArray();
                    private readonly char[] ShortReadonlyCharArrayFieldFromConstToCharArray = ShortConstStringTypeMember.ToCharArray();
                    private readonly char[] LongReadonlyCharArrayFieldFromConstToCharArray = LongConstStringTypeMember.ToCharArray();
                    private ReadOnlySpan<char> ShortReadOnlySpanOfCharRVAProperty => new[] { 'a', 'e', 'i', 'o', 'u' };
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharRVAProperty => new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    private ReadOnlySpan<byte> ShortReadOnlySpanOfByteRVAProperty => new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u' };
                    private ReadOnlySpan<byte> LongReadOnlySpanOfByteRVAProperty => new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' };
                    private ReadOnlySpan<char> ShortReadOnlySpanOfCharFromToCharArrayProperty => "aeiou".ToCharArray();
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharFromToCharArrayProperty => "aeiouA".ToCharArray();
                    private ReadOnlySpan<char> ShortReadOnlySpanOfCharFromConstToCharArrayProperty => ShortConstStringTypeMember.ToCharArray();
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharFromConstToCharArrayProperty => LongConstStringTypeMember.ToCharArray();
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharRVAPropertyWithGetAccessor1
                    {
                        get => new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                    }
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharRVAPropertyWithGetAccessor2
                    {
                        get
                        {
                            return new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                        }
                    }
                    private ReadOnlySpan<char> LongReadOnlySpanOfCharRVAPropertyWithGetAccessor3
                    {
                        get
                        {
                            Console.WriteLine("foo");
                            return new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                        }
                    }

                    public Test()
                    {
                        LongReadonlyCharArrayFieldWithSimpleFieldModification = new[] { 'a' };
                        Console.WriteLine(InstanceReadonlyCharArrayField);
                    }

                    public void Dummy()
                    {
                        LongReadonlyCharArrayFieldWithSimpleElementModification[0] = 'a';
                        Console.WriteLine(InstanceReadonlyCharArrayField[0]);
                    }

                    private void TestMethod(string str, ReadOnlySpan<char> chars, ReadOnlySpan<byte> bytes)
                    {
                        const string ShortConstStringLocal = "foo";
                        const string LongConstStringLocal = "aeiouA";
                        string NonConstStringLocal = "aeiouA";

                        _ = chars.IndexOfAny("aeiou");
                        _ = chars.IndexOfAny([|"aeiouA"|]);
                        _ = chars.IndexOfAny("aeiouA" + NonConstStringProperty);
                        _ = chars.IndexOfAny("aeiouA" + NonConstStringLocal);

                        _ = chars.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                        _ = chars.IndexOfAny([|new[] { 'a', 'e', 'i', 'o', 'u', 'A' }|]);
                        _ = chars.IndexOfAny([|new char[] { 'a', 'e', 'i', 'o', 'u', 'A' }|]);
                        _ = chars.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u', NonConstChar });

                        _ = chars.IndexOfAny([|new[] { 'a', 'e', /* Comment */ 'i', 'o', 'u', 'A' }|]);
                        _ = chars.IndexOfAny([|new[]
                        {
                            // Comment
                            'a', 'e', 'i', 'o', 'u', 'A'
                        }|]);

                        _ = str.IndexOfAny([|new char[] { }|]);
                        _ = str.IndexOfAny([|new[] { 'a', 'e', 'i', 'o', 'u' }|]);
                        _ = str.IndexOfAny([|new[] { 'a', 'e', 'i', 'o', 'u', 'A' }|]);
                        _ = str.IndexOfAny([|new char[] { 'a', 'e', 'i', 'o', 'u', 'A' }|]);

                        _ = str?.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u', 'A' });

                        _ = bytes.IndexOfAny(new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u' });
                        _ = bytes.IndexOfAny([|new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }|]);
                        _ = bytes.IndexOfAny([|new byte[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }|]);
                        _ = bytes.IndexOfAny(new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', NonConstByte });

                        _ = chars.IndexOfAny(ShortConstStringTypeMember);
                        _ = chars.IndexOfAny([|LongConstStringTypeMember|]);
                        _ = chars.IndexOfAny(NonConstStringProperty);

                        _ = chars.IndexOfAny(ShortConstStringLocal);
                        _ = chars.IndexOfAny([|LongConstStringLocal|]);
                        _ = chars.IndexOfAny(NonConstStringLocal);

                        _ = chars.IndexOfAny(ShortStaticReadonlyCharArrayField);
                        _ = chars.IndexOfAny([|LongStaticReadonlyCharArrayField|]);
                        _ = chars.IndexOfAny([|LongStaticReadonlyExplicitCharArrayField|]);
                        _ = chars.IndexOfAny([|InstanceReadonlyCharArrayField|]);
                        _ = chars.IndexOfAny(InstanceSettableCharArrayField);
                        _ = chars.IndexOfAny(ShortReadonlyCharArrayFieldFromToCharArray);
                        _ = chars.IndexOfAny([|LongReadonlyCharArrayFieldFromToCharArray|]);
                        _ = chars.IndexOfAny(ShortReadonlyCharArrayFieldFromConstToCharArray);
                        _ = chars.IndexOfAny([|LongReadonlyCharArrayFieldFromConstToCharArray|]);

                        _ = str.IndexOfAny(ShortStaticReadonlyCharArrayField);
                        _ = str.IndexOfAny([|LongStaticReadonlyCharArrayField|]);
                        _ = str.IndexOfAny([|LongStaticReadonlyExplicitCharArrayField|]);
                        _ = str.IndexOfAny([|InstanceReadonlyCharArrayField|]);
                        _ = str.IndexOfAny(InstanceSettableCharArrayField);
                        _ = str.IndexOfAny(ShortReadonlyCharArrayFieldFromToCharArray);
                        _ = str.IndexOfAny([|LongReadonlyCharArrayFieldFromToCharArray|]);
                        _ = str.IndexOfAny(ShortReadonlyCharArrayFieldFromConstToCharArray);
                        _ = str.IndexOfAny([|LongReadonlyCharArrayFieldFromConstToCharArray|]);

                        _ = chars.IndexOfAny([|LongStaticReadonlyCharArrayFieldWithoutAccessibility|]);
                        _ = chars.IndexOfAny(LongStaticReadonlyCharArrayFieldWithPublicAccessibility);

                        _ = chars.IndexOfAny(ShortReadOnlySpanOfCharRVAProperty);
                        _ = chars.IndexOfAny([|LongReadOnlySpanOfCharRVAProperty|]);
                        _ = chars.IndexOfAny(ShortReadOnlySpanOfCharFromToCharArrayProperty);
                        _ = chars.IndexOfAny([|LongReadOnlySpanOfCharFromToCharArrayProperty|]);
                        _ = chars.IndexOfAny(ShortReadOnlySpanOfCharFromConstToCharArrayProperty);
                        _ = chars.IndexOfAny([|LongReadOnlySpanOfCharFromConstToCharArrayProperty|]);

                        _ = bytes.IndexOfAny(ShortReadOnlySpanOfByteRVAProperty);
                        _ = bytes.IndexOfAny([|LongReadOnlySpanOfByteRVAProperty|]);

                        _ = chars.IndexOfAny([|LongReadOnlySpanOfCharRVAPropertyWithGetAccessor1|]);
                        _ = chars.IndexOfAny([|LongReadOnlySpanOfCharRVAPropertyWithGetAccessor2|]);
                        _ = chars.IndexOfAny(LongReadOnlySpanOfCharRVAPropertyWithGetAccessor3);


                        // We detect simple cases where the array may be modified
                        _ = str.IndexOfAny(LongReadonlyCharArrayFieldWithSimpleFieldModification);
                        _ = str.IndexOfAny(LongReadonlyCharArrayFieldWithSimpleElementModification);


                        // Uses of ToCharArray are flagged even for shorter values.
                        _ = chars.IndexOfAny([|ShortConstStringTypeMember.ToCharArray()|]);
                        _ = chars.IndexOfAny([|LongConstStringTypeMember.ToCharArray()|]);
                        _ = chars.IndexOfAny([|"aeiou".ToCharArray()|]);
                        _ = chars.IndexOfAny([|"aeiouA".ToCharArray()|]);

                        _ = str.IndexOfAny([|ShortConstStringTypeMember.ToCharArray()|]);
                        _ = str.IndexOfAny([|LongConstStringTypeMember.ToCharArray()|]);
                        _ = str.IndexOfAny([|"aeiou".ToCharArray()|]);
                        _ = str.IndexOfAny([|"aeiouA".ToCharArray()|]);


                        // For cases like this that we'd want to flag, a different analyzer should suggest making the field 'const' first.
                        _ = chars.IndexOfAny(NonConstStringInstanceField);
                        _ = chars.IndexOfAny(NonConstStringStaticField);
                        _ = chars.IndexOfAny(NonConstStringReadonlyInstanceField);
                        _ = chars.IndexOfAny(NonConstStringReadonlyStaticField);


                        // A few cases that could be flagged, but currently aren't:
                        _ = chars.IndexOfAny("aeiou" + 'A');
                        _ = chars.IndexOfAny("aeiou" + "A");
                        _ = chars.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u', ConstChar });
                        _ = chars.IndexOfAny("aeiouA" + ShortConstStringTypeMember);
                        _ = chars.IndexOfAny(LongConstStringTypeMember + ShortConstStringTypeMember);
                        _ = bytes.IndexOfAny(new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', ConstByte });
                        _ = bytes.IndexOfAny(new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)ConstChar });
                        _ = chars.IndexOfAny(LongStaticReadonlyCharArrayFieldWithNonInlineLiteral);
                    }
                }
                """;

            await VerifyAnalyzerAsync(LanguageVersion.CSharp7_3, source);
            await VerifyAnalyzerAsync(LanguageVersion.CSharp11, source);
        }

        [Fact]
        public async Task TestUtf8StringLiteralsAnalyzer()
        {
            await VerifyAnalyzerAsync(LanguageVersion.CSharp11,
                """
                using System;

                internal sealed class Test
                {
                    private ReadOnlySpan<byte> ShortReadOnlySpanOfByteRVAPropertyU8 => "aeiou"u8;
                    private ReadOnlySpan<byte> LongReadOnlySpanOfByteRVAPropertyU8 => "aeiouA"u8;

                    private void TestMethod(ReadOnlySpan<byte> bytes)
                    {
                        _ = bytes.IndexOfAny("aeiou"u8);
                        _ = bytes.IndexOfAny([|"aeiouA"u8|]);

                        _ = bytes.IndexOfAny(ShortReadOnlySpanOfByteRVAPropertyU8);
                        _ = bytes.IndexOfAny([|LongReadOnlySpanOfByteRVAPropertyU8|]);
                    }
                }
                """);
        }

        public static IEnumerable<object[]> TestAllIndexOfAnyAndContainsAnySpanOverloads_MemberData()
        {
            return
                from method in new[] { "IndexOfAny", "LastIndexOfAny", "IndexOfAnyExcept", "LastIndexOfAnyExcept", "ContainsAny", "ContainsAnyExcept" }
                from bytes in new[] { true, false }
                from readOnlySpan in new[] { true, false }
                select new object[] { method, bytes, readOnlySpan };
        }

        [Theory]
        [MemberData(nameof(TestAllIndexOfAnyAndContainsAnySpanOverloads_MemberData))]
        public async Task TestAllIndexOfAnyAndContainsAnySpanOverloads(string method, bool bytes, bool readOnlySpan)
        {
            string type = bytes ? "byte" : "char";
            string argumentType = $"{(readOnlySpan ? "ReadOnly" : "")}Span<{type}>";
            string valuesExpression = bytes ? "\"aeiouA\"u8" : "\"aeiouA\"";
            string searchValuesFieldName = bytes ? "s_myBytes" : "s_myChars";

            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod({{argumentType}} input)
                    {
                        _ = input.{{method}}([|{{valuesExpression}}|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<{{type}}> {{searchValuesFieldName}} = SearchValues.Create({{valuesExpression}});

                    private void TestMethod({{argumentType}} input)
                    {
                        _ = input.{{method}}({{searchValuesFieldName}});
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp11, source, expected);
        }

        [Theory]
        [InlineData("IndexOfAny")]
        [InlineData("LastIndexOfAny")]
        public async Task TestAllIndexOfAnyStringOverloads(string method)
        {
            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(string input)
                    {
                        _ = input.{{method}}([|"aeiouA".ToCharArray()|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create("aeiouA");

                    private void TestMethod(string input)
                    {
                        _ = input.AsSpan().{{method}}(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Theory]
        [InlineData("const string", "= \"aeiouA\";", true)]
        [InlineData("static readonly char[]", "= new[] { 'a', 'e', 'i', 'o', 'u', 'A' };", false)]
        [InlineData("static readonly byte[]", "= new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' };", false)]
        [InlineData("readonly char[]", "= new[] { 'a', 'e', 'i', 'o', 'u', 'A' };", false)]
        [InlineData("readonly byte[]", "= new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' };", false)]
        [InlineData("readonly char[]", "= new char[] { 'a', 'e', 'i', 'o', 'u', 'A' };", false)]
        [InlineData("readonly char[]", "= new char[]  { 'a', 'e', 'i', 'o', 'u',  'A' };", false)]
        [InlineData("ReadOnlySpan<char>", "=> new[] { 'a', 'e', 'i', 'o', 'u', 'A' };", false)]
        [InlineData("ReadOnlySpan<byte>", "=> new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' };", false)]
        [InlineData("ReadOnlySpan<byte>", "=> \"aeiouA\"u8;", false)]
        [InlineData("static ReadOnlySpan<char>", "=> new[] { 'a', 'e', 'i', 'o', 'u', 'A' };", true)]
        [InlineData("static ReadOnlySpan<byte>", "=> new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' };", true)]
        [InlineData("static ReadOnlySpan<byte>", "=> \"aeiouA\"u8;", true)]
        [InlineData("ReadOnlySpan<byte>", "{ get => \"aeiouA\"u8; }", false)]
        [InlineData("ReadOnlySpan<byte>", "{ get { return \"aeiouA\"u8; } }", false)]
        [InlineData("ReadOnlySpan<char>", "{ get => new[] { 'a', 'e', 'i', 'o', 'u', 'A' }; }", false)]
        [InlineData("ReadOnlySpan<byte>", "{ get { return new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }; } }", false)]
        [InlineData("static ReadOnlySpan<byte>", "{ get => \"aeiouA\"u8; }", true)]
        [InlineData("static ReadOnlySpan<byte>", "{ get { return \"aeiouA\"u8; } }", true)]
        [InlineData("static ReadOnlySpan<char>", "{ get => new[] { 'a', 'e', 'i', 'o', 'u', 'A' }; }", true)]
        [InlineData("static ReadOnlySpan<byte>", "{ get { return new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }; } }", true)]
        [InlineData("readonly char[]", "= \"aeiouA\".ToCharArray();", false)]
        [InlineData("static readonly char[]", "= \"aeiouA\".ToCharArray();", false)]
        [InlineData("ReadOnlySpan<char>", "=> \"aeiouA\".ToCharArray();", false)]
        [InlineData("static ReadOnlySpan<char>", "=> \"aeiouA\".ToCharArray();", true)]
        [InlineData("readonly char[]", "= ConstStringTypeMember.ToCharArray();", false, "ConstStringTypeMember")]
        [InlineData("static readonly char[]", "= ConstStringTypeMember.ToCharArray();", false, "ConstStringTypeMember")]
        [InlineData("ReadOnlySpan<char>", "=> ConstStringTypeMember.ToCharArray();", false, "ConstStringTypeMember")]
        [InlineData("static ReadOnlySpan<char>", "=> ConstStringTypeMember.ToCharArray();", true)]
        public async Task TestCodeFixerNamedArguments(string modifiersAndType, string initializer, bool createWillUseMemberReference, string createExpression = null)
        {
            const string OriginalValuesName = "MyValuesTypeMember";
            const string SearchValuesFieldName = "s_myValuesTypeMember";

            string byteOrChar = modifiersAndType.Contains("byte", StringComparison.Ordinal) ? "byte" : "char";
            string memberDefinition = $"{modifiersAndType} {OriginalValuesName} {initializer}";
            bool isProperty = initializer.Contains("=>", StringComparison.Ordinal) || initializer.Contains("get", StringComparison.Ordinal);

            string cSharp11CreateExpression = null;

            if (createWillUseMemberReference)
            {
                Assert.Null(createExpression);
                createExpression = OriginalValuesName;
            }
            else
            {
                if (byteOrChar == "char")
                {
                    createExpression ??= "\"aeiouA\"";
                }
                else
                {
                    if (createExpression is null)
                    {
                        createExpression = initializer.TrimStart('{', ' ', '=', '>', 'g', 'e', 't').TrimEnd(' ', '}').TrimEnd(';');

                        if (createExpression.StartsWith("return ", StringComparison.Ordinal))
                        {
                            createExpression = createExpression.Substring("return ".Length);
                        }
                    }

                    cSharp11CreateExpression = "\"aeiouA\"u8";
                }
            }

            await TestAsync(LanguageVersion.CSharp7_3, createExpression);
            await TestAsync(LanguageVersion.CSharp11, cSharp11CreateExpression ?? createExpression);

            async Task TestAsync(LanguageVersion languageVersion, string expectedCreateExpression)
            {
                if (languageVersion < LanguageVersion.CSharp11 &&
                    (memberDefinition.Contains("u8", StringComparison.Ordinal) || expectedCreateExpression.Contains("u8", StringComparison.Ordinal)))
                {
                    // Need CSharp 11 or newer to use Utf8 string literals
                    return;
                }

                string source =
                    $$"""
                    using System;
                    using System.Buffers;

                    internal sealed class Test
                    {
                        {{memberDefinition}}
                        private const string ConstStringTypeMember = "aeiouA";

                        private void TestMethod(ReadOnlySpan<{{byteOrChar}}> span)
                        {
                            _ = span.IndexOfAny([|{{OriginalValuesName}}|]);
                        }
                    }
                    """;

                string newLineAfterSearchValues = isProperty && createWillUseMemberReference ? Environment.NewLine : "";
                string expectedMemberDefinition = createWillUseMemberReference ? $"{memberDefinition}{Environment.NewLine}    " : "";

                string expected =
                    $$"""
                    using System;
                    using System.Buffers;

                    internal sealed class Test
                    {
                        private static readonly SearchValues<{{byteOrChar}}> {{SearchValuesFieldName}} = SearchValues.Create({{expectedCreateExpression}});{{newLineAfterSearchValues}}
                        {{expectedMemberDefinition}}private const string ConstStringTypeMember = "aeiouA";

                        private void TestMethod(ReadOnlySpan<{{byteOrChar}}> span)
                        {
                            _ = span.IndexOfAny({{SearchValuesFieldName}});
                        }
                    }
                    """;

                await VerifyCodeFixAsync(languageVersion, source, expected);
            }
        }

        [Theory]
        [InlineData("static readonly char[]", "new[] { 'a', 'e', 'i', 'o', 'u', 'A' }")]
        [InlineData("readonly char[]", "new[] { 'a', 'e', 'i', 'o', 'u', 'A' }")]
        [InlineData("readonly char[]", "new char[] { 'a', 'e', 'i', 'o', 'u', 'A' }")]
        [InlineData("readonly char[]", "new char[]  { 'a', 'e', 'i', 'o', 'u',  'A' }")]
        public async Task TestCodeFixerNamedArgumentsStringIndexOfAny(string modifiersAndType, string initializer)
        {
            const string OriginalValuesName = "MyValuesTypeMember";
            const string SearchValuesFieldName = "s_myValuesTypeMember";

            string memberDefinition = $"{modifiersAndType} {OriginalValuesName} = {initializer};";

            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    {{memberDefinition}}

                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|{{OriginalValuesName}}|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> {{SearchValuesFieldName}} = SearchValues.Create("aeiouA");

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny({{SearchValuesFieldName}});
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerConstStringMemberToCharArray()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private const string MyValuesTypeMember = "aeiouA";

                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|MyValuesTypeMember.ToCharArray()|]);
                    }
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myValuesTypeMember = SearchValues.Create(MyValuesTypeMember);
                    private const string MyValuesTypeMember = "aeiouA";

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myValuesTypeMember);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerLocalStringConst()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(ReadOnlySpan<char> chars)
                    {
                        const string Values = "aeiouA";

                        _ = chars.IndexOfAny([|Values|]);
                    }
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_values = SearchValues.Create("aeiouA");

                    private void TestMethod(ReadOnlySpan<char> chars)
                    {
                        _ = chars.IndexOfAny(s_values);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestCodeFixerLocalStringConstToCharArray(bool spanInput)
        {
            string argumentType = spanInput ? "ReadOnlySpan<char>" : "string";

            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod({{argumentType}} text)
                    {
                        const string Values = "aeiouA";

                        _ = text.IndexOfAny([|Values.ToCharArray()|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_values = SearchValues.Create("aeiouA");

                    private void TestMethod({{argumentType}} text)
                    {
                        _ = text{{(spanInput ? "" : ".AsSpan()")}}.IndexOfAny(s_values);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7_3, "\"aeiouA\"", "\"aeiouA\"")]
        [InlineData(LanguageVersion.CSharp7_3, "@\"aeiouA\"", "@\"aeiouA\"")]
        [InlineData(LanguageVersion.CSharp11, "\"aeiouA\"u8", "\"aeiouA\"u8")]
        [InlineData(LanguageVersion.CSharp7_3, "new[] { 'a', 'e', 'i', 'o', 'u', 'A' }", "\"aeiouA\"")]
        [InlineData(LanguageVersion.CSharp7_3, "new char[] { 'a', 'e', 'i', 'o', 'u', 'A' }", "\"aeiouA\"")]
        [InlineData(LanguageVersion.CSharp7_3, "new char[]  { 'a', 'e', 'i', 'o',  'u', 'A' }", "\"aeiouA\"")]
        [InlineData(LanguageVersion.CSharp7_3, "new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }", "new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }")]
        [InlineData(LanguageVersion.CSharp11, "new[] { (byte)'a', (byte)'e', (byte)'i', (byte)'o', (byte)'u', (byte)'A' }", "\"aeiouA\"u8")]
        public async Task TestCodeFixerInlineArguments(LanguageVersion languageVersion, string values, string expectedCreateArgument)
        {
            string byteOrChar = values.Contains("byte", StringComparison.Ordinal) || values.Contains("u8", StringComparison.Ordinal) ? "byte" : "char";
            string searchValuesFieldName = byteOrChar == "byte" ? "s_myBytes" : "s_myChars";

            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(ReadOnlySpan<{{byteOrChar}}> span)
                    {
                        _ = span.IndexOfAny([|{{values}}|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<{{byteOrChar}}> {{searchValuesFieldName}} = SearchValues.Create({{expectedCreateArgument}});

                    private void TestMethod(ReadOnlySpan<{{byteOrChar}}> span)
                    {
                        _ = span.IndexOfAny({{searchValuesFieldName}});
                    }
                }
                """;

            await VerifyCodeFixAsync(languageVersion, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerInlineStringLiteralToCharArray()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|"aeiouA".ToCharArray()|]);
                    }
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create("aeiouA");

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerDoesNotRemoveCommentsInArrayInitializer()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|new[] { 'a', /* Useful comment */ 'b' }|]);
                    }
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create(new[] { 'a', /* Useful comment */ 'b' });

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);

            source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|new[]
                        {
                            'a',
                            'b' // Useful comment
                        }|]);
                    }
                }
                """;

            expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create(new[]
                        {
                            'a',
                            'b' // Useful comment
                        });

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public static async Task TestCodeFixerDoesNotRemoveTheOriginalMemberIfPublic()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    public ReadOnlySpan<char> InvalidChars => new[] { 'a', 'b', 'c', 'd', 'e', 'f' };

                    public int IndexOfInvalidChar(ReadOnlySpan<char> input) =>
                        input.IndexOfAny([|InvalidChars|]);
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_invalidChars = SearchValues.Create("abcdef");

                    public ReadOnlySpan<char> InvalidChars => new[] { 'a', 'b', 'c', 'd', 'e', 'f' };

                    public int IndexOfInvalidChar(ReadOnlySpan<char> input) =>
                        input.IndexOfAny(s_invalidChars);
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerAddsSystemUsingIfNeeded()
        {
            string source =
                """
                using System.Buffers;

                internal sealed class Test
                {
                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|"aeiouA".ToCharArray()|]);
                    }
                }
                """;

            string expected =
                """
                using System.Buffers;
                using System;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create("aeiouA");

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Theory]
        [InlineData("MyValues", "s_myValues")]
        [InlineData("myValues", "s_myValues")]
        [InlineData("_myValues", "s_myValues")]
        [InlineData("_MyValues", "s_MyValues")]
        [InlineData("s_myValues", "s_myValues", false)]
        [InlineData("s_MyValues", "s_MyValues", false)]
        [InlineData("s_myValues", "s_myValuesSearchValues", true)]
        [InlineData("s_MyValues", "s_MyValuesSearchValues", true)]
        public async Task TestCodeFixerPicksFriendlyFieldNames(string memberName, string expectedFieldName, bool memberHasOtherUses = false)
        {
            string memberDefinition = $"private readonly char[] {memberName} = \"aeiouA\".ToCharArray();";
            string otherMemberUses = memberHasOtherUses ? $" _ = {memberName};" : "";

            string source =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    {{memberDefinition}}

                    private void TestMethod(ReadOnlySpan<char> text)
                    {
                        _ = text.IndexOfAny([|{{memberName}}|]);{{otherMemberUses}}
                    }
                }
                """;

            string expectedMemberDefinition = memberHasOtherUses ? $"{Environment.NewLine}    {memberDefinition}" : "";

            string expected =
                $$"""
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> {{expectedFieldName}} = SearchValues.Create("aeiouA");{{expectedMemberDefinition}}

                    private void TestMethod(ReadOnlySpan<char> text)
                    {
                        _ = text.IndexOfAny({{expectedFieldName}});{{otherMemberUses}}
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestCodeFixerAccountsForUsingStatements(bool hasSystemBuffersUsing)
        {
            string usingLine = hasSystemBuffersUsing ? "using System.Buffers;" : "";
            string systemBuffersPrefix = hasSystemBuffersUsing ? "" : "System.Buffers.";

            string source =
                $$"""
                using System;
                {{usingLine}}

                internal sealed class Test
                {
                    private void TestMethod(string text)
                    {
                        _ = text.IndexOfAny([|"aeiouA".ToCharArray()|]);
                    }
                }
                """;

            string expected =
                $$"""
                using System;
                {{usingLine}}

                internal sealed class Test
                {
                    private static readonly {{systemBuffersPrefix}}SearchValues<char> s_myChars = {{systemBuffersPrefix}}SearchValues.Create("aeiouA");

                    private void TestMethod(string text)
                    {
                        _ = text.AsSpan().IndexOfAny(s_myChars);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerAvoidsMemberNameConflicts()
        {
            string source =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static string s_myChars => "";
                    private static readonly string s_myChars1 = "";
                    private static string s_myChars2() => "";
                    private sealed class s_myChars3 { }

                    private void TestMethod(ReadOnlySpan<char> chars)
                    {
                        _ = chars.IndexOfAny([|"aeiouA"|]);
                    }
                }
                """;

            string expected =
                """
                using System;
                using System.Buffers;

                internal sealed class Test
                {
                    private static readonly SearchValues<char> s_myChars4 = SearchValues.Create("aeiouA");

                    private static string s_myChars => "";
                    private static readonly string s_myChars1 = "";
                    private static string s_myChars2() => "";
                    private sealed class s_myChars3 { }

                    private void TestMethod(ReadOnlySpan<char> chars)
                    {
                        _ = chars.IndexOfAny(s_myChars4);
                    }
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp7_3, source, expected);
        }

        [Fact]
        public async Task TestCodeFixerWorksInTopLevelStatementsDocument()
        {
            string source =
                """
                using System.Buffers;

                _ = "".IndexOfAny([|"aeiouA".ToCharArray()|]);
                """;

            string expected =
                """
                using System.Buffers;
                using System;

                _ = "".AsSpan().IndexOfAny(s_myChars);

                partial class Program
                {
                    private static readonly SearchValues<char> s_myChars = SearchValues.Create("aeiouA");
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp9, source, expected, topLevelStatements: true);

            source =
                """
                using System.Buffers;

                _ = "".IndexOfAny([|s_myValues|]);

                partial class Program
                {
                    private static readonly char[] s_myValues  = new[] { 'a', 'e', 'i', 'o', 'u', 'A' };
                }
                """;

            expected =
                """
                using System.Buffers;
                using System;

                _ = "".AsSpan().IndexOfAny(s_myValues);

                partial class Program
                {
                }

                partial class Program
                {
                    private static readonly SearchValues<char> s_myValues = SearchValues.Create("aeiouA");
                }
                """;

            await VerifyCodeFixAsync(LanguageVersion.CSharp9, source, expected, topLevelStatements: true);
        }

        private static async Task VerifyAnalyzerAsync(LanguageVersion languageVersion, string source) =>
            await VerifyCodeFixAsync(languageVersion, source, expected: null);

        private static async Task VerifyCodeFixAsync(LanguageVersion languageVersion, string source, string expected, bool topLevelStatements = false)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = Net80,
                LanguageVersion = languageVersion,
                TestCode = source,
                FixedCode = expected,
                TestState = { OutputKind = topLevelStatements ? OutputKind.ConsoleApplication : null },
            }.RunAsync();
        }

        // TEMP - need newer version of Microsoft.CodeAnalysis.Analyzer.Testing
        // Replace with 'ReferenceAssemblies.Net.Net80'
        private static readonly Lazy<ReferenceAssemblies> _lazyNet80 =
            new(() =>
            {
                if (!NuGet.Frameworks.NuGetFramework.Parse("net8.0").IsPackageBased)
                {
                    // The NuGet version provided at runtime does not recognize the 'net8.0' target framework
                    throw new NotSupportedException("The 'net8.0' target framework is not supported by this version of NuGet.");
                }

                return new ReferenceAssemblies(
                    "net8.0",
                    new PackageIdentity(
                        "Microsoft.NETCore.App.Ref",
                        "8.0.0-preview.7.23375.6"),
                    System.IO.Path.Combine("ref", "net8.0"));
            });

        public static ReferenceAssemblies Net80 => _lazyNet80.Value;
    }
}
