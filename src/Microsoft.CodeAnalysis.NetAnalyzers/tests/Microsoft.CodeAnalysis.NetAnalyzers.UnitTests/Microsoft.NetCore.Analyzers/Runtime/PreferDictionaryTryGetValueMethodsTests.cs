// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryTryGetValueAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferDictionaryTryGetValueFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryTryGetValueAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicPreferDictionaryTryGetValueFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferDictionaryTryGetValueMethodsTests
    {
        #region C# Tests

        private const string CSharpTemplate = @"
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Test
{{
    public class TestClass
    {{
        public int TestMethod()
        {{
            {0}
        }}

        private class MyDictionary<TKey, TValue> {{
            public bool ContainsKey(TKey key) {{
                return true;
            }}

            public TValue this[TKey key] {{ get => default; set {{}} }}
        }} 
    }}
}}";

        private const string DictionaryContainsKeyPrintValue = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine({|#1:data[key]|});
            }

            return 0;";

        private const string DictionaryContainsKeyPrintValueFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                Console.WriteLine(value);
            }

            return 0;";

        private const string DictionaryContainsKeyReturnValue = @"
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                return {|#1:data[key]|};
            }

            return 0;";

        private const string DictionaryContainsKeyReturnValueFixed = @"
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                return value;
            }

            return 0;";

        private const string DictionaryContainsKeyMultipleStatementsInIf = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine({|#1:data[key]|});
                
                return x;
            }

            return 0;";

        private const string DictionaryContainsKeyMultipleStatementsInIfFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine(value);
                
                return x;
            }

            return 0;";

        private const string DictionaryContainsKeyMultipleConditions = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (key == ""key"" && {|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine({|#1:data[key]|});
                
                return x;
            }

            return 0;";

        private const string DictionaryContainsKeyMultipleConditionsFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (key == ""key"" && data.TryGetValue(key, out int value))
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine(value);
                
                return x;
            }

            return 0;";

        private const string DictionaryContainsKeyNestedDictionaryAccess = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (key == ""key"" && {|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine(Wrapper({|#1:data[key]|}));
                
                return x;
            }

            int Wrapper(int i) {
                return i;
            }

            return 0;";

        private const string DictionaryContainsKeyNestedDictionaryAccessFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (key == ""key"" && data.TryGetValue(key, out int value))
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine(Wrapper(value));
                
                return x;
            }

            int Wrapper(int i) {
                return i;
            }

            return 0;";

        private const string DictionaryContainsKeyTernary = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return {|#0:data.ContainsKey(key)|} ? {|#1:data[key]|} : 2;";

        private const string DictionaryContainsKeyTernaryFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return data.TryGetValue(key, out int value) ? value : 2;";

        #region NoDiagnostic

        private const string DictionaryContainsKeyModifyDictionary = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                data[key] = 2;
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string DictionaryContainsKeyNonIDictionary = @"
            string key = ""key"";
            MyDictionary<string, int> data = new MyDictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string DictionaryContainsKeyNotGuardedByContainsKey = @"
            string key = ""key"";
            int value = 3;
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsValue(value))
            {
                Console.WriteLine(data[key]);
            }

            return 0;";

        #endregion

        #endregion

        #region VB Tests

        private const string VbTemplate = @"
Imports System
Imports System.Collections
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Linq

Namespace Test
    Public Class TestClass
        Public Function TestMethod() As Integer
            {0}
        End Function

        Private Class MyDictionary(Of TKey, TValue)
            Public Function ContainsKey(ByVal key As TKey) As Boolean
                Return True
            End Function

            Default Public Property Item(ByVal key As TKey) As TValue
                Get
                    Return Nothing
                End Get
                Set(ByVal value As TValue)
                End Set
            End Property
        End Class
    End Class
End Namespace";

        private const string VbDictionaryContainsKeyPrintValue = @"
            Dim key As String = ""key""
            Dim data As Dictionary(Of String, Guid) = New Dictionary(Of String, Guid)()

            If {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine({|#1:data(key)|})
            End If

            Return 0";

        private const string VbDictionaryContainsKeyPrintValueFixed = @"
            Dim key As String = ""key""
            Dim data As Dictionary(Of String, Guid) = New Dictionary(Of String, Guid)()

            Dim value As Guid
            If data.TryGetValue(key, value) Then
                Console.WriteLine(value)
            End If

            Return 0";

        private const string VbDictionaryContainsKeyReturnValue = @"
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Return {|#1:data(key)|}
            End If

            Return 0";

        private const string VbDictionaryContainsKeyReturnValueFixed = @"
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            Dim value As Integer
            If data.TryGetValue(key, value) Then
                Return value
            End If

            Return 0";

        private const string VbDictionaryContainsKeyMultipleStatementsInIf = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine({|#1:data(key)|})

                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyMultipleStatementsInIfFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer
            If data.TryGetValue(key, value) Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine(value)

                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyMultipleConditions = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If key = ""key"" AndAlso {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine({|#1:data(key)|})
                
                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyMultipleConditionsFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer
            If key = ""key"" AndAlso data.TryGetValue(key, value) Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine(value)
                
                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyNestedDictionaryAccess = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If key = ""key"" AndAlso {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine(2)
                Dim x = 2
                Dim wrapper = Function(i As Integer) As Integer
                    Return i
                End Function
                Console.WriteLine(wrapper({|#1:data(key)|}))
        
                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyNestedDictionaryAccessFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer
            If key = ""key"" AndAlso data.TryGetValue(key, value) Then
                Console.WriteLine(2)
                Dim x = 2
                Dim wrapper = Function(i As Integer) As Integer
                    Return i
                End Function
                Console.WriteLine(wrapper(value))
        
                Return x
            End If

            Return 0";

        private const string VbDictionaryContainsKeyTernary = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Return If({|#0:data.ContainsKey(key)|}, {|#1:data(key)|}, 2)";

        private const string VbDictionaryContainsKeyTernaryFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer
            Return If(data.TryGetValue(key, value), value, 2)";

        #region NoDiagnostic

        private const string VbDictionaryContainsKeyModifyDictionary = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(2)
                data(key) = 2
                Console.WriteLine(data(key))

                Return 2
            End If

            Return 0";

        private const string VbDictionaryContainsKeyNonIDictionary = @"
            Dim key As String = ""key""
            Dim data As MyDictionary(Of String, Integer) = New MyDictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(2)
                Console.WriteLine(data(key))

                Return 2
            End If

            Return 0";

        private const string VbDictionaryContainsKeyNotGuardedByContainsKey = @"
            Dim key As String = ""key""
            Dim value As Integer = 3
            Dim data As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsValue(value) Then
                Console.WriteLine(data(key))
            End If

            Return 0";

        #endregion

        #endregion

        [Theory]
        [InlineData(DictionaryContainsKeyPrintValue, DictionaryContainsKeyPrintValueFixed)]
        [InlineData(DictionaryContainsKeyReturnValue, DictionaryContainsKeyReturnValueFixed)]
        [InlineData(DictionaryContainsKeyMultipleStatementsInIf, DictionaryContainsKeyMultipleStatementsInIfFixed)]
        [InlineData(DictionaryContainsKeyMultipleConditions, DictionaryContainsKeyMultipleConditionsFixed)]
        [InlineData(DictionaryContainsKeyNestedDictionaryAccess, DictionaryContainsKeyNestedDictionaryAccessFixed)]
        [InlineData(DictionaryContainsKeyTernary, DictionaryContainsKeyTernaryFixed)]
        public Task ShouldReportDiagnostic(string codeSnippet, string fixedCodeSnippet)
        {
            string testCode = CreateCSharpCode(codeSnippet);
            string fixedCode = CreateCSharpCode(fixedCodeSnippet);
            var diagnostic = VerifyCS.Diagnostic(PreferDictionaryTryGetValueAnalyzer.ContainsKeyRule).WithLocation(0).WithLocation(1);

            return new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Theory]
        [InlineData(DictionaryContainsKeyModifyDictionary)]
        [InlineData(DictionaryContainsKeyNonIDictionary)]
        [InlineData(DictionaryContainsKeyNotGuardedByContainsKey)]
        public Task ShouldNotReportDiagnostic(string codeSnippet)
        {
            string testCode = CreateCSharpCode(codeSnippet);

            return new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData(VbDictionaryContainsKeyPrintValue, VbDictionaryContainsKeyPrintValueFixed)]
        [InlineData(VbDictionaryContainsKeyReturnValue, VbDictionaryContainsKeyReturnValueFixed)]
        [InlineData(VbDictionaryContainsKeyMultipleStatementsInIf, VbDictionaryContainsKeyMultipleStatementsInIfFixed)]
        [InlineData(VbDictionaryContainsKeyMultipleConditions, VbDictionaryContainsKeyMultipleConditionsFixed)]
        [InlineData(VbDictionaryContainsKeyNestedDictionaryAccess, VbDictionaryContainsKeyNestedDictionaryAccessFixed)]
        [InlineData(VbDictionaryContainsKeyTernary, VbDictionaryContainsKeyTernaryFixed)]
        public Task VbShouldReportDiagnostic(string codeSnippet, string fixedCodeSnippet)
        {
            string testCode = CreateVbCode(codeSnippet);
            string fixedCode = CreateVbCode(fixedCodeSnippet);
            var diagnostic = VerifyVB.Diagnostic(PreferDictionaryTryGetValueAnalyzer.ContainsKeyRule).WithLocation(0).WithLocation(1);

            return new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Theory]
        [InlineData(VbDictionaryContainsKeyModifyDictionary)]
        [InlineData(VbDictionaryContainsKeyNonIDictionary)]
        [InlineData(VbDictionaryContainsKeyNotGuardedByContainsKey)]
        public Task VbShouldNotReportDiagnostic(string codeSnippet)
        {
            string testCode = CreateVbCode(codeSnippet);

            return new VerifyVB.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory, CombinatorialData]
        [WorkItem(6022, "https://github.com/dotnet/roslyn-analyzers/issues/6022")]
        public async Task TestVarPreference(bool preferVar)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Collections.Generic;

class C
{
    void M(string key)
    {
        var data = new Dictionary<string, int>();
        if ([|data.ContainsKey(key)|])
        {
            Console.WriteLine(data[key]);
        }
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true
[*]
csharp_style_var_for_built_in_types = {preferVar}
csharp_style_var_when_type_is_apparent = {preferVar}
csharp_style_var_elsewhere = {preferVar}") }
                },
                FixedCode = $@"
using System;
using System.Collections.Generic;

class C
{{
    void M(string key)
    {{
        var data = new Dictionary<string, int>();
        if (data.TryGetValue(key, out {(preferVar ? "var" : "int")} value))
        {{
            Console.WriteLine(value);
        }}
    }}
}}
",
            }.RunAsync();
        }

        private static string CreateCSharpCode(string content)
        {
            return string.Format(CultureInfo.InvariantCulture, CSharpTemplate, content);
        }

        private static string CreateVbCode(string content)
        {
            return string.Format(CultureInfo.InvariantCulture, VbTemplate, content);
        }
    }
}