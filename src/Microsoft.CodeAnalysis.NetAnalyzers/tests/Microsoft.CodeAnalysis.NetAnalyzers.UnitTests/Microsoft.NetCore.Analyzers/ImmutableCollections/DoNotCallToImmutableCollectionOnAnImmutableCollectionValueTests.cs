// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer,
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer,
    Microsoft.NetCore.Analyzers.ImmutableCollections.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixer>;

namespace Microsoft.NetCore.Analyzers.ImmutableCollections.UnitTests
{
    public class DoNotCallToImmutableCollectionOnAnImmutableCollectionValueTests
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

        #region No Diagnostic Tests

        [Theory, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        [MemberData(nameof(CollectionNames_Arity1))]
        public async Task NoDiagnosticCases_Arity1(string collectionName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Collections.Generic;
using System.Collections.Immutable;
using static System.Collections.Immutable.{collectionName};

static class Extensions
{{
     public static {collectionName}<TSource> To{collectionName}<TSource>(this IEnumerable<TSource> items)
     {{
         return default({collectionName}<TSource>);
     }}

     public static {collectionName}<TSource> To{collectionName}<TSource>(this IEnumerable<TSource> items, IEqualityComparer<TSource> comparer)
     {{
         return default({collectionName}<TSource>);
     }}
}}

class C
{{
    public void M(IEnumerable<int> p1, List<int> p2, {collectionName}<int> p3, IEqualityComparer<int> comparer)
    {{
        // Allowed
        p1.To{collectionName}();
        p2.To{collectionName}();
        p3.To{collectionName}(comparer); // Potentially modifies the collection

        Extensions.To{collectionName}(p1);
        Extensions.To{collectionName}(p2);
        Extensions.To{collectionName}(p3, comparer); // Potentially modifies the collection

        // No dataflow
        IEnumerable<int> l1 = p3;
        l1.To{collectionName}();

        Extensions.To{collectionName}(l1);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Module Extensions
	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TSource)(items As IEnumerable(Of TSource)) As {collectionName}(Of TSource)
		Return Nothing
	End Function

	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TSource)(items As IEnumerable(Of TSource), comparer as IEqualityComparer(Of TSource)) As {collectionName}(Of TSource)
		Return Nothing
	End Function
End Module

Class C
	Public Sub M(p1 As IEnumerable(Of Integer), p2 As List(Of Integer), p3 As {collectionName}(Of Integer), comparer As IEqualityComparer(Of Integer))
		' Allowed
		p1.To{collectionName}()
		p2.To{collectionName}()
        p3.To{collectionName}(comparer) ' Potentially modifies the collection

        Extensions.To{collectionName}(p1)
        Extensions.To{collectionName}(p2)
        Extensions.To{collectionName}(p3, comparer) ' Potentially modifies the collection

		' No dataflow
		Dim l1 As IEnumerable(Of Integer) = p3
		l1.To{collectionName}()

        Extensions.To{collectionName}(l1)
	End Sub
End Class
");
        }

        [Theory, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        [MemberData(nameof(CollectionNames_Arity2))]
        public async Task NoDiagnosticCases_Arity2(string collectionName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Collections.Generic;
using System.Collections.Immutable;
using static System.Collections.Immutable.{collectionName};

static class Extensions
{{
     public static {collectionName}<TKey, TValue> To{collectionName}<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
     {{
         return default({collectionName}<TKey, TValue>);
     }}

    public static {collectionName}<TKey, TValue> To{collectionName}<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey> keyComparer)
    {{
         return default({collectionName}<TKey, TValue>);
    }}
}}

class C
{{
    public void M(IEnumerable<KeyValuePair<int, int>> p1, List<KeyValuePair<int, int>> p2, {collectionName}<int, int> p3, IEqualityComparer<int> keyComparer)
    {{
        // Allowed
        p1.To{collectionName}();
        p2.To{collectionName}();
        p3.To{collectionName}(keyComparer); // Potentially modifies the collection

        Extensions.To{collectionName}(p1);
        Extensions.To{collectionName}(p2);
        Extensions.To{collectionName}(p3, keyComparer); // Potentially modifies the collection

        // No dataflow
        IEnumerable<KeyValuePair<int, int>> l1 = p3;
        l1.To{collectionName}();

        Extensions.To{collectionName}(l1);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Module Extensions
	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TKey, TValue)(items As IEnumerable(Of KeyValuePair(Of TKey, TValue))) As {collectionName}(Of TKey, TValue)
		Return Nothing
	End Function

	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TKey, TValue)(items As IEnumerable(Of KeyValuePair(Of TKey, TValue)), keyComparer As IEqualityComparer(Of TKey)) As {collectionName}(Of TKey, TValue)
		Return Nothing
	End Function
End Module

Class C
	Public Sub M(p1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)), p2 As List(Of KeyValuePair(Of Integer, Integer)), p3 As {collectionName}(Of Integer, Integer), keyComparer As IEqualityComparer(Of Integer))
		' Allowed
		p1.To{collectionName}()
		p2.To{collectionName}()
        p3.To{collectionName}(keyComparer) ' Potentially modifies the collection

        Extensions.To{collectionName}(p1)
        Extensions.To{collectionName}(p2)
        Extensions.To{collectionName}(p3, keyComparer) ' Potentially modifies the collection

		' No dataflow
		Dim l1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)) = p3
		l1.To{collectionName}()

        Extensions.To{collectionName}(l1)
	End Sub
End Class
");
        }

        #endregion

        #region Diagnostic Tests

        [Theory]
        [MemberData(nameof(CollectionNames_Arity1))]
        public async Task DiagnosticCases_Arity1(string collectionName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Collections.Generic;
using System.Collections.Immutable;

static class Extensions
{{
     public static {collectionName}<TSource> To{collectionName}<TSource>(this IEnumerable<TSource> items)
     {{
         return default({collectionName}<TSource>);
     }}
}}

class C
{{
    public void M(IEnumerable<int> p1, List<int> p2, {collectionName}<int> p3)
    {{
        p1.To{collectionName}().To{collectionName}();
        p3.To{collectionName}();

        Extensions.To{collectionName}(Extensions.To{collectionName}(p1));
        Extensions.To{collectionName}(p3);
    }}
}}
",
                // Test0.cs(18,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(17, 9, collectionName),
                // Test0.cs(19,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(18, 9, collectionName),
                // Test0.cs(20,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(20, 9, collectionName),
                // Test0.cs(21,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(21, 9, collectionName));

            await VerifyVB.VerifyAnalyzerAsync($@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Module Extensions
	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TSource)(items As IEnumerable(Of TSource)) As {collectionName}(Of TSource)
		Return Nothing
	End Function
End Module

Class C
	Public Sub M(p1 As IEnumerable(Of Integer), p2 As List(Of Integer), p3 As {collectionName}(Of Integer))
		p1.To{collectionName}().To{collectionName}()
		p3.To{collectionName}()

		Extensions.To{collectionName}(Extensions.To{collectionName}(p1))
		Extensions.To{collectionName}(p3)
	End Sub
End Class
",
                // Test0.vb(14,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(14, 3, collectionName),
                // Test0.vb(15,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(15, 3, collectionName),
                // Test0.vb(17,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(17, 3, collectionName),
                // Test0.vb(18,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(18, 3, collectionName));
        }

        [Theory]
        [MemberData(nameof(CollectionNames_Arity2))]
        public async Task DiagnosticCases_Arity2(string collectionName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Collections.Generic;
using System.Collections.Immutable;

static class Extensions
{{
     public static {collectionName}<TKey, TValue> To{collectionName}<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
     {{
         return default({collectionName}<TKey, TValue>);
     }}
}}

class C
{{
    public void M(IEnumerable<KeyValuePair<int, int>> p1, List<KeyValuePair<int, int>> p2, {collectionName}<int, int> p3)
    {{
        p1.To{collectionName}().To{collectionName}();
        p3.To{collectionName}();

        Extensions.To{collectionName}(Extensions.To{collectionName}(p1));
        Extensions.To{collectionName}(p3);
    }}
}}
",
                // Test0.cs(18,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(17, 9, collectionName),
                // Test0.cs(19,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(18, 9, collectionName),
                // Test0.cs(20,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(20, 9, collectionName),
                // Test0.cs(21,9): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetCSharpResultAt(21, 9, collectionName));

            await VerifyVB.VerifyAnalyzerAsync($@"
Imports System.Collections.Generic
Imports System.Collections.Immutable

Module Extensions
	<System.Runtime.CompilerServices.Extension> _
	Public Function To{collectionName}(Of TKey, TValue)(items As IEnumerable(Of KeyValuePair(Of TKey, TValue))) As {collectionName}(Of TKey, TValue)
		Return Nothing
	End Function
End Module

Class C
	Public Sub M(p1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)), p2 As List(Of KeyValuePair(Of Integer, Integer)), p3 As {collectionName}(Of Integer, Integer))
		p1.To{collectionName}().To{collectionName}()
		p3.To{collectionName}()

		Extensions.To{collectionName}(Extensions.To{collectionName}(p1))
		Extensions.To{collectionName}(p3)
	End Sub
End Class
",
                // Test0.vb(14, 3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(14, 3, collectionName),
                // Test0.vb(15,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(15, 3, collectionName),
                // Test0.vb(17,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(17, 3, collectionName),
                // Test0.vb(18,3): warning CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
                GetBasicResultAt(18, 3, collectionName));
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string collectionName)
        {
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic(DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer.Rule).WithLocation(line, column).WithArguments($"To{collectionName}", collectionName);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column, string collectionName)
        {
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic(DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer.Rule).WithLocation(line, column).WithArguments($"To{collectionName}", collectionName);
#pragma warning restore RS0030 // Do not used banned APIs
        }
    }
}
