// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CollectionPropertiesShouldBeReadOnlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CollectionPropertiesShouldBeReadOnlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CollectionPropertiesShouldBeReadOnlyTests
    {
        private static DiagnosticResult GetBasicResultAt(int line, int column, string propertyName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(propertyName);

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string propertyName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(propertyName);

        [Fact]
        public async Task CSharp_CA2227_Test()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public System.Collections.ICollection Col { get; set; }
}
", GetCSharpResultAt(6, 43, "Col"));
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public async Task CSharp_CA2227_Test_GenericCollection()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public System.Collections.Generic.ICollection<int> Col { get; set; }
}
", GetCSharpResultAt(6, 56, "Col"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_CA2227_Test_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

internal class A
{
    public System.Collections.ICollection Col { get; set; }
}

public class A2
{
    public System.Collections.ICollection Col { get; private set; }
}

public class A3
{
    internal System.Collections.ICollection Col { get; set; }
}

public class A4
{
    private class A5
    {
        public System.Collections.ICollection Col { get; set; }
    }
}
");
        }

        [Fact]
        public async Task Basic_CA2227_Test()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public Property Col As System.Collections.ICollection
End Class
", GetBasicResultAt(5, 21, "Col"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task Basic_CA2227_Test_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Class A
    Public Property Col As System.Collections.ICollection
End Class

Public Class A2
    Public Property Col As System.Collections.ICollection
        Get
            Return Nothing
        End Get
        Private Set(value As System.Collections.ICollection)
        End Set
    End Property
End Class

Public Class A3
    Friend Property Col As System.Collections.ICollection
        Get
            Return Nothing
        End Get
        Set(value As System.Collections.ICollection)
        End Set
    End Property
End Class

Public Class A4
    Private Class A5
        Public Property Col As System.Collections.ICollection
            Get
                Return Nothing
            End Get
            Set(value As System.Collections.ICollection)
            End Set
        End Property
    End Class
End Class
");
        }

        [Fact]
        public async Task CSharp_CA2227_Inherited()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A<T>
{
    public System.Collections.Generic.List<T> Col { get; set; }
}
", GetCSharpResultAt(6, 47, "Col"));
        }

        [Fact]
        public async Task CSharp_CA2227_NotPublic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A
{
    internal System.Collections.ICollection Col { get; set; }
    protected System.Collections.ICollection Col2 { get; set; }
    private System.Collections.ICollection Col3 { get; set; }
    public System.Collections.ICollection Col4 { get; }
    public System.Collections.ICollection Col5 { get; protected set; }
    public System.Collections.ICollection Col6 { get; private set; }
}
");
        }

        [Fact]
        public async Task CSharp_CA2227_Array()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public int[] Col { get; set; }
}
");
        }

        [Fact]
        public async Task CSharp_CA2227_Indexer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public System.Collections.ICollection this[int i]
    {
        get { throw new NotImplementedException(); }
        set { }
    }
}
");
        }

        [Fact]
        public async Task CSharp_CA2227_NonCollection()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public string Name { get; set; }
}
");
        }

        [Fact]
        [WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        [WorkItem(3313, "https://github.com/dotnet/roslyn-analyzers/issues/3313")]
        public async Task CA2227_ReadOnlyCollections()
        {
            // Readonly interfaces don't implement ICollection/ICollection<T> so won't report a diagnostic

            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class C
{
    public IReadOnlyCollection<string> PI1 { get; protected set; }
    public IReadOnlyDictionary<string, int> PI2 { get; protected set; }

    public ReadOnlyCollection<string> P1 { get; protected set; }
    public ReadOnlyDictionary<string, int> P2 { get; protected set; }
    public ReadOnlyObservableCollection<string> P3 { get; protected set; }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Collections.ObjectModel

Public Class C
    Public Property PI1 As IReadOnlyCollection(Of String)
    Public Property PI2 As IReadOnlyDictionary(Of String, Integer)

    Public Property P1 As ReadOnlyCollection(Of String)
    Public Property P2 As ReadOnlyDictionary(Of String, Integer)
    Public Property P3 As ReadOnlyObservableCollection(Of String)
End Class");
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public async Task CSharp_CA2227_ImmutableCollection()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Immutable;

public class A
{
    public ImmutableArray<byte> ImmArray { get; set; }
    public ImmutableHashSet<string> ImmSet { get; set; }
    public ImmutableList<int> ImmList { get; set; }
    public ImmutableDictionary<A, int> ImmDictionary { get; set; }
}
");
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public async Task CSharp_CA2227_ImmutableCollection_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Immutable;

public class A
{
    public IImmutableStack<byte> ImmStack { get; set; }
    public IImmutableQueue<byte> ImmQueue { get; set; }
    public IImmutableSet<string> ImmSet { get; set; }
    public IImmutableList<int> ImmList { get; set; }
    public IImmutableDictionary<A, int> ImmDictionary { get; set; }
}
");
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public async Task CSharp_CA2227_ImmutableCollection_03()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

public class A
{
    public CustomImmutableList ImmList { get; set; }
}

public class CustomImmutableList : IImmutableList<int>, ICollection<int>
{
    public int this[int index] => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public IImmutableList<int> Add(int value)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> AddRange(IEnumerable<int> items)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(int item)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(int[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public int IndexOf(int item, int index, int count, IEqualityComparer<int> equalityComparer)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> Insert(int index, int element)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> InsertRange(int index, IEnumerable<int> items)
    {
        throw new NotImplementedException();
    }

    public int LastIndexOf(int item, int index, int count, IEqualityComparer<int> equalityComparer)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> Remove(int value, IEqualityComparer<int> equalityComparer)
    {
        throw new NotImplementedException();
    }

    public bool Remove(int item)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> RemoveAll(Predicate<int> match)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> RemoveRange(IEnumerable<int> items, IEqualityComparer<int> equalityComparer)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> RemoveRange(int index, int count)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> Replace(int oldValue, int newValue, IEqualityComparer<int> equalityComparer)
    {
        throw new NotImplementedException();
    }

    public IImmutableList<int> SetItem(int index, int value)
    {
        throw new NotImplementedException();
    }

    void ICollection<int>.Add(int item)
    {
        throw new NotImplementedException();
    }

    void ICollection<int>.Clear()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CSharp_CA2227_DataMember()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System.Runtime.Serialization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DataMemberAttribute : Attribute
    {
    }
}

class A
{
    [System.Runtime.Serialization.DataMember]
    public System.Collections.ICollection Col { get; set; }
}
");
        }

        [Fact, WorkItem(4461, "https://github.com/dotnet/roslyn-analyzers/issues/4461")]
        public async Task CA2227_CSharp_InitPropertyRecord()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System.Collections.Generic;

public record MyRecord(IList<int> Items);",
            }.RunAsync();
        }

        [Fact, WorkItem(4461, "https://github.com/dotnet/roslyn-analyzers/issues/4461")]
        public async Task CA2227_CSharp_InitProperty()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System.Collections.Generic;

class C
{
    public IList<int> L { get; init; }
}

struct S
{
    public IList<int> L { get; init; }
}",
            }.RunAsync();
        }
    }
}