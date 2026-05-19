// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer,
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer,
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixer>;

namespace Microsoft.NetCore.Analyzers.ImmutableCollections.UnitTests
{
    public class DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixerTests
    {
        public static readonly TheoryData<string> CollectionNames_Arity1 = new()
        {
            nameof(ImmutableArray),
            nameof(ImmutableHashSet),
            nameof(ImmutableList),
            nameof(ImmutableSortedSet)
        };

        public static readonly TheoryData<string> CollectionNames_Arity2 = new()
        {
            nameof(ImmutableDictionary),
            nameof(ImmutableSortedDictionary)
        };

        [Theory]
        [MemberData(nameof(CollectionNames_Arity1))]
        public async Task CA2009_Arity1_CSharpAsync(string collectionName)
        {
            var initial = $@"
using System.Collections.Generic;
using System.Collections.Immutable;

class C
{{
    public void M(IEnumerable<int> p1, List<int> p2, {collectionName}<int> p3)
    {{
        var a = [|p1.To{collectionName}().To{collectionName}()|];
        var b = [|p3.To{collectionName}()|];
        var c = [|{collectionName}.To{collectionName}({collectionName}.To{collectionName}(p1))|];
        var d = [|{collectionName}.To{collectionName}(p3)|];
    }}
}}";

            var expected = $@"
using System.Collections.Generic;
using System.Collections.Immutable;

class C
{{
    public void M(IEnumerable<int> p1, List<int> p2, {collectionName}<int> p3)
    {{
        var a = p1.To{collectionName}();
        var b = p3;
        var c = {collectionName}.To{collectionName}(p1);
        var d = p3;
    }}
}}";
            await VerifyCS.VerifyCodeFixAsync(initial, expected);
        }

        [Theory]
        [MemberData(nameof(CollectionNames_Arity1))]
        public async Task CA2009_Arity1_BasicAsync(string collectionName)
        {
            var initial = $@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Class C
	Public Sub M(p1 As IEnumerable(Of Integer), p2 As List(Of Integer), p3 As {collectionName}(Of Integer))
		Dim a = [|p1.To{collectionName}().To{collectionName}()|]
		Dim b = [|p3.To{collectionName}()|]
		Dim c = [|{collectionName}.To{collectionName}({collectionName}.To{collectionName}(p1))|]
		Dim d = [|{collectionName}.To{collectionName}(p3)|]
	End Sub
End Class";

            var expected = $@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Class C
	Public Sub M(p1 As IEnumerable(Of Integer), p2 As List(Of Integer), p3 As {collectionName}(Of Integer))
		Dim a = p1.To{collectionName}()
		Dim b = p3
		Dim c = {collectionName}.To{collectionName}(p1)
		Dim d = p3
	End Sub
End Class";
            await VerifyVB.VerifyCodeFixAsync(initial, expected);
        }

        [Theory]
        [MemberData(nameof(CollectionNames_Arity2))]
        public async Task CA2009_Arity2_CSharpAsync(string collectionName)
        {
            var initial = $@"
using System.Collections.Generic;
using System.Collections.Immutable;

class C
{{
    public void M(IEnumerable<KeyValuePair<int, int>> p1, List<KeyValuePair<int, int>> p2, {collectionName}<int, int> p3)
    {{
        var a = [|p1.To{collectionName}().To{collectionName}()|];
        var b = [|p3.To{collectionName}()|];
        var c = [|{collectionName}.To{collectionName}({collectionName}.To{collectionName}(p1))|];
        var d = [|{collectionName}.To{collectionName}(p3)|];
    }}
}}";

            var expected = $@"
using System.Collections.Generic;
using System.Collections.Immutable;

class C
{{
    public void M(IEnumerable<KeyValuePair<int, int>> p1, List<KeyValuePair<int, int>> p2, {collectionName}<int, int> p3)
    {{
        var a = p1.To{collectionName}();
        var b = p3;
        var c = {collectionName}.To{collectionName}(p1);
        var d = p3;
    }}
}}";
            await VerifyCS.VerifyCodeFixAsync(initial, expected);
        }

        [Theory]
        [MemberData(nameof(CollectionNames_Arity2))]
        public async Task CA2009_Arity2_BasicAsync(string collectionName)
        {
            var initial = $@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Class C
	Public Sub M(p1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)), p2 As List(Of KeyValuePair(Of Integer, Integer)), p3 As {collectionName}(Of Integer, Integer))
		Dim a = [|p1.To{collectionName}().To{collectionName}()|]
		Dim b = [|p3.To{collectionName}()|]
		Dim c = [|{collectionName}.To{collectionName}({collectionName}.To{collectionName}(p1))|]
		Dim d = [|{collectionName}.To{collectionName}(p3)|]
	End Sub
End Class";

            var expected = $@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Class C
	Public Sub M(p1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)), p2 As List(Of KeyValuePair(Of Integer, Integer)), p3 As {collectionName}(Of Integer, Integer))
		Dim a = p1.To{collectionName}()
		Dim b = p3
		Dim c = {collectionName}.To{collectionName}(p1)
		Dim d = p3
	End Sub
End Class";
            await VerifyVB.VerifyCodeFixAsync(initial, expected);
        }
    }
}