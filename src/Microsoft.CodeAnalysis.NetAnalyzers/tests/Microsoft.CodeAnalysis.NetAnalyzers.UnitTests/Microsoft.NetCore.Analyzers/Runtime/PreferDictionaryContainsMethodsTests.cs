// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryContainsMethods,
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryContainsMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryContainsMethods,
    Microsoft.NetCore.Analyzers.Runtime.PreferDictionaryContainsMethodsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferDictionaryContainsMethodsTests
    {
        #region Expected Diagnostic
        [Fact]
        public async Task IDictionary_Keys_Contains_ReportsDiagnostic_CS()
        {
            const string declaration = @"IDictionary<string, int> dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, @"bool wutzHuh = {|#0:dictionary.Keys.Contains(""Wumphed"")|};");
            string fixedCode = CreateCSSource(declaration, @"bool wutzHuh = dictionary.ContainsKey(""Wumphed"");");
            var diagnostic = VerifyCS.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("IDictionary");

            await new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task IDictionary_Keys_Contains_ReportsDiagnostic_VB()
        {
            const string declaration = @"Dim dictionary As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()";
            string testCode = CreateVBSource(declaration, @"Dim wutzHuh = {|#0:dictionary.Keys.Contains(""Wumphed"")|}");
            string fixedCode = CreateVBSource(declaration, @"Dim wutzHuh = dictionary.ContainsKey(""Wumphed"")");
            var diagnostic = VerifyVB.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("IDictionary");

            await new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task BuiltInDictionary_Keys_Contains_ReportsDiagnostic_CS()
        {
            const string declaration = @"var dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, @"bool isPhleeb = {|#0:dictionary.Keys.Contains(""bizzurf"")|};");
            string fixedCode = CreateCSSource(declaration, @"bool isPhleeb = dictionary.ContainsKey(""bizzurf"");");
            var diagnostic = VerifyCS.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("Dictionary");

            await new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task BuiltInDictionary_Keys_Contains_ReportsDiagnostic_VB()
        {
            const string declaration = "Dim dictionary = New Dictionary(Of String, Integer)()";
            string testCode = CreateVBSource(declaration, @"Dim isPhleeb = {|#0:dictionary.Keys.Contains(""bizzurf"")|}");
            string fixedCode = CreateVBSource(declaration, @"Dim isPhleeb = dictionary.ContainsKey(""bizzurf"")");
            var diagnostic = VerifyVB.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("Dictionary");

            await new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task BuiltInDictionary_Values_Contains_ReportsDiagnostic_CS()
        {
            const string declaration = "var dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, @"bool wasFlobbed = {|#0:dictionary.Values.Contains(75)|};");
            string fixedCode = CreateCSSource(declaration, @"bool wasFlobbed = dictionary.ContainsValue(75);");
            var diagnostic = VerifyCS.Diagnostic(ContainsValueRule)
                .WithLocation(0)
                .WithArguments("Dictionary");

            await new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task BuiltInDictionary_Values_Contains_ReportsDiagnostic_VB()
        {
            const string declaration = "Dim dictionary = New Dictionary(Of String, Integer)()";
            string testCode = CreateVBSource(declaration, @"Dim wasFlobbed = {|#0:dictionary.Values.Contains(75)|}");
            string fixedCode = CreateVBSource(declaration, @"Dim wasFlobbed = dictionary.ContainsValue(75)");
            var diagnostic = VerifyVB.Diagnostic(ContainsValueRule)
                .WithLocation(0)
                .WithArguments("Dictionary");

            await new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task ExplicitContainsKey_WhenTypedAsIDictionary_ReportsDiagnostic_CS()
        {
            const string declaration = "IDictionary<string, int> dictionary = new TestDictionary();";
            string testCode = CreateCSSource(declaration, @"bool tugBlug = {|#0:dictionary.Keys.Contains(""bricklebrit"")|};");
            string fixedCode = CreateCSSource(declaration, @"bool tugBlug = dictionary.ContainsKey(""bricklebrit"");");
            var diagnostic = VerifyCS.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("IDictionary");

            await new VerifyCS.Test
            {
                TestState = { Sources = { testCode, CSExplicitContainsKeyDictionarySource } },
                FixedState = { Sources = { fixedCode, CSExplicitContainsKeyDictionarySource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task ExplicitContainsKey_WhenTypedAsIDictionary_ReportsDiagnostic_VB()
        {
            const string declaration = "Dim dictionary As IDictionary(Of String, Integer) = New TestDictionary()";
            string testCode = CreateVBSource(declaration, @"Dim tugBlug = {|#0:dictionary.Keys.Contains(""bricklebrit"")|}");
            string fixedCode = CreateVBSource(declaration, @"Dim tugBlug = dictionary.ContainsKey(""bricklebrit"")");
            var diagnostic = VerifyVB.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("IDictionary");

            await new VerifyVB.Test
            {
                TestState = { Sources = { testCode, VBExplicitContainsKeyDictionarySource } },
                FixedState = { Sources = { fixedCode, VBExplicitContainsKeyDictionarySource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }
        #endregion

        #region No Diagnostic
        [Fact]
        public async Task IDictionary_Values_Contains_NoDiagnostic_CS()
        {
            const string declaration = @"IDictionary<string, int> dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, @"bool urrp = dictionary.Values.Contains(49);");

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task IDictionary_Values_Contains_NoDiagnostic_VB()
        {
            const string declaration = @"Dim dictionary As IDictionary(Of String, Integer) = New Dictionary(Of String, Integer)()";
            string testCode = CreateVBSource(declaration, @"Dim urrp = dictionary.Values.Contains(49)");

            await new VerifyVB.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task ExplicitContainsKey_Keys_Contains_NoDiagnostic_CS()
        {
            string testCode = CreateCSSource(
                @"var dictionary = new TestDictionary();",
                @"bool buzzed = dictionary.Keys.Contains(""moofled"");");

            await new VerifyCS.Test
            {
                TestState = { Sources = { testCode, CSExplicitContainsKeyDictionarySource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task ExplicitContainsKey_Keys_Contains_NoDiagnostic_VB()
        {
            string testCode = CreateVBSource(
                @"Dim dictionary = New TestDictionary()",
                @"Dim buzzed = dictionary.Keys.Contains(""moofled"")");

            await new VerifyVB.Test
            {
                TestState = { Sources = { testCode, VBExplicitContainsKeyDictionarySource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }
        #endregion

        #region Helpers
        private static string CreateCSSource(params string[] statements)
        {
            string body = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Testopolis
{{
    public class WumphingWumbler
    {{
        public void MegaWumph()
        {{
            {0}
        }}
    }}
}}
";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, body, string.Join("\r\n            ", statements));
        }

        private static string CreateVBSource(params string[] statements)
        {
            string body = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Namespace Testopolis

    Public Class WumphingWumbler

        Public Sub MegaWumph()
            {0}
        End Sub
    End Class
End Namespace
";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, body, string.Join("\r\n            ", statements));
        }

        private const string CSExplicitContainsKeyDictionarySource = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Testopolis
{
    public class TestDictionary : IDictionary<string, int>
    {
        bool IDictionary<string, int>.ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public void Add(string key, int value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string key, out int value)
        {
            throw new NotImplementedException();
        }

        public int this[string key]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public ICollection<string> Keys { get; }
        public ICollection<int> Values { get; }

        public void Add(KeyValuePair<string, int> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<string, int> item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, int> item)
        {
            throw new NotImplementedException();
        }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
";

        private const string VBExplicitContainsKeyDictionarySource = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq

Namespace Testopolis
    Public Class TestDictionary : Implements IDictionary(Of String, Integer)

        Private Function ContainsKey(key As String) As Boolean Implements IDictionary(Of String, Integer).ContainsKey
            Throw New NotImplementedException()
        End Function

        Default Public Property Item(key As String) As Integer Implements IDictionary(Of String, Integer).Item
            Get
                Throw New NotImplementedException()
            End Get
            Set(value As Integer)
                Throw New NotImplementedException()
            End Set
        End Property

        Public ReadOnly Property Keys As ICollection(Of String) Implements IDictionary(Of String, Integer).Keys
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public ReadOnly Property Values As ICollection(Of Integer) Implements IDictionary(Of String, Integer).Values
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public ReadOnly Property Count As Integer Implements ICollection(Of KeyValuePair(Of String, Integer)).Count
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of KeyValuePair(Of String, Integer)).IsReadOnly
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public Sub Add(key As String, value As Integer) Implements IDictionary(Of String, Integer).Add
            Throw New NotImplementedException()
        End Sub

        Public Sub Add(item As KeyValuePair(Of String, Integer)) Implements ICollection(Of KeyValuePair(Of String, Integer)).Add
            Throw New NotImplementedException()
        End Sub

        Public Sub Clear() Implements ICollection(Of KeyValuePair(Of String, Integer)).Clear
            Throw New NotImplementedException()
        End Sub

        Public Sub CopyTo(array() As KeyValuePair(Of String, Integer), arrayIndex As Integer) Implements ICollection(Of KeyValuePair(Of String, Integer)).CopyTo
            Throw New NotImplementedException()
        End Sub

        Public Function Remove(key As String) As Boolean Implements IDictionary(Of String, Integer).Remove
            Throw New NotImplementedException()
        End Function

        Public Function Remove(item As KeyValuePair(Of String, Integer)) As Boolean Implements ICollection(Of KeyValuePair(Of String, Integer)).Remove
            Throw New NotImplementedException()
        End Function

        Public Function TryGetValue(key As String, ByRef value As Integer) As Boolean Implements IDictionary(Of String, Integer).TryGetValue
            Throw New NotImplementedException()
        End Function

        Public Function Contains(item As KeyValuePair(Of String, Integer)) As Boolean Implements ICollection(Of KeyValuePair(Of String, Integer)).Contains
            Throw New NotImplementedException()
        End Function

        Public Function GetEnumerator() As IEnumerator(Of KeyValuePair(Of String, Integer)) Implements IEnumerable(Of KeyValuePair(Of String, Integer)).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace";

        private static DiagnosticDescriptor ContainsKeyRule => PreferDictionaryContainsMethods.ContainsKeyRule;
        private static DiagnosticDescriptor ContainsValueRule => PreferDictionaryContainsMethods.ContainsValueRule;
        #endregion
    }
}
