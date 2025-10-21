// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpConstantExpectedAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public sealed class ConstantExpectedTests
    {
        [Theory]
        [InlineData("char", "char.MinValue", "char.MaxValue")]
        [InlineData("sbyte", "sbyte.MinValue", "sbyte.MaxValue")]
        [InlineData("short", "short.MinValue", "short.MaxValue")]
        [InlineData("int", "int.MinValue", "int.MaxValue")]
        [InlineData("long", "long.MinValue", "long.MaxValue")]
        [InlineData("byte", "byte.MinValue", "byte.MaxValue")]
        [InlineData("ushort", "ushort.MinValue", "ushort.MaxValue")]
        [InlineData("uint", "uint.MinValue", "uint.MaxValue")]
        [InlineData("ulong", "ulong.MinValue", "ulong.MaxValue")]
        [InlineData("bool", "false", "true")]
        [InlineData("float", "float.MinValue", "float.MaxValue")]
        [InlineData("double", "double.MinValue", "double.MaxValue")]
        public static async Task TestConstantExpectedSupportedUnmanagedTypesAsync(string type, string minValue, string maxValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod1([ConstantExpected] {type} val) {{ }}
    public static void TestMethod2([ConstantExpected(Min={minValue})] {type} val) {{ }}
    public static void TestMethod3([ConstantExpected(Max={maxValue})] {type} val) {{ }}
    public static void TestMethod4([ConstantExpected(Min={minValue}, Max={maxValue})] {type} val) {{ }}
    public static void TestMethod5([ConstantExpected(Min=null)] {type} val) {{ }}
    public static void TestMethod6([ConstantExpected(Max=null)] {type} val) {{ }}
    public static void TestMethod7([ConstantExpected(Min=null, Max=null)] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("sbyte", "sbyte.MinValue", "sbyte.MaxValue")]
        [InlineData("short", "short.MinValue", "short.MaxValue")]
        [InlineData("int", "int.MinValue", "int.MaxValue")]
        [InlineData("long", "long.MinValue", "long.MaxValue")]
        [InlineData("byte", "byte.MinValue", "byte.MaxValue")]
        [InlineData("ushort", "ushort.MinValue", "ushort.MaxValue")]
        [InlineData("uint", "uint.MinValue", "uint.MaxValue")]
        [InlineData("ulong", "ulong.MinValue", "ulong.MaxValue")]
        public static async Task TestConstantExpectedSupportedEnumTypesAsync(string type, string minValue, string maxValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod1([ConstantExpected] AEnum val) {{ }}
    public static void TestMethod2([ConstantExpected(Min={minValue})] AEnum val) {{ }}
    public static void TestMethod3([ConstantExpected(Max={maxValue})] AEnum val) {{ }}
    public static void TestMethod4([ConstantExpected(Min={minValue}, Max={maxValue})] AEnum val) {{ }}
    public static void TestMethod5([ConstantExpected(Min=null)] AEnum val) {{ }}
    public static void TestMethod6([ConstantExpected(Max=null)] AEnum val) {{ }}
    public static void TestMethod7([ConstantExpected(Min=null, Max=null)] AEnum val) {{ }}
}}

public enum AEnum : {type}
{{
    One,
    Two
}}
";
            await TestCSAsync(csInput);
        }

        [Fact]
        public static async Task TestConstantExpectedSupportedComplexTypesAsync()
        {
            string csInput = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{
    public static void TestMethodString([ConstantExpected] string val) { }
    public static void TestMethodGeneric<T>([ConstantExpected] T val) { }
    
    public static class GenenricClass<T>
    {
        public static void TestMethodGeneric([ConstantExpected] T val) { }
    }
}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("char")]
        [InlineData("sbyte")]
        [InlineData("short")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("byte")]
        [InlineData("ushort")]
        [InlineData("uint")]
        [InlineData("ulong")]
        [InlineData("float")]
        [InlineData("double")]
        [InlineData("bool")]
        [InlineData("string")]
        public static async Task TestConstantExpectedSupportedComplex2TypesAsync(string type)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public interface ITest<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public interface ITest2<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public abstract class AbstractTest<T>
    {{
        public abstract T Method2(T operand1, [ConstantExpected] T operand2);
    }}

    public class Generic : AbstractTest<{type}>, ITest<{type}>, ITest2<{type}>
    {{
        public {type} Method({type} operand1, {{|#0:{type} operand2|}}) => throw new NotImplementedException();
        {type} ITest2<{type}>.Method({type} operand1, {{|#1:{type} operand2|}}) => throw new NotImplementedException();
        public override {type} Method2({type} operand1, {{|#2:{type} operand2|}}) => throw new NotImplementedException();
    }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(0),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(1),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(2));
        }

        [Theory]
        [InlineData("char")]
        [InlineData("sbyte")]
        [InlineData("short")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("byte")]
        [InlineData("ushort")]
        [InlineData("uint")]
        [InlineData("ulong")]
        [InlineData("float")]
        [InlineData("double")]
        [InlineData("bool")]
        [InlineData("string")]
        public static async Task TestMissingConstantExpectedSupportedComplex2TypesAsync(string type)
        {
            string csInput = @$"
using System;
using Similar;
#nullable enable

public class Test
{{
    public interface ITest<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public interface ITest2<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public abstract class AbstractTest<T>
    {{
        public abstract T Method2(T operand1, [ConstantExpected] T operand2);
    }}

    public class Generic : AbstractTest<{type}>, ITest<{type}>, ITest2<{type}>
    {{
        public {type} Method({type} operand1, {type} operand2) => throw new NotImplementedException();
        {type} ITest2<{type}>.Method({type} operand1, {type} operand2) => throw new NotImplementedException();
        public override {type} Method2({type} operand1, {type} operand2) => throw new NotImplementedException();
    }}
}}
";
            await TestCSMissingAttributeAsync(csInput);
        }

        [Fact]
        public static async Task TestConstantExpectedSupportedComplex3TypesAsync()
        {
            string csInput = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{
    public interface ITest<T>
    {
        T Method(T operand1, [ConstantExpected] T operand2);
    }
    public interface ITest2<T>
    {
        T Method(T operand1, [ConstantExpected] T operand2);
    }
    public abstract class AbstractTest<T>
    {
        public abstract T Method2(T operand1, [ConstantExpected] T operand2);
    }
    public class GenericForward<T> : AbstractTest<T>, ITest<T>, ITest2<T>
    {
        public T Method(T operand1, {|#0:T operand2|}) => throw new NotImplementedException();
        T ITest2<T>.Method(T operand1, {|#1:T operand2|}) => throw new NotImplementedException();
        public override T Method2(T operand1, {|#2:T operand2|}) => throw new NotImplementedException();
    }
}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(0),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(1),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.AttributeExpectedRule)
                        .WithLocation(2));
        }

        [Theory]
        [InlineData("", "", "nint", "nint")]
        [InlineData("", "", "nuint", "nuint")]
        [InlineData("", "", "object", "object")]
        [InlineData("", "", "Test", "Test")]
        [InlineData("", "", "Guid", "System.Guid")]
        [InlineData("", "", "decimal", "decimal")]
        [InlineData("", "", "byte[]", "byte[]")]
        [InlineData("", "", "(int, long)", "(int, long)")]
        [InlineData("<T>", "", "T[]", "T[]")]
        [InlineData("", "<T>", "T[]", "T[]")]
        public static async Task TestConstantExpectedUnsupportedTypesAsync(string classGeneric, string methodGeneric, string type, string diagnosticType)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test{classGeneric}
{{
    public static void TestMethod{methodGeneric}([{{|#0:ConstantExpected|}}] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.UnsupportedTypeRule)
                        .WithLocation(0)
                        .WithArguments(diagnosticType));
        }

        [Theory]
        [InlineData("object")]
        [InlineData("Test")]
        [InlineData("Guid")]
        [InlineData("decimal")]
        [InlineData("byte[]")]
        [InlineData("(int, long)")]
        public static async Task TestConstantExpectedUnsupportedIgnoredComplexTypesAsync(string type)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public interface ITest<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public interface ITest2<T>
    {{
        T Method(T operand1, [ConstantExpected] T operand2);
    }}
    public abstract class AbstractTest<T>
    {{
        public abstract T Method2(T operand1, [ConstantExpected] T operand2);
    }}
    public class Generic : AbstractTest<{type}>, ITest<{type}>, ITest2<{type}>
    {{
        public {type} Method({type} operand1, {type} operand2) => throw new NotImplementedException();
        {type} ITest2<{type}>.Method({type} operand1, {type} operand2) => throw new NotImplementedException();
        public override {type} Method2({type} operand1, {type} operand2) => throw new NotImplementedException();
    }}
}}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("", "", "char", "\"a\"", "\"a\"")]
        [InlineData("", "", "sbyte", "\"a\"", "\"a\"")]
        [InlineData("", "", "short", "\"a\"", "\"a\"")]
        [InlineData("", "", "int", "\"a\"", "\"a\"")]
        [InlineData("", "", "long", "\"a\"", "\"a\"")]
        [InlineData("", "", "byte", "\"a\"", "\"a\"")]
        [InlineData("", "", "ushort", "\"a\"", "\"a\"")]
        [InlineData("", "", "uint", "\"a\"", "\"a\"")]
        [InlineData("", "", "ulong", "\"a\"", "\"a\"")]
        [InlineData("", "", "bool", "\"a\"", "\"a\"")]
        [InlineData("", "", "float", "\"a\"", "\"a\"")]
        [InlineData("", "", "double", "\"a\"", "\"a\"")]
        [InlineData("", "", "string", "true", "false")]
        [InlineData("<T>", "", "T", "\"min\"", "false")]
        [InlineData("", "<T>", "T", "\"min\"", "false")]
        public static async Task TestConstantExpectedIncompatibleConstantTypeErrorAsync(string classGeneric, string methodGeneric, string type, string badMinValue, string badMaxValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test{classGeneric}
{{
    public static void TestMethod{methodGeneric}([ConstantExpected({{|#0:Min = {badMinValue}|}})] {type} val) {{ }}
    public static void TestMethod2{methodGeneric}([ConstantExpected({{|#1:Min = {badMinValue}|}}, {{|#2:Max = {badMaxValue}|}})] {type} val) {{ }}
    public static void TestMethod3{methodGeneric}([ConstantExpected({{|#3:Max = {badMaxValue}|}})] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                        .WithLocation(0)
                        .WithArguments("Min", type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                        .WithLocation(1)
                        .WithArguments("Min", type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                        .WithLocation(2)
                        .WithArguments("Max", type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                        .WithLocation(3)
                        .WithArguments("Max", type));
        }

        [Theory]
        [InlineData("char", "'Z'", "'A'")]
        [InlineData("sbyte", "1", "0")]
        [InlineData("short", "1", "0")]
        [InlineData("int", "1", "0")]
        [InlineData("long", "1", "0")]
        [InlineData("byte", "1", "0")]
        [InlineData("ushort", "1", "0")]
        [InlineData("uint", "1", "0")]
        [InlineData("ulong", "1", "0")]
        [InlineData("float", "1", "0")]
        [InlineData("double", "1", "0")]
        public static async Task TestConstantExpectedInvertedConstantTypeErrorAsync(string type, string min, string max)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([{{|#0:ConstantExpected(Min = {min}, Max = {max})|}}] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvertedRangeRule)
                        .WithLocation(0));
        }

        [Theory]
        [InlineData("sbyte", "AEnum.Five", "AEnum.Two")]
        [InlineData("short", "AEnum.Five", "AEnum.Two")]
        [InlineData("int", "AEnum.Five", "AEnum.Two")]
        [InlineData("long", "AEnum.Five", "AEnum.Two")]
        [InlineData("byte", "AEnum.Five", "AEnum.Two")]
        [InlineData("ushort", "AEnum.Five", "AEnum.Two")]
        [InlineData("uint", "AEnum.Five", "AEnum.Two")]
        [InlineData("ulong", "AEnum.Five", "AEnum.Two")]
        public static async Task TestEnumConstantExpectedInvertedConstantTypeErrorAsync(string type, string min, string max)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([{{|#0:ConstantExpected(Min = {min}, Max = {max})|}}] {type} val) {{ }}
}}

public enum AEnum : {type}
{{
    Zero,
    One = 1,
    Two = 1 << 1,
    Three = 1 << 2,
    Four = 1 << 3,
    Five = 1 << 4
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvertedRangeRule)
                        .WithLocation(0));
        }

        [Theory]
        [InlineData("sbyte", sbyte.MinValue, sbyte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("short", short.MinValue, short.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("int", int.MinValue, int.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("long", long.MinValue, long.MaxValue, "ulong.MaxValue", "ulong.MaxValue", "false", "true")]
        [InlineData("byte", byte.MinValue, byte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("ushort", ushort.MinValue, ushort.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("uint", uint.MinValue, uint.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("ulong", ulong.MinValue, ulong.MaxValue, "long.MinValue", "-1", "false", "true")]
        [InlineData("float", float.MinValue, float.MaxValue, "double.MinValue", "double.MaxValue", "false", "true")]
        public static async Task TestConstantExpectedInvalidBoundsAsync(string type, object min, object max, string min1, string max1, string badMinValue, string badMaxValue)
        {
            string minString = min.ToString();
            string maxString = max.ToString();
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([ConstantExpected({{|#0:Min = {min1}|}})] {type} val) {{ }}
    public static void TestMethod2([ConstantExpected({{|#1:Min = {min1}|}}, {{|#2:Max = {max1}|}})] {type} val) {{ }}
    public static void TestMethod3([ConstantExpected({{|#3:Max = {max1}|}})] {type} val) {{ }}
    public static void TestMethod4([ConstantExpected({{|#4:Min = {badMinValue}|}}, {{|#5:Max = {max1}|}})] {type} val) {{ }}
    public static void TestMethod5([ConstantExpected({{|#6:Min = {min1}|}}, {{|#7:Max = {badMaxValue}|}})] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(0)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(1)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(2)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(3)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                    .WithLocation(4)
                    .WithArguments("Min", type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(5)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(6)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                    .WithLocation(7)
                    .WithArguments("Max", type));
        }

        [Theory]
        [InlineData("sbyte", sbyte.MinValue, sbyte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("short", short.MinValue, short.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("int", int.MinValue, int.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("long", long.MinValue, long.MaxValue, "ulong.MaxValue", "ulong.MaxValue", "false", "true")]
        [InlineData("byte", byte.MinValue, byte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("ushort", ushort.MinValue, ushort.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("uint", uint.MinValue, uint.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [InlineData("ulong", ulong.MinValue, ulong.MaxValue, "long.MinValue", "-1", "false", "true")]
        public static async Task TestEnumConstantExpectedInvalidBoundsAsync(string type, object min, object max, string min1, string max1, string badMinValue, string badMaxValue)
        {
            string minString = min.ToString();
            string maxString = max.ToString();
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([ConstantExpected({{|#0:Min = {min1}|}})] AEnum val) {{ }}
    public static void TestMethod2([ConstantExpected({{|#1:Min = {min1}|}}, {{|#2:Max = {max1}|}})] AEnum val) {{ }}
    public static void TestMethod3([ConstantExpected({{|#3:Max = {max1}|}})] AEnum val) {{ }}
    public static void TestMethod4([ConstantExpected({{|#4:Min = {badMinValue}|}}, {{|#5:Max = {max1}|}})] AEnum val) {{ }}
    public static void TestMethod5([ConstantExpected({{|#6:Min = {min1}|}}, {{|#7:Max = {badMaxValue}|}})] AEnum val) {{ }}
}}

public enum AEnum : {type}
{{
    Zero,
    One = 1,
    Two = 1 << 1,
    Three = 1 << 2,
    Four = 1 << 3,
    Five = 1 << 4
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(0)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(1)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(2)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(3)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                    .WithLocation(4)
                    .WithArguments("Min", "AEnum"),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(5)
                    .WithArguments("Max", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.InvalidBoundsRule)
                    .WithLocation(6)
                    .WithArguments("Min", minString, maxString),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1856.IncompatibleConstantTypeRule)
                    .WithLocation(7)
                    .WithArguments("Max", "AEnum"));
        }

        [Theory]
        [InlineData("char", "'A'", "'Z'", "'A'", "(char)('A'+'\\u0001')")]
        [InlineData("sbyte", "10", "20", "10", "2*5")]
        [InlineData("short", "10", "20", "10", "2*5")]
        [InlineData("int", "10", "20", "10", "2*5")]
        [InlineData("long", "10", "20", "10", "2*5")]
        [InlineData("byte", "10", "20", "10", "2*5")]
        [InlineData("ushort", "10", "20", "10", "2*5")]
        [InlineData("uint", "10", "20", "10", "2*5")]
        [InlineData("ulong", "10", "20", "10", "2*5")]
        [InlineData("float", "10", "20", "10", "2*5")]
        [InlineData("double", "10", "20", "10", "2*5")]
        [InlineData("bool", "true", "true", "true", "!false")]
        [InlineData("string", "null", "null", "\"true\"", "\"false\"")]
        public static async Task TestArgumentConstantAsync(string type, string minValue, string maxValue, string value, string expression)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod()
    {{
        TestMethodWithConstant({value});
        TestMethodWithConstant({expression});
        TestMethodWithConstrainedConstant({value});
        TestMethodWithConstrainedConstant({expression});
        TestMethodGeneric<{type}>({value});
        TestMethodGeneric<{type}>({expression});
        GenericClass<{type}>.TestMethodGeneric({value});
        GenericClass<{type}>.TestMethodGeneric({expression});
    }}
    public static void TestMethodWithConstant([ConstantExpected] {type} val) {{ }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {minValue}, Max = {maxValue})] {type} val) {{ }}
    public static void TestMethodGeneric<T>([ConstantExpected] T val) {{ }}
    
    public static class GenericClass<T>
    {{
        public static void TestMethodGeneric([ConstantExpected] T val) {{ }}
    }}
}}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("sbyte", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("short", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("int", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("long", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("byte", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("ushort", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("uint", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [InlineData("ulong", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        public static async Task TestEnumArgumentConstantAsync(string type, string minValue, string maxValue, string value, string expression)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod()
    {{
        TestMethodWithConstant({value});
        TestMethodWithConstant({expression});
        TestMethodWithConstrainedConstant({value});
        TestMethodWithConstrainedConstant({expression});
        TestMethodGeneric<AEnum>({value});
        TestMethodGeneric<AEnum>({expression});
        GenericClass<AEnum>.TestMethodGeneric({value});
        GenericClass<AEnum>.TestMethodGeneric({expression});
    }}
    public static void TestMethodWithConstant([ConstantExpected] AEnum val) {{ }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {minValue}, Max = {maxValue})] AEnum val) {{ }}
    public static void TestMethodGeneric<T>([ConstantExpected] T val) {{ }}
    
    public static class GenericClass<T>
    {{
        public static void TestMethodGeneric([ConstantExpected] T val) {{ }}
    }}
}}

public enum AEnum : {type}
{{
    Zero,
    One = 1,
    Two = 1 << 1,
    Three = 1 << 2,
    Four = 1 << 3,
    Five = 1 << 4
}}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("char")]
        [InlineData("sbyte")]
        [InlineData("short")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("byte")]
        [InlineData("ushort")]
        [InlineData("uint")]
        [InlineData("ulong")]
        [InlineData("float")]
        [InlineData("double")]
        [InlineData("string")]
        public static async Task TestArgumentNotConstantAsync(string type)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod({type} nonConstant)
    {{
        TestMethodWithConstant({{|#0:nonConstant|}});
        TestMethodGeneric<{type}>({{|#1:nonConstant|}});
        GenenricClass<{type}>.TestMethodGeneric({{|#2:nonConstant|}});
    }}
    public static void TestMethodWithConstant([ConstantExpected] {type} val) {{ }}
    public static void TestMethodGeneric<T>([ConstantExpected] T val) {{ }}
    
    public static class GenenricClass<T>
    {{
        public static void TestMethodGeneric([ConstantExpected] T val) {{ }}
    }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(0),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(1),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(2));
        }

        [Theory]
        [InlineData("sbyte")]
        [InlineData("short")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("byte")]
        [InlineData("ushort")]
        [InlineData("uint")]
        [InlineData("ulong")]
        public static async Task TestEnumArgumentNotConstantAsync(string type)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod(AEnum nonConstant)
    {{
        TestMethodWithConstant({{|#0:nonConstant|}});
        TestMethodGeneric<AEnum>({{|#1:nonConstant|}});
        GenenricClass<AEnum>.TestMethodGeneric({{|#2:nonConstant|}});
    }}
    public static void TestMethodWithConstant([ConstantExpected] AEnum val) {{ }}
    public static void TestMethodGeneric<T>([ConstantExpected] T val) {{ }}
    
    public static class GenenricClass<T>
    {{
        public static void TestMethodGeneric([ConstantExpected] T val) {{ }}
    }}
}}

public enum AEnum : {type}
{{
    One,
    Two
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(0),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(1),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantNotConstantRule)
                        .WithLocation(2));
        }

        [Theory]
        [InlineData("char", "(char)(object)10.5")]
        [InlineData("string", "(string)(object)20")]
        public static async Task TestArgumentInvalidConstantAsync(string type, string constant)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod()
    {{
        TestMethodWithConstant({{|#0:{constant}|}});
        TestMethodGeneric<{type}>({{|#1:{constant}|}});
        GenericClass<{type}>.TestMethodGeneric({{|#2:{constant}|}});
    }}
    public static void TestMethodWithConstant([ConstantExpected] {type} val) {{ }}
    public static void TestMethodGeneric<T>([ConstantExpected] T val) {{ }}
    
    public static class GenericClass<T>
    {{
        public static void TestMethodGeneric([ConstantExpected] T val) {{ }}
    }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                    .WithLocation(0)
                    .WithArguments(type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                    .WithLocation(1)
                    .WithArguments(type),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                    .WithLocation(2)
                    .WithArguments(type));
        }

        [Theory]
        [InlineData("char", "'B'", "'C'", "'D'")]
        [InlineData("sbyte", "3", "4", "5")]
        [InlineData("short", "3", "4", "5")]
        [InlineData("int", "3", "4", "5")]
        [InlineData("long", "3", "4", "5")]
        [InlineData("byte", "3", "4", "5")]
        [InlineData("ushort", "3", "4", "5")]
        [InlineData("uint", "3", "4", "5")]
        [InlineData("ulong", "3", "4", "5")]
        [InlineData("float", "3", "4", "5")]
        [InlineData("double", "3", "4", "5")]
        public static async Task TestArgumentOutOfBoundsConstantAsync(string type, string min, string max, string testValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod()
    {{
        TestMethodWithConstant({{|#0:{testValue}|}});
    }}
    public static void TestMethodWithConstant([ConstantExpected(Min = {min}, Max = {max})] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantOutOfBoundsRule)
                        .WithLocation(0)
                        .WithArguments(min.Trim('\''), max.Trim('\'')));
        }

        [Theory]
        [InlineData("sbyte", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("short", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("int", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("long", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("byte", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("ushort", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("uint", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [InlineData("ulong", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        public static async Task TestEnumArgumentOutOfBoundsConstantAsync(string type, string min, string max, string testValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod()
    {{
        TestMethodWithConstant({{|#0:{testValue}|}});
    }}
    public static void TestMethodWithConstant([ConstantExpected(Min = {min}, Max = {max})] AEnum val) {{ }}
}}

public enum AEnum : {type}
{{
    Zero,
    One,
    Two,
    Three,
    Four,
    Five
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantOutOfBoundsRule)
                        .WithLocation(0)
                        .WithArguments("3", "4"));
        }

        [Fact]
        public static async Task TestArgumentInvalidGenericTypeParameterConstantAsync()
        {
            string csInput = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{
    public static void TestMethod(int[] nonConstant)
    {
        TestMethodGeneric<int[]>(nonConstant); // ignore scenario
        GenericClass<int[]>.TestMethodGeneric(nonConstant); // ignore scenario
    }
    public static void TestMethodGeneric<T>([ConstantExpected] T val) { }
    
    public static class GenericClass<T>
    {
        public static void TestMethodGeneric([ConstantExpected] T val) { }
    }
}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("char", "'B'", "'C'")]
        [InlineData("sbyte", "3", "4")]
        [InlineData("short", "3", "4")]
        [InlineData("int", "3", "4")]
        [InlineData("long", "3", "4")]
        [InlineData("byte", "3", "4")]
        [InlineData("ushort", "3", "4")]
        [InlineData("uint", "3", "4")]
        [InlineData("ulong", "3", "4")]
        [InlineData("float", "3", "4")]
        [InlineData("double", "3", "4")]
        [InlineData("bool", "false", "false")]
        public static async Task TestConstantCompositionAsync(string type, string min, string max)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([ConstantExpected] {type} constant)
    {{
        TestMethodWithConstant(constant);
    }}
    public static void TestMethodWithConstant([ConstantExpected] {type} val) {{ }}
    public static void TestMethodConstrained([ConstantExpected(Min = {min}, Max = {max})] {type} constant)
    {{
        TestMethodWithConstant(constant);
        TestMethodWithConstrainedConstant(constant);
    }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {min}, Max = {max})] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("sbyte", "AEnum.Two", "AEnum.Three")]
        [InlineData("short", "AEnum.Two", "AEnum.Three")]
        [InlineData("int", "AEnum.Two", "AEnum.Three")]
        [InlineData("long", "AEnum.Two", "AEnum.Three")]
        [InlineData("byte", "AEnum.Two", "AEnum.Three")]
        [InlineData("ushort", "AEnum.Two", "AEnum.Three")]
        [InlineData("uint", "AEnum.Two", "AEnum.Three")]
        [InlineData("ulong", "AEnum.Two", "AEnum.Three")]
        public static async Task TestEnumConstantCompositionAsync(string type, string min, string max)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethod([ConstantExpected] AEnum constant)
    {{
        TestMethodWithConstant(constant);
    }}
    public static void TestMethodWithConstant([ConstantExpected] AEnum val) {{ }}
    public static void TestMethodConstrained([ConstantExpected(Min = {min}, Max = {max})] AEnum constant)
    {{
        TestMethodWithConstant(constant);
        TestMethodWithConstrainedConstant(constant);
    }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {min}, Max = {max})] AEnum val) {{ }}
}}

public enum AEnum : {type}
{{
    Zero,
    One = 1,
    Two = 1 << 1,
    Three = 1 << 2,
    Four = 1 << 3,
    Five = 1 << 4
}}
";
            await TestCSAsync(csInput);
        }

        [Fact]
        public static async Task TestConstantCompositionStringAsync()
        {
            string csInput = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{
    public static void TestMethod([ConstantExpected] string constant)
    {
        TestMethodWithConstant(constant);
    }
    public static void TestMethodWithConstant([ConstantExpected] string val) { }
}
";
            await TestCSAsync(csInput);
        }

        [Theory]
        [InlineData("char", "'B'", "'C'", "'D'", 'B', 'C')]
        [InlineData("sbyte", "3", "4", "5", 3, 4)]
        [InlineData("short", "3", "4", "5", 3, 4)]
        [InlineData("int", "3", "4", "5", 3, 4)]
        [InlineData("long", "3", "4", "5", 3, 4)]
        [InlineData("byte", "3", "4", "5", 3, 4)]
        [InlineData("ushort", "3", "4", "5", 3, 4)]
        [InlineData("uint", "3", "4", "5", 3, 4)]
        [InlineData("ulong", "3", "4", "5", 3, 4)]
        [InlineData("float", "3", "4", "5", 3, 4)]
        [InlineData("double", "3", "4", "5", 3, 4)]
        [InlineData("bool", "false", "false", "true", false, false)]
        public static async Task TestConstantCompositionOutOfBoundsAsync(string type, string min, string max, string outOfBoundMax, object minValue, object maxValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethodConstrained([ConstantExpected(Min = {min}, Max = {outOfBoundMax})] {type} constant)
    {{
        TestMethodWithConstrainedConstant({{|#0:constant|}});
    }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {min}, Max = {max})] {type} val) {{ }}
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantOutOfBoundsRule)
                        .WithLocation(0)
                        .WithArguments(minValue.ToString(), maxValue.ToString()));
        }

        [Theory]
        [InlineData("sbyte", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("short", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("int", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("long", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("byte", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("ushort", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("uint", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [InlineData("ulong", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        public static async Task TestEnumConstantCompositionOutOfBoundsAsync(string type, string min, string max, string outOfBoundMax, object minValue, object maxValue)
        {
            string csInput = @$"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{{
    public static void TestMethodConstrained([ConstantExpected(Min = {min}, Max = {outOfBoundMax})] AEnum constant)
    {{
        TestMethodWithConstrainedConstant({{|#0:constant|}});
    }}
    public static void TestMethodWithConstrainedConstant([ConstantExpected(Min = {min}, Max = {max})] AEnum val) {{ }}
}}

public enum AEnum : {type}
{{
    Zero,
    One = 1,
    Two = 1 << 1,
    Three = 1 << 2,
    Four = 1 << 3,
    Five = 1 << 4
}}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantOutOfBoundsRule)
                        .WithLocation(0)
                        .WithArguments(minValue.ToString(), maxValue.ToString()));
        }

        [Fact]
        public static async Task TestConstantCompositionNotSameTypeAsync()
        {
            string csInput = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

public class Test
{
    public static void TestMethod([ConstantExpected] long constant)
    {
        TestMethodWithConstant({|#0:(int)constant|});
        TestMethodWithStringConstant({|#1:(string)(object)constant|});
    }
    public static void TestMethod([ConstantExpected] short constant)
    {
        TestMethodWithConstant({|#2:constant|});
    }
    public static void TestMethodWithConstant([ConstantExpected] int val) { }
    public static void TestMethodWithStringConstant([ConstantExpected] string val) { }
}
";
            await TestCSAsync(csInput,
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                        .WithLocation(0)
                        .WithArguments("int"),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                    .WithLocation(1)
                    .WithArguments("string"),
                VerifyCS.Diagnostic(ConstantExpectedAnalyzer.CA1857.ConstantInvalidConstantRule)
                        .WithLocation(2)
                        .WithArguments("int"));
        }

        private static async Task TestCSAsync(string source, params DiagnosticResult[] diagnosticResults)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
            };
            test.ExpectedDiagnostics.AddRange(diagnosticResults);
            await test.RunAsync();
        }

        private static async Task TestCSMissingAttributeAsync(string source, params DiagnosticResult[] diagnosticResults)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
            };
            test.TestState.Sources.Add(SimilarAttributeSource);
            test.ExpectedDiagnostics.AddRange(diagnosticResults);
            await test.RunAsync();
        }

        private const string SimilarAttributeSource = @"#nullable enable
using System;
namespace Similar
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ConstantExpectedAttribute : Attribute
    {
        public object? Min { get; set; }
        public object? Max { get; set; }
    }
}";
    }
}
