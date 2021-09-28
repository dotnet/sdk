// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferDictionaryContainsMethods,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferDictionaryContainsMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicPreferDictionaryContainsMethods,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicPreferDictionaryContainsMethodsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferDictionaryContainsMethodsTests
    {
        #region Test Data
        public static IEnumerable<object[]> DictionaryKeysExpressions
        {
            get
            {
                yield return new object[] { "dictionary.Keys" };
                yield return new object[] { "(dictionary.Keys)" };
                yield return new object[] { "((dictionary.Keys))" };
            }
        }

        public static IEnumerable<object[]> DictionaryValuesExpressions
        {
            get
            {
                yield return new object[] { "dictionary.Values" };
                yield return new object[] { "(dictionary.Values)" };
                yield return new object[] { "((dictionary.Values))" };
            }
        }
        #endregion

        #region Expected Diagnostic
        [Theory]
        [MemberData(nameof(DictionaryKeysExpressions))]
        public async Task IDictionary_Keys_Contains_ReportsDiagnostic_CSAsync(string dictionaryKeys)
        {
            const string declaration = @"IDictionary<string, int> dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, @$"bool wutzHuh = {{|#0:{dictionaryKeys}.Contains(""Wumphed"")|}};");
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
        public async Task IDictionary_Keys_Contains_ReportsDiagnostic_VBAsync()
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

        [Theory]
        [MemberData(nameof(DictionaryKeysExpressions))]
        public async Task BuiltInDictionary_Keys_Contains_ReportsDiagnostic_CSAsync(string dictionaryKeys)
        {
            const string declaration = @"var dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, $@"bool isPhleeb = {{|#0:{dictionaryKeys}.Contains(""bizzurf"")|}};");
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
        public async Task BuiltInDictionary_Keys_Contains_ReportsDiagnostic_VBAsync()
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

        [Theory]
        [MemberData(nameof(DictionaryValuesExpressions))]
        public async Task BuiltInDictionary_Values_Contains_ReportsDiagnostic_CSAsync(string dictionaryValues)
        {
            const string declaration = "var dictionary = new Dictionary<string, int>();";
            string testCode = CreateCSSource(declaration, $@"bool wasFlobbed = {{|#0:{dictionaryValues}.Contains(75)|}};");
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
        public async Task BuiltInDictionary_Values_Contains_ReportsDiagnostic_VBAsync()
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

        [Theory]
        [MemberData(nameof(DictionaryKeysExpressions))]
        public async Task ExplicitContainsKey_WhenTypedAsIDictionary_ReportsDiagnostic_CSAsync(string dictionaryKeys)
        {
            const string declaration = "IDictionary<string, int> dictionary = new TestDictionary();";
            string testCode = CreateCSSource(declaration, $@"bool tugBlug = {{|#0:{dictionaryKeys}.Contains(""bricklebrit"")|}};");
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
        public async Task ExplicitContainsKey_WhenTypedAsIDictionary_ReportsDiagnostic_VBAsync()
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

        [Theory]
        [MemberData(nameof(DictionaryKeysExpressions))]
        public async Task IEnumerableKeyCollection_ReportsDiagnostic_CSAsync(string dictionaryKeys)
        {
            const string declaration = "var dictionary = new TestDictionary();";
            string testCode = CreateCSSource(declaration, $@"bool b = {{|#0:{dictionaryKeys}.Contains(""bracklebrat"")|}};");
            string fixedCode = CreateCSSource(declaration, @"bool b = dictionary.ContainsKey(""bracklebrat"");");
            var diagnostic = VerifyCS.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("TestDictionary");

            await new VerifyCS.Test
            {
                TestState = { Sources = { testCode, CSCustomFacadeCollectionsDictionarySource, CSIEnumerableFacadeCollectionsSource } },
                FixedState = { Sources = { fixedCode, CSCustomFacadeCollectionsDictionarySource, CSIEnumerableFacadeCollectionsSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task IEnumerableKeyCollection_ReportsDiagnostic_VBAsync()
        {
            const string declaration = "Dim dictionary = New TestDictionary()";
            string testCode = CreateVBSource(declaration, @"Dim b = {|#0:dictionary.Keys.Contains(""bracklebrat"")|}");
            string fixedCode = CreateVBSource(declaration, @"Dim b = dictionary.ContainsKey(""bracklebrat"")");
            var diagnostic = VerifyVB.Diagnostic(ContainsKeyRule)
                .WithLocation(0)
                .WithArguments("TestDictionary");

            await new VerifyVB.Test
            {
                TestState = { Sources = { testCode, VBCustomFacadeCollectionsDictionarySource, VBIEnumerableFacadeCollectionsSource } },
                FixedState = { Sources = { fixedCode, VBCustomFacadeCollectionsDictionarySource, VBIEnumerableFacadeCollectionsSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DictionaryValuesExpressions))]
        public async Task IEnumerableValueCollection_ReportsDiagnostic_CSAsync(string dictionaryValues)
        {
            const string declaration = "var dictionary = new TestDictionary();";
            string testCode = CreateCSSource(declaration, $@"bool b = {{|#0:{dictionaryValues}.Contains(314159)|}};");
            string fixedCode = CreateCSSource(declaration, @"bool b = dictionary.ContainsValue(314159);");
            var diagnostic = VerifyCS.Diagnostic(ContainsValueRule)
                .WithLocation(0)
                .WithArguments("TestDictionary");

            await new VerifyCS.Test
            {
                TestState = { Sources = { testCode, CSCustomFacadeCollectionsDictionarySource, CSIEnumerableFacadeCollectionsSource } },
                FixedState = { Sources = { fixedCode, CSCustomFacadeCollectionsDictionarySource, CSIEnumerableFacadeCollectionsSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task IEnumerableValueCollection_ReportsDiagnostic_VBAsync()
        {
            const string declaration = "Dim dictionary = New TestDictionary()";
            string testCode = CreateVBSource(declaration, @"Dim b = {|#0:dictionary.Values.Contains(314159)|}");
            string fixedCode = CreateVBSource(declaration, @"Dim b = dictionary.ContainsValue(314159)");
            var diagnostic = VerifyVB.Diagnostic(ContainsValueRule)
                .WithLocation(0)
                .WithArguments("TestDictionary");

            await new VerifyVB.Test
            {
                TestState = { Sources = { testCode, VBCustomFacadeCollectionsDictionarySource, VBIEnumerableFacadeCollectionsSource } },
                FixedState = { Sources = { fixedCode, VBCustomFacadeCollectionsDictionarySource, VBIEnumerableFacadeCollectionsSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }
        #endregion

        #region No Diagnostic
        [Fact]
        public async Task IDictionary_Values_Contains_NoDiagnostic_CSAsync()
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
        public async Task IDictionary_Values_Contains_NoDiagnostic_VBAsync()
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
        public async Task ExplicitContainsKey_Keys_Contains_NoDiagnostic_CSAsync()
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
        public async Task ExplicitContainsKey_Keys_Contains_NoDiagnostic_VBAsync()
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

        [Theory]
        [InlineData(CSFacadeCollectionContainsWithWrongArgumentTypeSource, @"dictionary.Keys.Contains(29)")]
        [InlineData(CSFacadeCollectionContainsWithWrongArgumentTypeSource, @"dictionary.Values.Contains(""RuhRoh"")")]
        [InlineData(CSFacadeCollectionContainsWithBaseTypeArgumentSource, @"dictionary.Keys.Contains(new object())")]
        [InlineData(CSFacadeCollectionContainsWithBaseTypeArgumentSource, @"dictionary.Values.Contains(new object())")]
        public async Task ContainsArgument_WrongType_NoDiagnostic_CSAsync(string facadeCollectionSource, string containsInvocation)
        {
            string testCode = CreateCSSourceWithoutLinq(
                @"var dictionary = new TestDictionary();",
                $@"bool b = {containsInvocation};");

            await new VerifyCS.Test
            {
                TestState = { Sources = { testCode, CSCustomFacadeCollectionsDictionarySource, facadeCollectionSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Theory]
        [InlineData(VBFacadeCollectionContainsWithWrongArgumentTypeSource, @"dictionary.Keys.Contains(29)")]
        [InlineData(VBFacadeCollectionContainsWithWrongArgumentTypeSource, @"dictionary.Values.Contains(""RuhRoh"")")]
        [InlineData(VBFacadeCollectionContainsWithBaseTypeArgumentSource, @"dictionary.Keys.Contains(New Object())")]
        [InlineData(VBFacadeCollectionContainsWithBaseTypeArgumentSource, @"dictionary.Values.Contains(New Object())")]
        public async Task ContainsArgument_WrongType_NoDiagnostic_VBAsync(string facadeCollectionSource, string containsInvocation)
        {
            string testCode = CreateVBSourceWithoutLinq(
                @"Dim dictionary = New TestDictionary()",
                $@"Dim b = {containsInvocation}");

            await new VerifyVB.Test
            {
                TestState = { Sources = { testCode, VBCustomFacadeCollectionsDictionarySource, facadeCollectionSource } },
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

        private static string CreateCSSourceWithoutLinq(params string[] statements)
        {
            string body = @"
using System;
using System.Collections.Generic;

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

        private static string CreateVBSourceWithoutLinq(params string[] statements)
        {
            string body = @"
Imports System
Imports System.Collections.Generic

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

        /// <summary>
        /// Source code that defines an IDictionary(string, int) implementation of type 'TestDictionary' that exposes Keys and Values 
        /// properties of type 'KeyCollection' and 'ValueCollection', respectively.
        /// The corrasponding properties on IDictionary`2 are implemented explicitly.
        /// </summary>
        private const string CSCustomFacadeCollectionsDictionarySource = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace Testopolis
{
    public class TestDictionary : IDictionary<string, int>
    {
        public KeyCollection Keys { get; }

        public ValueCollection Values { get; }

        ICollection<string> IDictionary<string, int>.Keys { get; }

        ICollection<int> IDictionary<string, int>.Values { get; }

        public bool ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public bool ContainsValue(int value)
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

        /// <summary>
        /// Defines 'KeyCollection' and 'ValueCollection' types that implement IEnumerable`1 but not ICollection`1.
        /// </summary>
        private const string CSIEnumerableFacadeCollectionsSource = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace Testopolis
{
    public class KeyCollection : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class ValueCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
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

        private const string CSFacadeCollectionContainsWithWrongArgumentTypeSource = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace Testopolis
{
    public class KeyCollection : IEnumerable<string>
    {
        public bool Contains(int wrongType)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<string> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class ValueCollection : IEnumerable<int>
    {
        public bool Contains(string wrongType)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<int> GetEnumerator()
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

        private const string CSFacadeCollectionContainsWithBaseTypeArgumentSource = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace Testopolis
{
    public class KeyCollection : IEnumerable<string>
    {
        public bool Contains(object tooGeneral)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<string> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class ValueCollection : IEnumerable<int>
    {
        public bool Contains(object tooGeneral)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<int> GetEnumerator()
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

        private const string VBCustomFacadeCollectionsDictionarySource = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Namespace Testopolis
    Public Class TestDictionary : Implements IDictionary(Of String, Integer)

        Public ReadOnly Property Keys As KeyCollection
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Public ReadOnly Property Values As ValueCollection
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Private ReadOnly Property ICollection_Keys As ICollection(Of String) Implements IDictionary(Of String, Integer).Keys
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Private ReadOnly Property ICollection_Values As ICollection(Of Integer) Implements IDictionary(Of String, Integer).Values
            Get
                Throw New NotImplementedException()
            End Get
        End Property

        Default Public Property Item(key As String) As Integer Implements IDictionary(Of String, Integer).Item
            Get
                Throw New NotImplementedException()
            End Get
            Set(value As Integer)
                Throw New NotImplementedException()
            End Set
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

        Public Function ContainsKey(key As String) As Boolean Implements IDictionary(Of String, Integer).ContainsKey
            Throw New NotImplementedException()
        End Function

        Public Function ContainsValue(value As Integer) As Boolean
            Throw New NotImplementedException()
        End Function

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
End Namespace
";

        private const string VBIEnumerableFacadeCollectionsSource = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Namespace Testopolis
    Public Class KeyCollection : Implements IEnumerable(Of String)

        Public Function GetEnumerator() As IEnumerator(Of String) Implements IEnumerable(Of String).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class

    Public Class ValueCollection : Implements IEnumerable(Of Integer)

        Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace
";

        private const string VBFacadeCollectionContainsWithWrongArgumentTypeSource = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Namespace Testopolis

    Public Class KeyCollection : Implements IEnumerable(Of String)

        Public Function Contains(wrongType As Integer) As Boolean
            Throw New NotImplementedException()
        End Function

        Public Function GetEnumerator() As IEnumerator(Of String) Implements IEnumerable(Of String).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class

    Public Class ValueCollection : Implements IEnumerable(Of Integer)

        Public Function Contains(wrongType As String) As Boolean
            Throw New NotImplementedException()
        End Function

        Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace
";

        private const string VBFacadeCollectionContainsWithBaseTypeArgumentSource = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Namespace Testopolis

    Public Class KeyCollection : Implements IEnumerable(Of String)

        Public Function Contains(tooGeneral As Object) As Boolean
            Throw New NotImplementedException()
        End Function

        Public Function GetEnumerator() As IEnumerator(Of String) Implements IEnumerable(Of String).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class

    Public Class ValueCollection : Implements IEnumerable(Of Integer)

        Public Function Contains(tooGeneral As Object) As Boolean
            Throw New NotImplementedException()
        End Function

        Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
            Throw New NotImplementedException()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace
";

        private static DiagnosticDescriptor ContainsKeyRule => PreferDictionaryContainsMethods.ContainsKeyRule;
        private static DiagnosticDescriptor ContainsValueRule => PreferDictionaryContainsMethods.ContainsValueRule;
        #endregion
    }
}
