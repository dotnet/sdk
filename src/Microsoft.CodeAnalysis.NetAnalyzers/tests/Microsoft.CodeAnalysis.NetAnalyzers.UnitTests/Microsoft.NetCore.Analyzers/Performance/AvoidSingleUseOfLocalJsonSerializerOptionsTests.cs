// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.AvoidSingleUseOfLocalJsonSerializerOptions,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.AvoidSingleUseOfLocalJsonSerializerOptions,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class AvoidSingleUseOfLocalJsonSerializerOptionsTests
    {
        #region Diagnostic Tests

        [Fact]
        public Task CS_UseNewOptionsAsArgument()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Text.Json;

                internal class Program
                {
                    static void Main(string[] args)
                    {
                        string json = JsonSerializer.Serialize(args, {|CA1869:new JsonSerializerOptions { AllowTrailingCommas = true }|});
                        Console.WriteLine(json);
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Text.Json;

                internal class Program
                {
                    static void Main(string[] args)
                    {
                        JsonSerializerOptions options = {|CA1869:new JsonSerializerOptions()|};
                        options.AllowTrailingCommas = true;

                        string json = JsonSerializer.Serialize(args, options);
                        Console.WriteLine(json);
                    }
                }
                """);

        [Fact]
        public Task VB_UseNewOptionsAsArgument()
            => VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Text.Json

                Module Program
                    Sub Main(args As String())
                        Dim json = JsonSerializer.Serialize(args, {|CA1869:New JsonSerializerOptions With {.AllowTrailingCommas = True}|})
                        Console.WriteLine(json)
                    End Sub
                End Module
                """);

        [Fact]
        public Task VB_UseNewLocalOptionsAsArgument()
            => VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Text.Json

                Module Program
                    Sub Main(args As String())
                        Dim options = {|CA1869:New JsonSerializerOptions()|}
                        options.AllowTrailingCommas = True

                        Dim json = JsonSerializer.Serialize(args, options)
                        Console.WriteLine(json)
                    End Sub
                End Module
                """);

        [Theory]
        [InlineData("{|CA1869:new JsonSerializerOptions()|}")]
        [InlineData("{|CA1869:new JsonSerializerOptions{}|}")]
        [InlineData("({|CA1869:new JsonSerializerOptions()|})")]
        [InlineData("(({|CA1869:new JsonSerializerOptions()|}))")]
        [InlineData("1 == 1 ? {|CA1869:new JsonSerializerOptions()|} : null")]
        [InlineData("1 == 1 ? null : 2 == 2 ? null : {|CA1869:new JsonSerializerOptions()|}")]
        public Task CS_UseNewOptionsAsArgument_Variants(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        return JsonSerializer.Serialize(value, {{expression}});
                    }
                }
                """);

        [Theory]
        [InlineData("{|CA1869:new JsonSerializerOptions()|}")]
        [InlineData("{|CA1869:new JsonSerializerOptions{}|}")]
        [InlineData("({|CA1869:new JsonSerializerOptions()|})")]
        [InlineData("(({|CA1869:new JsonSerializerOptions()|}))")]
        [InlineData("1 == 1 ? {|CA1869:new JsonSerializerOptions()|} : null")]
        [InlineData("1 == 1 ? null : 2 == 2 ? null : {|CA1869:new JsonSerializerOptions()|}")]
        public Task CS_UseNewLocalOptionsAsArgument_Variants(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        var options = {{expression}};
                        return JsonSerializer.Serialize(value, options);
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_Assignment()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt;
                        opt = {|CA1869:new JsonSerializerOptions()|};

                        return JsonSerializer.Serialize(value, opt);
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_SecondLocalReference()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                        _ = opt;

                        return JsonSerializer.Serialize(value, opt);
                    }
                }
                """);

        [Fact] // this could be better handled with data flow analysis.
        public Task CS_UseNewLocalOptionsAsArgument_OverwriteLocal()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static JsonSerializerOptions s_options;

                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                        opt = s_options;

                        return JsonSerializer.Serialize(value, opt);
                    }
                }
                """);

        [Theory]
        [InlineData("opt1")]
        [InlineData("opt2")]
        public Task CS_UseNewLocalOptionsAsArgument_MultiAssignment(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt1, opt2;
                        opt1 = opt2 = {|CA1869:new JsonSerializerOptions()|};

                        return JsonSerializer.Serialize(value, {{expression}});
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_Delegate()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Text.Json;

                class Program
                {
                    static Action Serialize<T>(T value)
                    {
                        Action lambda = () =>
                        {
                            JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                            JsonSerializer.Serialize(value, opt);
                        };
                        return lambda;
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_LocalFunction()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        return LocalFunc();

                        string LocalFunc()
                        {
                            JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                            return JsonSerializer.Serialize(value, opt);
                        }
                    }
                }
                """);

        [Theory]
        [InlineData("""
            for (int i = 0; i < values.Length; i++)
            {
                JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                concatJson += JsonSerializer.Serialize(values[i], opt);
            }
            """)]
        [InlineData("""
            foreach (T value in values)
            {
                JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                concatJson += JsonSerializer.Serialize(value, opt);
            }
            """)]
        [InlineData("""
            if (values.Length == 0) 
                return concatJson;

            int i = 0;
            do
            {
                JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                concatJson += JsonSerializer.Serialize(values[i++], opt);
            }
            while (i < values.Length);
            """)]
        [InlineData("""
            int i = 0;
            while (i < values.Length)
            {
                JsonSerializerOptions opt = {|CA1869:new JsonSerializerOptions()|};
                concatJson += JsonSerializer.Serialize(values[i++], opt);
            }
            """)]
        public Task CS_UseNewLocalOptionsAsArgument_JsonSerializerOptions_OnLoop(string loop)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T[] values)
                    {
                        string concatJson = "";

                        {{loop}}

                        return concatJson;
                    }
                }
                """);

        [Theory]
        [InlineData("""
            For i = 0 To values.Length
                Dim opt = {|CA1869:New JsonSerializerOptions()|}
                concatJson += JsonSerializer.Serialize(values(i), opt)
            Next
            """)]
        [InlineData("""
            For Each value In values
                Dim opt = {|CA1869:New JsonSerializerOptions()|}
                concatJson += JsonSerializer.Serialize(value, opt)
            Next
            """)]
        [InlineData("""
            Dim i = 0
            Do While i < values.Length
                Dim opt = {|CA1869:New JsonSerializerOptions()|}
                concatJson += JsonSerializer.Serialize(values(i), opt)
                i = i + 1
            Loop
            """)]
        [InlineData("""
            Dim i = 0
            Do
                Dim opt = {|CA1869:New JsonSerializerOptions()|}
                concatJson += JsonSerializer.Serialize(values(i), opt)
                i = i + 1
            Loop While i < values.Length
            """)]
        public Task VB_UseNewLocalOptionsAsArgument_JsonSerializerOptions_OnLoop(string loop)
            => VerifyVB.VerifyAnalyzerAsync($$"""
                Imports System.Text.Json

                Class Program
                    Shared Function Serialize(Of T)(values As T()) As String
                        Dim concatJson = ""

                        {{loop}}

                        return concatJson
                    End Function
                End Class
                """);
        #endregion

        #region No Diagnostic Tests
        [Fact]
        public Task CS_UseNewOptionsAsArgument_NonSerializerMethod_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        return MyCustomSerializeMethod(value, new JsonSerializerOptions());
                    }

                    static string MyCustomSerializeMethod<T>(T value, JsonSerializerOptions options)
                        => JsonSerializer.Serialize(value, options);
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_NonSerializerMethod_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        var options = new JsonSerializerOptions();
                        return MyCustomSerializeMethod(value, options);
                    }

                    static string MyCustomSerializeMethod<T>(T value, JsonSerializerOptions options) 
                        => JsonSerializer.Serialize(value, options);
                }
                """);

        [Fact]
        public Task CS_UseNewOptionsAsArgument_InterlockedCompareExchange_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;
                using System.Threading;

                class Program
                {
                    static JsonSerializerOptions s_options;
                    static string Serialize<T>(T value)
                    {
                        return JsonSerializer.Serialize(value,
                            s_options ??
                            Interlocked.CompareExchange(ref s_options, new JsonSerializerOptions() {WriteIndented = true}, null) ??
                            s_options);
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_MethodWithJsonOptionsArgument_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions options = TweakOptions(new JsonSerializerOptions());
                        return JsonSerializer.Serialize(value, options);
                    }

                    static JsonSerializerOptions TweakOptions(JsonSerializerOptions options)
                    {
                        options.WriteIndented = true;
                        return options;
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_MethodWithJsonOptionsArgument_VarDeclaration_ReturnsNonOptions_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        T newValue = ProcessOptions<T>(new JsonSerializerOptions());
                        return JsonSerializer.Serialize(newValue);
                    }

                    static T ProcessOptions<T>(JsonSerializerOptions options)
                    {
                        return default;
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_MethodWithJsonOptionsArgument_ExprStatement_ReturnsNonOptions_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        T newValue;
                        newValue = ProcessOptions<T>(new JsonSerializerOptions());
                        return JsonSerializer.Serialize(newValue);
                    }

                    static T ProcessOptions<T>(JsonSerializerOptions options)
                    {
                        return default;
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_EscapeCurrentScope_NonSerializerMethod_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        var options = new JsonSerializerOptions();
                        string json1 = MyCustomSerializeMethod(value, options);
                        string json2 = JsonSerializer.Serialize(value, options);
                        return json1 + json2;
                    }

                    static string MyCustomSerializeMethod<T>(T value, JsonSerializerOptions options) 
                        => JsonSerializer.Serialize(value, options);
                }
                """);

        [Theory]
        [MemberData(nameof(CS_UseNewLocalOptionsAsArgument_FieldAssignment_NoWarn_TheoryData))]
        public Task CS_UseNewLocalOptionsAsArgument_FieldAssignment_NoWarn(string snippet)
        {
            var test = new VerifyCS.Test();
            test.LanguageVersion = LanguageVersion.CSharp8; // needed for coalescing assignment.
            test.TestCode = $$"""
                using System;
                using System.Text.Json;
                using System.Threading;

                class Program
                {
                    static JsonSerializerOptions s_options;

                    static string Serialize<T>(T value)
                    {
                        {{snippet}}
                        return JsonSerializer.Serialize(value, opt);
                    }
                }
                """;

            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(CS_UseNewLocalOptionsAsArgument_PropertyAssignment_NoWarn_TheoryData))]
        public Task CS_UseNewLocalOptionsAsArgument_PropertyAssignment_NoWarn(string snippet)
        {
            var test = new VerifyCS.Test();
            test.LanguageVersion = LanguageVersion.CSharp8; // needed for coalescing assignment.
            test.TestCode = $$"""
                using System;
                using System.Text.Json;

                class Program
                {
                    private JsonSerializerOptions Options { get; set; }

                    string Serialize<T>(T value)
                    {
                        {{snippet}}
                        return JsonSerializer.Serialize(value, opt);
                    }
                }
                """;
            return test.RunAsync();
        }

        public static IEnumerable<object[]> CS_UseNewLocalOptionsAsArgument_FieldAssignment_NoWarn_TheoryData()
        {
            return CS_UseNewLocalOptionsAsArgument_Assignment_TheoryData(useField: true).Select(e => new object[] { e });
        }

        public static IEnumerable<object[]> CS_UseNewLocalOptionsAsArgument_PropertyAssignment_NoWarn_TheoryData()
        {
            return CS_UseNewLocalOptionsAsArgument_Assignment_TheoryData(useField: false).Select(e => new object[] { e });
        }

        private static List<string> CS_UseNewLocalOptionsAsArgument_Assignment_TheoryData(bool useField)
        {
            string target = useField ? "s_options" : "Options";

            var l = new List<string>()
            {
                $"""
                JsonSerializerOptions opt = new JsonSerializerOptions();
                {target} = opt;
                """,

                $"""
                JsonSerializerOptions opt;
                {target} = opt = new JsonSerializerOptions();
                """,

                $"JsonSerializerOptions opt = {target} = new JsonSerializerOptions();",

                $"JsonSerializerOptions opt = {target} ??= new JsonSerializerOptions();",

                $"""
                JsonSerializerOptions opt = new JsonSerializerOptions();
                {target} ??= opt;
                """,

                $"""
                JsonSerializerOptions opt = new JsonSerializerOptions();
                ({target}, _) = (opt, 42);
                """,

                $"""
                JsonSerializerOptions opt = new JsonSerializerOptions();
                (({target}, _), _) = ((opt, 42), 42);
                """
            };

            if (useField)
            {
                l.Add("""
                JsonSerializerOptions opt = 
                    s_options ?? 
                    Interlocked.CompareExchange(ref s_options, new JsonSerializerOptions() {WriteIndented = true}, null) ??
                    s_options;
                """);
            }

            return l;
        }

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_NotSingleUse_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string SerializeTwice<T>(T value)
                    {
                        JsonSerializerOptions opt = new JsonSerializerOptions();

                        string str1 = JsonSerializer.Serialize(value, opt);
                        string str2 = JsonSerializer.Serialize(value, opt);

                        return str1 + str2;
                    }
                }
                """);

        [Theory]
        [InlineData("opt1", "opt2")]
        [InlineData("opt1", "opt3")]
        [InlineData("opt2", "opt3")]
        public Task CS_UseNewLocalOptionsAsArgument_MultiAssignment_NotSingleUse_NoWarn(string expression1, string expression2)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt1, opt2, opt3;
                        opt1 = opt2 = opt3 = new JsonSerializerOptions();

                        string json1 = JsonSerializer.Serialize(value, {{expression1}});
                        string json2 = JsonSerializer.Serialize(value, {{expression2}});
        
                        return json1 + json2;
                    }
                }
                """);

        [Theory]
        [InlineData("opt1")]
        [InlineData("opt2")]
        [InlineData("opt3")]
        public Task CS_UseNewLocalOptionsAsArgument_MultiAssignment_EscapeCurrentScope_FieldAssignment_NoWarn(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static JsonSerializerOptions s_options;

                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt1, opt2, opt3;
                        opt1 = opt2 = opt3 = new JsonSerializerOptions();

                        s_options = {{expression}};

                        return JsonSerializer.Serialize(value, opt1);
                    }
                }
                """);

        [Theory]
        [InlineData("opt1 = opt2 = s_options")]
        [InlineData("opt1 = s_options = opt2")]
        [InlineData("s_options = opt1 = opt2")]
        public Task CS_UseNewLocalOptionsAsArgument_MultiAssignment_EscapeCurrentScope_FieldInMultiAssignment_NoWarn(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static JsonSerializerOptions s_options;

                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt1, opt2;
                        {{expression}} = new JsonSerializerOptions();

                        return JsonSerializer.Serialize(value, opt1);
                    }
                }
                """);

        [Theory]
        [InlineData("s_options = opt1 = opt2")]
        [InlineData("opt1 = s_options = opt2")]
        public Task CSharpUseNewOptionsAsLocalThenAsArgument_AssignmentOnNextStatement_Multiple_WithEscapeScopeOnAssignment_NoWarn(string expression)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static JsonSerializerOptions s_options;

                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt1, opt2;
                        opt1 = opt2 = new JsonSerializerOptions();

                        {{expression}};

                        return JsonSerializer.Serialize(value, opt1);
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_EscapeCurrentScope_ClosureDelegate_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Text.Json;

                class Program
                {
                    static Action Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt = new JsonSerializerOptions();
                        Action lambda = () =>
                        {
                            JsonSerializer.Serialize(value, opt);
                        };
                        return lambda;
                    }
                }
                """);

        [Fact]
        public Task CS_UseNewLocalOptionsAsArgument_EscapeCurrentScope_ClosureLocalFunction_NoWarn()
            => VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T value)
                    {
                        JsonSerializerOptions opt = new JsonSerializerOptions();
                        return LocalFunc();

                        string LocalFunc()
                        {
                            return JsonSerializer.Serialize(value, opt);
                        }
                    }
                }
                """);

        [Theory]
        [InlineData("""
            for (int i = 0; i < values.Length; i++)
            {
                concatJson += JsonSerializer.Serialize(values[i], opt);
            }
            """)]
        [InlineData("""
            foreach (T value in values)
            {
                concatJson += JsonSerializer.Serialize(value, opt);
            }
            """)]
        [InlineData("""
            if (values.Length == 0) 
                return concatJson;

            int i = 0;
            do
            {
                concatJson += JsonSerializer.Serialize(values[i++], opt);
            }
            while (i < values.Length);
            """)]
        [InlineData("""
            int i = 0;
            while (i < values.Length)
            {
                concatJson += JsonSerializer.Serialize(values[i++], opt);
            }
            """)]
        public Task CS_UseNewLocalOptionsAsArgument_JsonSerializerOptions_BeforeLoop_NoWarn(string loop)
            => VerifyCS.VerifyAnalyzerAsync($$"""
                using System.Text.Json;

                class Program
                {
                    static string Serialize<T>(T[] values)
                    {
                        JsonSerializerOptions opt = new JsonSerializerOptions();
                        string concatJson = "";

                        {{loop}}

                        return concatJson;
                    }
                }
                """);

        [Theory]
        [InlineData("""
            For i = 0 To values.Length
                concatJson += JsonSerializer.Serialize(values(i), opt)
            Next
            """)]
        [InlineData("""
            For Each value In values
                concatJson += JsonSerializer.Serialize(value, opt)
            Next
            """)]
        [InlineData("""
            Dim i = 0
            Do While i < values.Length
                concatJson += JsonSerializer.Serialize(values(i), opt)
                i = i + 1
            Loop
            """)]
        [InlineData("""
            Dim i = 0
            Do
                concatJson += JsonSerializer.Serialize(values(i), opt)
                i = i + 1
            Loop While i < values.Length
            """)]
        public Task VB_UseNewLocalOptionsAsArgument_JsonSerializerOptions_BeforeLoop_NoWarn(string loop)
            => VerifyVB.VerifyAnalyzerAsync($$"""
                Imports System.Text.Json

                Class Program
                    Shared Function Serialize(Of T)(values As T()) As String
                        Dim opt = New JsonSerializerOptions()
                        Dim concatJson = ""

                        {{loop}}

                        return concatJson
                    End Function
                End Class
                """);
        #endregion
    }
}
