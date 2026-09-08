// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpConstantExpectedAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public sealed class ConstantExpectedTests
    {
        [TestMethod]
        [DataRow("char", "char.MinValue", "char.MaxValue")]
        [DataRow("sbyte", "sbyte.MinValue", "sbyte.MaxValue")]
        [DataRow("short", "short.MinValue", "short.MaxValue")]
        [DataRow("int", "int.MinValue", "int.MaxValue")]
        [DataRow("long", "long.MinValue", "long.MaxValue")]
        [DataRow("byte", "byte.MinValue", "byte.MaxValue")]
        [DataRow("ushort", "ushort.MinValue", "ushort.MaxValue")]
        [DataRow("uint", "uint.MinValue", "uint.MaxValue")]
        [DataRow("ulong", "ulong.MinValue", "ulong.MaxValue")]
        [DataRow("bool", "false", "true")]
        [DataRow("float", "float.MinValue", "float.MaxValue")]
        [DataRow("double", "double.MinValue", "double.MaxValue")]
        public async Task TestConstantExpectedSupportedUnmanagedTypesAsync(string type, string minValue, string maxValue)
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

        [TestMethod]
        [DataRow("sbyte", "sbyte.MinValue", "sbyte.MaxValue")]
        [DataRow("short", "short.MinValue", "short.MaxValue")]
        [DataRow("int", "int.MinValue", "int.MaxValue")]
        [DataRow("long", "long.MinValue", "long.MaxValue")]
        [DataRow("byte", "byte.MinValue", "byte.MaxValue")]
        [DataRow("ushort", "ushort.MinValue", "ushort.MaxValue")]
        [DataRow("uint", "uint.MinValue", "uint.MaxValue")]
        [DataRow("ulong", "ulong.MinValue", "ulong.MaxValue")]
        public async Task TestConstantExpectedSupportedEnumTypesAsync(string type, string minValue, string maxValue)
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

        [TestMethod]
        public async Task TestConstantExpectedSupportedComplexTypesAsync()
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

        [TestMethod]
        [DataRow("char")]
        [DataRow("sbyte")]
        [DataRow("short")]
        [DataRow("int")]
        [DataRow("long")]
        [DataRow("byte")]
        [DataRow("ushort")]
        [DataRow("uint")]
        [DataRow("ulong")]
        [DataRow("float")]
        [DataRow("double")]
        [DataRow("bool")]
        [DataRow("string")]
        public async Task TestConstantExpectedSupportedComplex2TypesAsync(string type)
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

        [TestMethod]
        [DataRow("char")]
        [DataRow("sbyte")]
        [DataRow("short")]
        [DataRow("int")]
        [DataRow("long")]
        [DataRow("byte")]
        [DataRow("ushort")]
        [DataRow("uint")]
        [DataRow("ulong")]
        [DataRow("float")]
        [DataRow("double")]
        [DataRow("bool")]
        [DataRow("string")]
        public async Task TestMissingConstantExpectedSupportedComplex2TypesAsync(string type)
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

        [TestMethod]
        public async Task TestConstantExpectedSupportedComplex3TypesAsync()
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

        [TestMethod]
        [DataRow("", "", "nint", "nint")]
        [DataRow("", "", "nuint", "nuint")]
        [DataRow("", "", "object", "object")]
        [DataRow("", "", "Test", "Test")]
        [DataRow("", "", "Guid", "System.Guid")]
        [DataRow("", "", "decimal", "decimal")]
        [DataRow("", "", "byte[]", "byte[]")]
        [DataRow("", "", "(int, long)", "(int, long)")]
        [DataRow("<T>", "", "T[]", "T[]")]
        [DataRow("", "<T>", "T[]", "T[]")]
        public async Task TestConstantExpectedUnsupportedTypesAsync(string classGeneric, string methodGeneric, string type, string diagnosticType)
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

        [TestMethod]
        [DataRow("object")]
        [DataRow("Test")]
        [DataRow("Guid")]
        [DataRow("decimal")]
        [DataRow("byte[]")]
        [DataRow("(int, long)")]
        public async Task TestConstantExpectedUnsupportedIgnoredComplexTypesAsync(string type)
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

        [TestMethod]
        [DataRow("", "", "char", "\"a\"", "\"a\"")]
        [DataRow("", "", "sbyte", "\"a\"", "\"a\"")]
        [DataRow("", "", "short", "\"a\"", "\"a\"")]
        [DataRow("", "", "int", "\"a\"", "\"a\"")]
        [DataRow("", "", "long", "\"a\"", "\"a\"")]
        [DataRow("", "", "byte", "\"a\"", "\"a\"")]
        [DataRow("", "", "ushort", "\"a\"", "\"a\"")]
        [DataRow("", "", "uint", "\"a\"", "\"a\"")]
        [DataRow("", "", "ulong", "\"a\"", "\"a\"")]
        [DataRow("", "", "bool", "\"a\"", "\"a\"")]
        [DataRow("", "", "float", "\"a\"", "\"a\"")]
        [DataRow("", "", "double", "\"a\"", "\"a\"")]
        [DataRow("", "", "string", "true", "false")]
        [DataRow("<T>", "", "T", "\"min\"", "false")]
        [DataRow("", "<T>", "T", "\"min\"", "false")]
        public async Task TestConstantExpectedIncompatibleConstantTypeErrorAsync(string classGeneric, string methodGeneric, string type, string badMinValue, string badMaxValue)
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

        [TestMethod]
        [DataRow("char", "'Z'", "'A'")]
        [DataRow("sbyte", "1", "0")]
        [DataRow("short", "1", "0")]
        [DataRow("int", "1", "0")]
        [DataRow("long", "1", "0")]
        [DataRow("byte", "1", "0")]
        [DataRow("ushort", "1", "0")]
        [DataRow("uint", "1", "0")]
        [DataRow("ulong", "1", "0")]
        [DataRow("float", "1", "0")]
        [DataRow("double", "1", "0")]
        public async Task TestConstantExpectedInvertedConstantTypeErrorAsync(string type, string min, string max)
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

        [TestMethod]
        [DataRow("sbyte", "AEnum.Five", "AEnum.Two")]
        [DataRow("short", "AEnum.Five", "AEnum.Two")]
        [DataRow("int", "AEnum.Five", "AEnum.Two")]
        [DataRow("long", "AEnum.Five", "AEnum.Two")]
        [DataRow("byte", "AEnum.Five", "AEnum.Two")]
        [DataRow("ushort", "AEnum.Five", "AEnum.Two")]
        [DataRow("uint", "AEnum.Five", "AEnum.Two")]
        [DataRow("ulong", "AEnum.Five", "AEnum.Two")]
        public async Task TestEnumConstantExpectedInvertedConstantTypeErrorAsync(string type, string min, string max)
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

        [TestMethod]
        [DataRow("sbyte", sbyte.MinValue, sbyte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("short", short.MinValue, short.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("int", int.MinValue, int.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("long", long.MinValue, long.MaxValue, "ulong.MaxValue", "ulong.MaxValue", "false", "true")]
        [DataRow("byte", byte.MinValue, byte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("ushort", ushort.MinValue, ushort.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("uint", uint.MinValue, uint.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("ulong", ulong.MinValue, ulong.MaxValue, "long.MinValue", "-1", "false", "true")]
        [DataRow("float", float.MinValue, float.MaxValue, "double.MinValue", "double.MaxValue", "false", "true")]
        public async Task TestConstantExpectedInvalidBoundsAsync(string type, object min, object max, string min1, string max1, string badMinValue, string badMaxValue)
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

        [TestMethod]
        [DataRow("sbyte", sbyte.MinValue, sbyte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("short", short.MinValue, short.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("int", int.MinValue, int.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("long", long.MinValue, long.MaxValue, "ulong.MaxValue", "ulong.MaxValue", "false", "true")]
        [DataRow("byte", byte.MinValue, byte.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("ushort", ushort.MinValue, ushort.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("uint", uint.MinValue, uint.MaxValue, "long.MinValue", "long.MaxValue", "false", "true")]
        [DataRow("ulong", ulong.MinValue, ulong.MaxValue, "long.MinValue", "-1", "false", "true")]
        public async Task TestEnumConstantExpectedInvalidBoundsAsync(string type, object min, object max, string min1, string max1, string badMinValue, string badMaxValue)
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

        [TestMethod]
        [DataRow("char", "'A'", "'Z'", "'A'", "(char)('A'+'\\u0001')")]
        [DataRow("sbyte", "10", "20", "10", "2*5")]
        [DataRow("short", "10", "20", "10", "2*5")]
        [DataRow("int", "10", "20", "10", "2*5")]
        [DataRow("long", "10", "20", "10", "2*5")]
        [DataRow("byte", "10", "20", "10", "2*5")]
        [DataRow("ushort", "10", "20", "10", "2*5")]
        [DataRow("uint", "10", "20", "10", "2*5")]
        [DataRow("ulong", "10", "20", "10", "2*5")]
        [DataRow("float", "10", "20", "10", "2*5")]
        [DataRow("double", "10", "20", "10", "2*5")]
        [DataRow("bool", "true", "true", "true", "!false")]
        [DataRow("string", "null", "null", "\"true\"", "\"false\"")]
        public async Task TestArgumentConstantAsync(string type, string minValue, string maxValue, string value, string expression)
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

        [TestMethod]
        [DataRow("sbyte", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("short", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("int", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("long", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("byte", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("ushort", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("uint", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        [DataRow("ulong", "AEnum.One", "AEnum.Five", "AEnum.Two", "AEnum.One | AEnum.Two")]
        public async Task TestEnumArgumentConstantAsync(string type, string minValue, string maxValue, string value, string expression)
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

        [TestMethod]
        [DataRow("char")]
        [DataRow("sbyte")]
        [DataRow("short")]
        [DataRow("int")]
        [DataRow("long")]
        [DataRow("byte")]
        [DataRow("ushort")]
        [DataRow("uint")]
        [DataRow("ulong")]
        [DataRow("float")]
        [DataRow("double")]
        [DataRow("string")]
        public async Task TestArgumentNotConstantAsync(string type)
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

        [TestMethod]
        [DataRow("sbyte")]
        [DataRow("short")]
        [DataRow("int")]
        [DataRow("long")]
        [DataRow("byte")]
        [DataRow("ushort")]
        [DataRow("uint")]
        [DataRow("ulong")]
        public async Task TestEnumArgumentNotConstantAsync(string type)
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

        [TestMethod]
        [DataRow("char", "(char)(object)10.5")]
        [DataRow("string", "(string)(object)20")]
        public async Task TestArgumentInvalidConstantAsync(string type, string constant)
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

        [TestMethod]
        [DataRow("char", "'B'", "'C'", "'D'")]
        [DataRow("sbyte", "3", "4", "5")]
        [DataRow("short", "3", "4", "5")]
        [DataRow("int", "3", "4", "5")]
        [DataRow("long", "3", "4", "5")]
        [DataRow("byte", "3", "4", "5")]
        [DataRow("ushort", "3", "4", "5")]
        [DataRow("uint", "3", "4", "5")]
        [DataRow("ulong", "3", "4", "5")]
        [DataRow("float", "3", "4", "5")]
        [DataRow("double", "3", "4", "5")]
        public async Task TestArgumentOutOfBoundsConstantAsync(string type, string min, string max, string testValue)
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

        [TestMethod]
        [DataRow("sbyte", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("short", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("int", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("long", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("byte", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("ushort", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("uint", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        [DataRow("ulong", "AEnum.Three", "AEnum.Four", "AEnum.Five")]
        public async Task TestEnumArgumentOutOfBoundsConstantAsync(string type, string min, string max, string testValue)
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

        [TestMethod]
        public async Task TestArgumentInvalidGenericTypeParameterConstantAsync()
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

        [TestMethod]
        [DataRow("char", "'B'", "'C'")]
        [DataRow("sbyte", "3", "4")]
        [DataRow("short", "3", "4")]
        [DataRow("int", "3", "4")]
        [DataRow("long", "3", "4")]
        [DataRow("byte", "3", "4")]
        [DataRow("ushort", "3", "4")]
        [DataRow("uint", "3", "4")]
        [DataRow("ulong", "3", "4")]
        [DataRow("float", "3", "4")]
        [DataRow("double", "3", "4")]
        [DataRow("bool", "false", "false")]
        public async Task TestConstantCompositionAsync(string type, string min, string max)
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

        [TestMethod]
        [DataRow("sbyte", "AEnum.Two", "AEnum.Three")]
        [DataRow("short", "AEnum.Two", "AEnum.Three")]
        [DataRow("int", "AEnum.Two", "AEnum.Three")]
        [DataRow("long", "AEnum.Two", "AEnum.Three")]
        [DataRow("byte", "AEnum.Two", "AEnum.Three")]
        [DataRow("ushort", "AEnum.Two", "AEnum.Three")]
        [DataRow("uint", "AEnum.Two", "AEnum.Three")]
        [DataRow("ulong", "AEnum.Two", "AEnum.Three")]
        public async Task TestEnumConstantCompositionAsync(string type, string min, string max)
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

        [TestMethod]
        public async Task TestConstantCompositionStringAsync()
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

        [TestMethod]
        [DataRow("char", "'B'", "'C'", "'D'", 'B', 'C')]
        [DataRow("sbyte", "3", "4", "5", 3, 4)]
        [DataRow("short", "3", "4", "5", 3, 4)]
        [DataRow("int", "3", "4", "5", 3, 4)]
        [DataRow("long", "3", "4", "5", 3, 4)]
        [DataRow("byte", "3", "4", "5", 3, 4)]
        [DataRow("ushort", "3", "4", "5", 3, 4)]
        [DataRow("uint", "3", "4", "5", 3, 4)]
        [DataRow("ulong", "3", "4", "5", 3, 4)]
        [DataRow("float", "3", "4", "5", 3, 4)]
        [DataRow("double", "3", "4", "5", 3, 4)]
        [DataRow("bool", "false", "false", "true", false, false)]
        public async Task TestConstantCompositionOutOfBoundsAsync(string type, string min, string max, string outOfBoundMax, object minValue, object maxValue)
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

        [TestMethod]
        [DataRow("sbyte", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("short", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("int", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("long", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("byte", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("ushort", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("uint", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        [DataRow("ulong", "AEnum.Two", "AEnum.Three", "AEnum.Four", 2, 4)]
        public async Task TestEnumConstantCompositionOutOfBoundsAsync(string type, string min, string max, string outOfBoundMax, object minValue, object maxValue)
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

        [TestMethod]
        public async Task TestConstantCompositionNotSameTypeAsync()
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
            await test.RunAsync(CancellationToken.None);
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
            await test.RunAsync(CancellationToken.None);
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
