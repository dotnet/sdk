// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferDictionaryTryMethodsOverContainsKeyGuardFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferDictionaryTryMethodsOverContainsKeyGuardFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
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
        public int TestMethod(Dictionary<string, int> parameter)
        {{
            {0}
        }}

        public Dictionary<string, int> memberField = new Dictionary<string, int>();
        public Dictionary<string, int>[] memberFieldArray = new [] {{new Dictionary<string, int>(), new Dictionary<string, int>()}};
        public Dictionary<string, int> MemberProperty {{get; set;}} = new Dictionary<string, int>();

        public static Dictionary<string, int> staticField = new Dictionary<string, int>();
        public static Dictionary<string, int> StaticProperty {{get; set;}} = new Dictionary<string, int>();
        private const string constKey = ""key"";

        private class MyDictionary<TKey, TValue> {{
            public bool ContainsKey(TKey key) {{
                return true;
            }}

            public TValue this[TKey key] {{ get => default; set {{}} }}
        }} 
    }}
}}";

        private const string GuardedPrintValue = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine({|#1:data[key]|});
                Console.WriteLine({|#2:data[key]|});
            }

            return 0;";

        private const string GuardedPrintValueFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                Console.WriteLine(value);
                Console.WriteLine(value);
            }

            return 0;";

        private const string GuardedReturn = @"
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                return {|#1:data[key]|};
            }

            return 0;";

        private const string GuardedReturnFixed = @"
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                return value;
            }

            return 0;";

        private const string GuardedWithUnrelatedStatements = @"
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

        private const string GuardedWithUnrelatedStatementsFixed = @"
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

        private const string GuardedAndCondition = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|} && {|#1:data[key]|} == 2)
            {
                Console.WriteLine({|#2:data[key]|});
                return 2;
            }

            return 0;";

        private const string GuardedAndConditionFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value) && value == 2)
            {
                Console.WriteLine(value);
                return 2;
            }

            return 0;";

        private const string GuardedOrCondition = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!{|#0:data.ContainsKey(key)|} || {|#1:data[key]|} != 2)
            {
                Console.WriteLine(2);
                return -1;
            }

            return {|#2:data[key]|};";

        private const string GuardedOrConditionFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!data.TryGetValue(key, out int value) || value != 2)
            {
                Console.WriteLine(2);
                return -1;
            }

            return value;";

        private const string GuardedWithThrow = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!{|#0:data.ContainsKey(key)|})
                throw new Exception();

            return {|#1:data[key]|};";

        private const string GuardedWithThrowFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!data.TryGetValue(key, out int value))
                throw new Exception();

            return value;";

        private const string GuardedNestedDictionaryAccess = @"
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

        private const string GuardedNestedDictionaryAccessFixed = @"
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

        private const string GuardedTernary = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return {|#0:data.ContainsKey(key)|} ? {|#1:data[key]|} : 2;";

        private const string GuardedTernaryTernaryFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return data.TryGetValue(key, out int value) ? value : 2;";

        private const string GuardedTernaryTernarySquared = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return {|#0:data.ContainsKey(key)|} ? {|#1:data[key]|} * {|#2:data[key]|} : 2;";

        private const string GuardedTernarySquaredFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();

            return data.TryGetValue(key, out int value) ? value * value : 2;";

        private const string GuardedWithKeyLiteral = @"
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(""key"")|})
            {
                Console.WriteLine({|#1:data[""key""]|});
                Console.WriteLine(data[""key2""]);
                Console.WriteLine({|#2:data[constKey]|});
            }

            return 0;";

        private const string GuardedWithKeyLiteralFixed = @"
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(""key"", out int value))
            {
                Console.WriteLine(value);
                Console.WriteLine(data[""key2""]);
                Console.WriteLine(value);
            }

            return 0;";

        private const string GuardedOutReference = @"
            string key = ""key"";
            bool GetDict(out IDictionary<string, int> dict)
            {
                dict = new Dictionary<string, int>();
                return true;
            }
            if (GetDict(out var data) && {|#0:data.ContainsKey(key)|})
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine({|#1:data[key]|});
                
                return x;
            }

            return 0;";

        private const string GuardedOutReferenceFixed = @"
            string key = ""key"";
            bool GetDict(out IDictionary<string, int> dict)
            {
                dict = new Dictionary<string, int>();
                return true;
            }
            if (GetDict(out var data) && data.TryGetValue(key, out int value))
            {
                Console.WriteLine(2);
                var x = 2;
                Console.WriteLine(value);
                
                return x;
            }

            return 0;";

        private const string GuardedAddBeforeUsage = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (!{|#0:data.ContainsKey(key)|})
            {
                {|#3:data.Add(key, 2);|}
            }

            Console.WriteLine(2);
            var x = 2;
            Console.WriteLine({|#1:data[key]|});

            return {|#2:data[key]|};";

        private const string GuardedAddBeforeUsageFixed = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (!data.TryGetValue(key, out int value))
            {
                value = 2;
                data.Add(key, value);
            }

            Console.WriteLine(2);
            var x = 2;
            Console.WriteLine(value);

            return value;";

        private const string GuardedIndexerSetBeforeUsage = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!{|#0:data.ContainsKey(key)|})
            {
                {|#3:data[key] = 2;|}
            }

            Console.WriteLine(2);
            var x = 2;
            Console.WriteLine({|#1:data[key]|});

            return {|#2:data[key]|};";

        private const string GuardedIndexerSetBeforeUsageFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (!data.TryGetValue(key, out int value))
            {
                value = 2;
                data[key] = value;
            }

            Console.WriteLine(2);
            var x = 2;
            Console.WriteLine(value);

            return value;";

        private const string GuardedIndexerPostIncrement = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
                {|#1:data[key]|}++;
            else
                data[key] = 1;

            return 0;";

        private const string GuardedIndexerPostIncrementFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
                data[key] = ++value;
            else
                data[key] = 1;

            return 0;";

        private const string GuardedIndexerPreIncrement = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
                ++{|#1:data[key]|};
            else
                data[key] = 1;

            return 0;";

        private const string GuardedIndexerPreIncrementFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
                data[key] = ++value;
            else
                data[key] = 1;

            return 0;";

        private const string GuardedIndexerInSimpleAssignment = @"
            string key = ""key"";
            var data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                data[key] = {|#1:data[key]|} + 1;
                return data[key];
            }
            return 0;";

        private const string GuardedIndexerInSimpleAssignmentFixed = @"
            string key = ""key"";
            var data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
            {
                data[key] = value + 1;
                return data[key];
            }
            return 0;";

        private const string GuardedIndexerInCompoundAssignment = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
                data[key] += {|#1:data[key]|} + 2;
            else
                data[key] = 1;

            return 0;";

        private const string GuardedIndexerInCompoundAssignmentFixed = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.TryGetValue(key, out int value))
                data[key] += value + 2;
            else
                data[key] = 1;

            return 0;";

        private const string GuardedKeyInSimpleAssignment = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if ({|#0:data.ContainsKey(key)|})
            {
                key = {|#1:data[key]|};
                key = data[key];
            }

            return 0;";

        private const string GuardedKeyInSimpleAssignmentFixed = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if (data.TryGetValue(key, out string value))
            {
                key = value;
                key = data[key];
            }

            return 0;";

        private const string GuardedInlineVariable = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if ({|#0:data.ContainsKey(key)|})
            {
                {|#1:var a = data[key];|}
            }
            return 0;";

        private const string GuardedInlineVariableFixed = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if (data.TryGetValue(key, out string a))
            {
            }
            return 0;";

        private const string GuardedInlineVariable2 = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if ({|#0:data.ContainsKey(key)|})
            {
                string {|#1:a = data[key]|}, b = """";
            }
            return 0;";

        private const string GuardedInlineVariable2Fixed = @"
            string key = ""key"";
            var data = new Dictionary<string, string>();
            if (data.TryGetValue(key, out string a))
            {
                string b = """";
            }
            return 0;";

        private const string GuardedReturnIdentifierUsed = @"
            int value = 0;
            int value1 = 1;
            int value2 = 2;
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if ({|#0:data.ContainsKey(key)|})
            {
                return {|#1:data[key]|};
            }

            return 0;";

        private const string GuardedReturnIdentifierUsedFixed = @"
            int value = 0;
            int value1 = 1;
            int value2 = 2;
            string key = ""key"";
            ConcurrentDictionary<string, int> data = new ConcurrentDictionary<string, int>();
            if (data.TryGetValue(key, out int value3))
            {
                return value3;
            }

            return 0;";

        #region NoDiagnostic

        private const string InvalidModifiedBeforeUse = @"
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

        private const string InvalidNonIDictionary = @"
            string key = ""key"";
            MyDictionary<string, int> data = new MyDictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string InvalidNotGuardedByContainsKey = @"
            string key = ""key"";
            int value = 3;
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsValue(value))
            {
                Console.WriteLine(data[key]);
            }

            return 0;";

        private const string InvalidAddBeforeUse = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                data.Add(key, 2);
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string InvalidRemoveBeforeUse = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                data.Remove(key);
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string InvalidModifyReference = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                Console.WriteLine(2);
                data = new Dictionary<string, int>();
                Console.WriteLine(data[key]);
                
                return 2;
            }

            return 0;";

        private const string InvalidDifferentKey = @"
            string key = ""key"";
            string key2 = ""key2"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                return data[key2];
            }

            return 0;";

        private const string InvalidKeyChangedSimple = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                key = ""key2"";
                return data[key];
            }

            return 0;";

        private const string InvalidKeyChangedCompound = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                key += ""key2"";
                return data[key];
            }

            return 0;";

        private const string InvalidKeyChangedIncrement = @"
            var key = 1;
            var data = new Dictionary<int, int>();
            if (data.ContainsKey(key))
            {
                key++;
                return data[key];
            }

            return 0;";

        private const string InvalidOtherLiteral = @"
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(""key""))
            {
                Console.WriteLine(data[""key2""]);
            }

            return 0;";

        private const string InvalidEntryModified = @"
            string key = ""key"";
            IDictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                data[key] = 1;
                return data[key];
            }
            return 0;";

        private const string InvalidEntryModifiedCoalesceAssignment = @"
            string key = ""key"";
            var data = new Dictionary<string, object>();
            if (data.ContainsKey(key))
            {
                data[key] ??= (object)1;
                return (int)data[key];
            }
            return 0;";

        private const string InvalidNotGuarded = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
            {
                data[key] = 2;
            }

            Console.WriteLine(2);
            var x = 2;
            Console.WriteLine(data[key]);

            return data[key];";

        private const string InvalidArrayIndexerChanged = @"
            string key = ""key"";
           var data = new Dictionary<string, int>[][]
            {
                new Dictionary<string, int>[] {new Dictionary<string, int>(),new Dictionary<string, int>()},
                new Dictionary<string, int>[] {new Dictionary<string, int>(),new Dictionary<string, int>()}
            };
            var i1 = 0;
            var i2 = 0;
            if (data[i1][i2].ContainsKey(key))
            {
                i1 = 1;
                return data[i1][i2][key];
            }

            return 0;";

        private const string InvalidKeyChangedInCondition = @"
            var key = 1;
            var data = new Dictionary<int, int>();
            if (data.ContainsKey(key) && data[key++] == 2)
            {
                return data[key];
            }

            return 0;";

        private const string InvalidKeyChangedAfterAdd = @"
            var key = 1;
            var data = new Dictionary<int, int>();
            if (!data.ContainsKey(key))
            {
                data.Add(key, 2);
                key = 2;
            }

            return data[key];";

        private const string InvalidComplexPostIncrement = @"
            string key = ""key"";
            Dictionary<string, int> data = new Dictionary<string, int>();
            if (data.ContainsKey(key))
                return data[key]++;

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
        Public Function TestMethod(ByRef parameter As Dictionary(Of String, Integer)) As Integer
            {0}
        End Function

        Public memberField As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)
        Public memberFieldArray As Dictionary(Of String, Integer)() = New Dictionary(Of String, Integer)() {{New Dictionary(Of String, Integer)(), New Dictionary(Of String, Integer)()}}
        Public Property MemberProperty As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)

        Public Shared staticField As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)
        Public Shared Property StaticProperty As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)
        Public Const constKey As String = ""key""

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

        private const string VbGuardedPrintValue = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine({|#1:data(key)|})
                Console.WriteLine({|#2:data(key)|})
            End If

            Return 0";

        private const string VbGuardedPrintValueFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            If data.TryGetValue(key, value) Then
                Console.WriteLine(value)
                Console.WriteLine(value)
            End If

            Return 0";

        private const string VbGuardedReturn = @"
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Return {|#1:data(key)|}
            End If

            Return 0";

        private const string VbGuardedReturnFixed = @"
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            If data.TryGetValue(key, value) Then
                Return value
            End If

            Return 0";

        private const string VbGuardedWithUnrelatedStatements = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine({|#1:data(key)|})

                Return x
            End If

            Return 0";

        private const string VbGuardedWithUnrelatedStatementsFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            If data.TryGetValue(key, value) Then
                Console.WriteLine(2)
                Dim x = 2
                Console.WriteLine(value)

                Return x
            End If

            Return 0";

        private const string VbGuardedAndCondition = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()
            If {|#0:data.ContainsKey(key)|} AndAlso {|#1:data(key)|} = 2 Then
                Console.WriteLine({|#2:data(key)|})
                Return 2
            End If

            Return 0";

        private const string VbGuardedAndConditionFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If data.TryGetValue(key, value) AndAlso value = 2 Then
                Console.WriteLine(value)
                Return 2
            End If

            Return 0";

        private const string VbGuardedOrCondition = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()
            If Not {|#0:data.ContainsKey(key)|} OrElse {|#1:data(key)|} <> 2 Then
                Console.WriteLine(2)
                Return -1
            End If

            Return {|#2:data(key)|}";

        private const string VbGuardedOrConditionFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If Not data.TryGetValue(key, value) OrElse value <> 2 Then
                Console.WriteLine(2)
                Return -1
            End If

            Return value";

        private const string VbGuardedNestedDictionaryAccess = @"
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

        private const string VbGuardedNestedDictionaryAccessFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
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

        private const string VbGuardedTernary = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Return If({|#0:data.ContainsKey(key)|}, {|#1:data(key)|}, 2)";

        private const string VbGuardedTernaryFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            Return If(data.TryGetValue(key, value), value, 2)";

        private const string VbGuardedTernarySquared = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Return If({|#0:data.ContainsKey(key)|}, {|#1:data(key)|} * {|#2:data(key)|}, 2)";

        private const string VbGuardedTernarySquaredFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            Return If(data.TryGetValue(key, value), value * value, 2)";

        private const string VbGuardedWithKeyLiteral = @"
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()
            If {|#0:data.ContainsKey(""key"")|}
                Console.WriteLine({|#1:data(""key"")|})
                Console.WriteLine(data(""key2""))
                Console.WriteLine({|#2:data(constKey)|})
            End If

            Return 0";

        private const string VbGuardedWithKeyLiteralFixed = @"
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If data.TryGetValue(""key"", value)
                Console.WriteLine(value)
                Console.WriteLine(data(""key2""))
                Console.WriteLine(value)
            End If

            Return 0";

        private const string VbGuardedWithKeyLiteralAndAccessWithExclamation = @"
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()
            If {|#0:data.ContainsKey(""key"")|}
                Console.WriteLine({|#1:data!key|})
            End If

            Return 0";

        private const string VbGuardedWithKeyLiteralAndAccessWithExclamationFixed = @"
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If data.TryGetValue(""key"", value)
                Console.WriteLine(value)
            End If

            Return 0";

        private const string VbGuardedAddBeforeUsage = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If Not {|#0:data.ContainsKey(key)|} Then
                {|#3:data.Add(key, 2)|}
            End If

            Console.WriteLine(2)
            Console.WriteLine({|#1:data(key)|})

            Return {|#2:data(key)|}";

        private const string VbGuardedAddBeforeUsageFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            If Not data.TryGetValue(key, value) Then
                value = 2
                data.Add(key, value)
            End If

            Console.WriteLine(2)
            Console.WriteLine(value)

            Return value";

        private const string VbGuardedIndexerSetBeforeUsage = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If Not {|#0:data.ContainsKey(key)|} Then
                {|#3:data(key) = 2|}
            End If

            Console.WriteLine(2)
            Console.WriteLine({|#1:data(key)|})

            Return {|#2:data(key)|}";

        private const string VbGuardedIndexerSetBeforeUsageFixed = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing
            If Not data.TryGetValue(key, value) Then
                value = 2
                data(key) = value
            End If

            Console.WriteLine(2)
            Console.WriteLine(value)

            Return value";

        private const string VbGuardedIndexerInSimpleAssignment = @"
            Dim key = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()
            If {|#0:data.ContainsKey(key)|} Then
                data(key) = {|#1:data(key)|} + 1
                Return data(key)
            End If
            Return 0";

        private const string VbGuardedIndexerInSimpleAssignmentFixed = @"
            Dim key = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If data.TryGetValue(key, value) Then
                data(key) = value + 1
                Return data(key)
            End If
            Return 0";

        private const string VbGuardedIndexerInCompoundAssignment = @"
            Dim key = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()
            If {|#0:data.ContainsKey(key)|} Then
                data(key) += {|#1:data(key)|} + 2
            Else
                data(key) = 1
            End If
            Return 0";

        private const string VbGuardedIndexerInCompoundAssignmentFixed = @"
            Dim key = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            Dim value As Integer = Nothing

            If data.TryGetValue(key, value) Then
                data(key) += value + 2
            Else
                data(key) = 1
            End If
            Return 0";

        private const string VbGuardedKeyInSimpleAssignment = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()
            If {|#0:data.ContainsKey(key)|} Then
                key = {|#1:data(key)|}
                key = data(key)
            End If
            Return 0";

        private const string VbGuardedKeyInSimpleAssignmentFixed = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()

            Dim value As String = Nothing

            If data.TryGetValue(key, value) Then
                key = value
                key = data(key)
            End If
            Return 0";

        private const string VbGuardedInlineVariable = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()
            If {|#0:data.ContainsKey(key)|} Then
                {|#1:Dim x As String = data(key)|}
            End If
            Return 0";

        private const string VbGuardedInlineVariableFixed = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()

            Dim x As String = Nothing

            If data.TryGetValue(key, x) Then
            End If
            Return 0";

        private const string VbGuardedInlineVariable2 = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()
            If {|#0:data.ContainsKey(key)|} Then
                Dim {|#1:x As String = data(key)|}, y
            End If
            Return 0";

        private const string VbGuardedInlineVariable2Fixed = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, String)()

            Dim x As String = Nothing

            If data.TryGetValue(key, x) Then
                Dim y
            End If
            Return 0";

        private const string VbGuardedReturnIdentifierUsed = @"
            Dim value As Integer = 0
            Dim value1 As Integer = 1
            Dim value2 As Integer = 2
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            If {|#0:data.ContainsKey(key)|} Then
                Return {|#1:data(key)|}
            End If

            Return 0";

        private const string VbGuardedReturnIdentifierUsedFixed = @"
            Dim value As Integer = 0
            Dim value1 As Integer = 1
            Dim value2 As Integer = 2
            Dim key As String = ""key""
            Dim data As ConcurrentDictionary(Of String, Integer) = New ConcurrentDictionary(Of String, Integer)()

            Dim value3 As Integer = Nothing
            If data.TryGetValue(key, value3) Then
                Return value3
            End If

            Return 0";

        #region NoDiagnostic

        private const string VbInvalidModifiedBeforeUse = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(2)
                data(key) = 2
                Console.WriteLine(data(key))

                Return 2
            End If

            Return 0";

        private const string VbInvalidRemoveBeforeUse = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(2)
                data.Remove(key)
                Console.WriteLine(data(key))

                Return 2
            End If

            Return 0";

        private const string VbInvalidNonIDictionary = @"
            Dim key As String = ""key""
            Dim data As MyDictionary(Of String, Integer) = New MyDictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(2)
                Console.WriteLine(data(key))

                Return 2
            End If

            Return 0";

        private const string VbInvalidNotGuardedByContainsKey = @"
            Dim key As String = ""key""
            Dim value As Integer = 3
            Dim data As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsValue(value) Then
                Console.WriteLine(data(key))
            End If

            Return 0";

        private const string VbInvalidModifyReference = @" Dim key As String = ""key""
            Dim data As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                data = New Dictionary(Of String, Integer)()
                Console.WriteLine(data(key))
            End If

            Return 0";

        private const string VbInvalidDifferentKey = @"
            Dim key As String = ""key""
            Dim key2 As String = ""key2""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                Console.WriteLine(data(key2))
            End If

            Return 0";

        private const string VbInvalidKeyChangedSimple = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                key = ""key2""
                Return data(key)
            End If

            Return 0";

        private const string VbInvalidKeyChangedCompound = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(key) Then
                key += ""key2""
                Return data(key)
            End If

            Return 0";

        private const string VbInvalidOtherLiteral = @"
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

            If data.ContainsKey(""key"") Then
                Console.WriteLine(data(""key2""))
            End If

            Return 0";

        private const string VbInvalidNotGuarded = @"
            Dim key As String = ""key""
            Dim data As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()
            If data.ContainsKey(key) Then
                data(key) = 2
            End If

            Console.WriteLine(data(key))

            Return data(key)";

        private const string VbInvalidArrayIndexerChanged = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, Integer)() {New Dictionary(Of String, Integer)(), New Dictionary(Of String, Integer)()}
            Dim i = 0
            If data(i).ContainsKey(key) Then
                i = 1
                Return data(i)(key)
            End If

            Return 0";

        private const string VbInvalidKeyChangedAfterAdd = @"
            Dim key = ""key""
            Dim data = New Dictionary(Of String, Integer)()
            If Not data.ContainsKey(key) Then
                data.Add(key, 2)
                key = ""key2""
            End If

            Return data(key)";

        #endregion

        #endregion

        [Theory]
        [InlineData(GuardedPrintValue, GuardedPrintValueFixed, 2)]
        [InlineData(GuardedReturn, GuardedReturnFixed)]
        [InlineData(GuardedWithUnrelatedStatements, GuardedWithUnrelatedStatementsFixed)]
        [InlineData(GuardedOutReference, GuardedOutReferenceFixed)]
        [InlineData(GuardedAndCondition, GuardedAndConditionFixed, 2)]
        [InlineData(GuardedOrCondition, GuardedOrConditionFixed, 2)]
        [InlineData(GuardedWithThrow, GuardedWithThrowFixed)]
        [InlineData(GuardedNestedDictionaryAccess, GuardedNestedDictionaryAccessFixed)]
        [InlineData(GuardedTernary, GuardedTernaryTernaryFixed)]
        [InlineData(GuardedTernaryTernarySquared, GuardedTernarySquaredFixed, 2)]
        [InlineData(GuardedWithKeyLiteral, GuardedWithKeyLiteralFixed, 2)]
        [InlineData(GuardedAddBeforeUsage, GuardedAddBeforeUsageFixed, 3)]
        [InlineData(GuardedIndexerSetBeforeUsage, GuardedIndexerSetBeforeUsageFixed, 3)]
        [InlineData(GuardedIndexerPostIncrement, GuardedIndexerPostIncrementFixed)]
        [InlineData(GuardedIndexerPreIncrement, GuardedIndexerPreIncrementFixed)]
        [InlineData(GuardedIndexerInSimpleAssignment, GuardedIndexerInSimpleAssignmentFixed)]
        [InlineData(GuardedIndexerInCompoundAssignment, GuardedIndexerInCompoundAssignmentFixed)]
        [InlineData(GuardedKeyInSimpleAssignment, GuardedKeyInSimpleAssignmentFixed)]
        [InlineData(GuardedInlineVariable, GuardedInlineVariableFixed)]
        [InlineData(GuardedInlineVariable2, GuardedInlineVariable2Fixed)]
        [InlineData(GuardedReturnIdentifierUsed, GuardedReturnIdentifierUsedFixed)]
        public Task ShouldReportDiagnostic(string codeSnippet, string fixedCodeSnippet, int additionalLocations = 1)
        {
            string testCode = CreateCSharpCode(codeSnippet);
            string fixedCode = CreateCSharpCode(fixedCodeSnippet);
            var diagnostic = VerifyCS.Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic);
            for (int i = 0; i < additionalLocations + 1; i++)
            {
                diagnostic = diagnostic.WithLocation(i);
            }

            return new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic },
                DisabledDiagnostics = { PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId }
            }.RunAsync();
        }

        [Theory]
        [InlineData(InvalidModifiedBeforeUse)]
        [InlineData(InvalidAddBeforeUse)]
        [InlineData(InvalidRemoveBeforeUse)]
        [InlineData(InvalidNonIDictionary)]
        [InlineData(InvalidNotGuardedByContainsKey)]
        [InlineData(InvalidModifyReference)]
        [InlineData(InvalidDifferentKey)]
        [InlineData(InvalidKeyChangedSimple)]
        [InlineData(InvalidKeyChangedCompound)]
        [InlineData(InvalidKeyChangedIncrement)]
        [InlineData(InvalidOtherLiteral)]
        [InlineData(InvalidEntryModified)]
        [InlineData(InvalidEntryModifiedCoalesceAssignment, LanguageVersion.CSharp8)]
        [InlineData(InvalidNotGuarded)]
        [InlineData(InvalidArrayIndexerChanged)]
        [InlineData(InvalidKeyChangedInCondition)]
        [InlineData(InvalidKeyChangedAfterAdd)]
        [InlineData(InvalidComplexPostIncrement)]
        public Task ShouldNotReportDiagnostic(string codeSnippet, LanguageVersion version = LanguageVersion.Default)
        {
            string testCode = CreateCSharpCode(codeSnippet);

            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                DisabledDiagnostics = { PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId }
            };
            if (version != default)
                test.LanguageVersion = version;

            return test.RunAsync();
        }

        [Theory]
        [InlineData(VbGuardedPrintValue, VbGuardedPrintValueFixed, 2)]
        [InlineData(VbGuardedReturn, VbGuardedReturnFixed)]
        [InlineData(VbGuardedWithUnrelatedStatements, VbGuardedWithUnrelatedStatementsFixed)]
        [InlineData(VbGuardedAndCondition, VbGuardedAndConditionFixed, 2)]
        [InlineData(VbGuardedOrCondition, VbGuardedOrConditionFixed, 2)]
        [InlineData(VbGuardedNestedDictionaryAccess, VbGuardedNestedDictionaryAccessFixed)]
        [InlineData(VbGuardedTernary, VbGuardedTernaryFixed)]
        [InlineData(VbGuardedTernarySquared, VbGuardedTernarySquaredFixed, 2)]
        [InlineData(VbGuardedWithKeyLiteral, VbGuardedWithKeyLiteralFixed, 2)]
        [InlineData(VbGuardedWithKeyLiteralAndAccessWithExclamation, VbGuardedWithKeyLiteralAndAccessWithExclamationFixed)]
        [InlineData(VbGuardedAddBeforeUsage, VbGuardedAddBeforeUsageFixed, 3)]
        [InlineData(VbGuardedIndexerSetBeforeUsage, VbGuardedIndexerSetBeforeUsageFixed, 3)]
        [InlineData(VbGuardedIndexerInSimpleAssignment, VbGuardedIndexerInSimpleAssignmentFixed)]
        [InlineData(VbGuardedIndexerInCompoundAssignment, VbGuardedIndexerInCompoundAssignmentFixed)]
        [InlineData(VbGuardedKeyInSimpleAssignment, VbGuardedKeyInSimpleAssignmentFixed)]
        [InlineData(VbGuardedInlineVariable, VbGuardedInlineVariableFixed)]
        [InlineData(VbGuardedInlineVariable2, VbGuardedInlineVariable2Fixed)]
        [InlineData(VbGuardedReturnIdentifierUsed, VbGuardedReturnIdentifierUsedFixed)]
        public Task VbShouldReportDiagnostic(string codeSnippet, string fixedCodeSnippet, int additionalLocations = 1)
        {
            string testCode = CreateVbCode(codeSnippet);
            string fixedCode = CreateVbCode(fixedCodeSnippet);
            var diagnostic = VerifyVB.Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic);
            for (int i = 0; i < additionalLocations + 1; i++)
            {
                diagnostic = diagnostic.WithLocation(i);
            }

            return new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic },
                DisabledDiagnostics = { PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId }
            }.RunAsync();
        }

        [Theory]
        [InlineData(VbInvalidModifiedBeforeUse)]
        [InlineData(VbInvalidRemoveBeforeUse)]
        [InlineData(VbInvalidNonIDictionary)]
        [InlineData(VbInvalidNotGuardedByContainsKey)]
        [InlineData(VbInvalidModifyReference)]
        [InlineData(VbInvalidDifferentKey)]
        [InlineData(VbInvalidKeyChangedSimple)]
        [InlineData(VbInvalidKeyChangedCompound)]
        [InlineData(VbInvalidOtherLiteral)]
        [InlineData(VbInvalidNotGuarded)]
        [InlineData(VbInvalidArrayIndexerChanged)]
        [InlineData(VbInvalidKeyChangedAfterAdd)]
        public Task VbShouldNotReportDiagnostic(string codeSnippet)
        {
            string testCode = CreateVbCode(codeSnippet);

            return new VerifyVB.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                DisabledDiagnostics = { PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId }
            }.RunAsync();
        }

        private static readonly string[] s_DictionaryRefs = {
            "local",
            "parameter",
            "memberField",
            "MemberProperty",
            "staticField",
            "StaticProperty",
            "localArray[0]",
            "localArray[1]",
            "memberFieldArray[1]"
        };

        public static IEnumerable<object[]> GetDictionaryCombinations()
        {
            return from first in s_DictionaryRefs from second in s_DictionaryRefs select new object[] { first, second };
        }

        [Theory]
        [MemberData(nameof(GetDictionaryCombinations))]
        public Task TestDictionaryReferences(string containsKeyRef, string indexerRef)
        {
            string testCode = CreateCSharpCode($$"""
            string key = "key";
            var local = new Dictionary<string, int>();
            var localArray = new [] {new Dictionary<string, int>(), new Dictionary<string, int>()};

            if ({|#0:{{containsKeyRef}}.ContainsKey(key)|})
                return {|#1:{{indexerRef}}[key]|};

            return 0;
""");
            if (containsKeyRef != indexerRef)
            {
                return new VerifyCS.Test
                {
                    TestCode = testCode,
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }.RunAsync();
            }

            string fixedCode = CreateCSharpCode($$"""
            string key = "key";
            var local = new Dictionary<string, int>();
            var localArray = new [] {new Dictionary<string, int>(), new Dictionary<string, int>()};

            if ({{containsKeyRef}}.TryGetValue(key, out int value))
                return value;

            return 0;
""");

            var diagnostic = VerifyCS.Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic).WithLocation(0).WithLocation(1);
            return new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetDictionaryCombinations))]
        public Task VbTestDictionaryReferences(string containsKeyRef, string indexerRef)
        {
            containsKeyRef = containsKeyRef.Replace('[', '(').Replace(']', ')');
            indexerRef = indexerRef.Replace('[', '(').Replace(']', ')');
            string testCode = CreateVbCode($$"""
            Dim key = "key"
            Dim local = New Dictionary(Of String, Integer)
            Dim localArray = New Dictionary(Of String, Integer)() {New Dictionary(Of String, Integer)(), New Dictionary(Of String, Integer)()}
            If {|#0:{{containsKeyRef}}.ContainsKey(key)|} Then
                Return {|#1:{{indexerRef}}(key)|}
            End If

            Return 0
""");
            if (containsKeyRef != indexerRef)
            {
                return new VerifyVB.Test
                {
                    TestCode = testCode,
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }.RunAsync();
            }

            string fixedCode = CreateVbCode($$"""
            Dim key = "key"
            Dim local = New Dictionary(Of String, Integer)
            Dim localArray = New Dictionary(Of String, Integer)() {New Dictionary(Of String, Integer)(), New Dictionary(Of String, Integer)()}

            Dim value As Integer = Nothing

            If {{containsKeyRef}}.TryGetValue(key, value) Then
                Return value
            End If

            Return 0
""");

            var diagnostic = VerifyVB.Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic).WithLocation(0).WithLocation(1);
            return new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Theory, CombinatorialData]
        [WorkItem(6022, "https://github.com/dotnet/roslyn-analyzers/issues/6022")]
        public async Task TestVarPreference(bool preferVar)
        {
            await new VerifyCS.Test
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
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

        [Theory]
        [InlineData("disable")]
        [InlineData("enable")]
        [InlineData("enable warnings")]
        [InlineData("enable annotations")]
        public async Task TestReferenceNullableHandling(string nullableMode)
        {
            var useNullable = nullableMode is "enable" or "enable annotations";
            await new VerifyCS.Test
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState =
                {
                    Sources =
                    {
                        $@"
#nullable {nullableMode}
using System;
using System.Collections.Generic;

class C
{{
    void Reference(string key)
    {{
        var objects = new Dictionary<string, object>();
        if ([|objects.ContainsKey(key)|])
            Console.WriteLine(objects[key]{(nullableMode == "enable" ? "!" : "")});
    }}

    void Value(string key)
    {{
        var ints = new Dictionary<string, int>();
        if ([|ints.ContainsKey(key)|])
            Console.WriteLine(ints[key]);
    }}
}}"
                    }
                },
                FixedCode = $@"
#nullable {nullableMode}
using System;
using System.Collections.Generic;

class C
{{
    void Reference(string key)
    {{
        var objects = new Dictionary<string, object>();
        if (objects.TryGetValue(key, out object{(useNullable ? "?" : "")} value))
            Console.WriteLine(value{(nullableMode == "enable" ? "!" : "")});
    }}

    void Value(string key)
    {{
        var ints = new Dictionary<string, int>();
        if (ints.TryGetValue(key, out int value))
            Console.WriteLine(value);
    }}
}}",
                LanguageVersion = LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Fact]
        [WorkItem(6589, "https://github.com/dotnet/roslyn-analyzers/issues/6589")]
        public Task MultipleConditionsInIfStatement()
        {
            const string code = @"
using System.Collections.Generic;

namespace UnitTests {
    class Program {
        public void Test(int key, string text) {
            var dictionary = new Dictionary<int, string>();
            if({|#0:dictionary.ContainsKey(key)|} && !string.IsNullOrEmpty(text)) {
                text = {|#1:dictionary[key]|};
            }          
        } 
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;

namespace UnitTests {
    class Program {
        public void Test(int key, string text) {
            var dictionary = new Dictionary<int, string>();
            if(dictionary.TryGetValue(key, out string value) && !string.IsNullOrEmpty(text)) {
                text = value;
            }          
        } 
    }
}";
            var diagnostic = VerifyCS
                .Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic)
                .WithLocation(0)
                .WithLocation(1);

            return VerifyCS.VerifyCodeFixAsync(code, diagnostic, fixedCode);
        }

        [Fact]
        [WorkItem(7098, "https://github.com/dotnet/roslyn-analyzers/issues/7098")]
        public async Task CodeFixPreservesStyle()
        {
            const string code =
                """
                using System.Collections.Generic;

                namespace UnitTests {
                    class Program {
                        Dictionary<int, string> dictionary = new();
                        public void Test(int key, string text) {
                            if({|#0:this.dictionary.ContainsKey(key)|} && !string.IsNullOrEmpty(text)) {
                                text = {|#1:dictionary[key]|};
                            }          
                        } 
                    }
                }
                """;
            const string fixedCode =
                """
                using System.Collections.Generic;

                namespace UnitTests {
                    class Program {
                        Dictionary<int, string> dictionary = new();
                        public void Test(int key, string text) {
                            if(this.dictionary.TryGetValue(key, out string value) && !string.IsNullOrEmpty(text)) {
                                text = value;
                            }          
                        } 
                    }
                }
                """;

            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp9,
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic)
                        .WithLocation(0)
                        .WithLocation(1),
                },
            }.RunAsync();
        }

        [Fact, WorkItem(7217, "https://github.com/dotnet/roslyn-analyzers/issues/7217")]
        public Task WhenIndexerInIndirectContainsKeyClause_NoDiagnostic()
        {
            const string code = """
                                using System.Collections.Generic;
                                using System.Linq;

                                class Program
                                {
                                    private Dictionary<string, List<string>> _dictionary = new Dictionary<string, List<string>>();
                                
                                    public void Test(string key)
                                    {
                                        List<string> data = new List<string>();
                                
                                        if (_dictionary.ContainsKey(key))
                                        {
                                            DbContext context = new DbContext();
                                            data = context.LoadData(key);
                                            if (data != null && data.Any())
                                            {
                                                var x = _dictionary[key];
                                            }
                                        }
                                    }
                                
                                    public class DbContext
                                    {
                                        public List<string> LoadData(string key) => new List<string>();
                                    }
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7295, "https://github.com/dotnet/roslyn-analyzers/issues/7295")]
        public Task WhenDifferentPropertyInstanceContainingDictionary_NoDiagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;

                                class Test
                                {
                                    private Dictionary<int, int> PermissionsData => throw null;
                                
                                    void M(int objId) {
                                        Test otherTest = new Test();
                                        if (PermissionsData.ContainsKey(objId))
                                        {
                                            Console.WriteLine(otherTest.PermissionsData[objId]);
                                        }
                                    }
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7295, "https://github.com/dotnet/roslyn-analyzers/issues/7295")]
        public Task WhenDifferentFieldInstanceContainingDictionary_NoDiagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;

                                class Test
                                {
                                    private Dictionary<int, int> permissionsData;
                                
                                    void M(int objId) {
                                        Test otherTest = new Test();
                                        if (permissionsData.ContainsKey(objId))
                                        {
                                            Console.WriteLine(otherTest.permissionsData[objId]);
                                        }
                                    }
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7295, "https://github.com/dotnet/roslyn-analyzers/issues/7295")]
        public Task WhenDifferentLocalInstancesContainingDictionary_NoDiagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;

                                class Test
                                {
                                    private Dictionary<int, int> permissionsData;
                                
                                    void M(int objId) {
                                        Test test1 = new Test();
                                        Test test2 = new Test();
                                        if (test1.permissionsData.ContainsKey(objId))
                                        {
                                            Console.WriteLine(test2.permissionsData[objId]);
                                        }
                                    }
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7295, "https://github.com/dotnet/roslyn-analyzers/issues/7295")]
        public Task WhenReferencingSameInstanceWithThisQualifier_Diagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;

                                class Test
                                {
                                    private Dictionary<int, int> permissionsData;
                                
                                    void M(int objId) {
                                        if ({|#0:permissionsData.ContainsKey(objId)|})
                                        {
                                            Console.WriteLine({|#1:this.permissionsData[objId]|});
                                        }
                                    }
                                }
                                """;

            const string fixedCode = """
                                using System;
                                using System.Collections.Generic;

                                class Test
                                {
                                    private Dictionary<int, int> permissionsData;
                                
                                    void M(int objId) {
                                        if (permissionsData.TryGetValue(objId, out int value))
                                        {
                                            Console.WriteLine(value);
                                        }
                                    }
                                }
                                """;

            var result = new DiagnosticResult(PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueDiagnostic)
                    .WithLocation(0)
                    .WithLocation(1);

            return VerifyCS.VerifyCodeFixAsync(code, result, fixedCode);
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