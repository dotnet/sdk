// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CollectionPropertiesShouldBeReadOnlyTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new CollectionPropertiesShouldBeReadOnlyAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CollectionPropertiesShouldBeReadOnlyAnalyzer();
        }

        private DiagnosticResult GetBasicResultAt(int line, int column, string propertyName)
        {
            return GetBasicResultAt(line, column,
                id: CollectionPropertiesShouldBeReadOnlyAnalyzer.RuleId,
                message: string.Format(MicrosoftCodeQualityAnalyzersResources.CollectionPropertiesShouldBeReadOnlyMessage, propertyName));
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string propertyName)
        {
            return GetCSharpResultAt(line, column,
                id: CollectionPropertiesShouldBeReadOnlyAnalyzer.RuleId,
                message: string.Format(MicrosoftCodeQualityAnalyzersResources.CollectionPropertiesShouldBeReadOnlyMessage, propertyName));
        }

        [Fact]
        public void CSharp_CA2227_Test()
        {
            VerifyCSharp(@"
using System;

public class A
{
    public System.Collections.ICollection Col { get; set; }
}
", GetCSharpResultAt(6, 43, "Col"));
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public void CSharp_CA2227_Test_GenericCollection()
        {
            VerifyCSharp(@"
using System;

public class A
{
    public System.Collections.Generic.ICollection<int> Col { get; set; }
}
", GetCSharpResultAt(6, 56, "Col"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_CA2227_Test_Internal()
        {
            VerifyCSharp(@"
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
        public void Basic_CA2227_Test()
        {
            VerifyBasic(@"
Imports System

Public Class A
    Public Property Col As System.Collections.ICollection
End Class
", GetBasicResultAt(5, 21, "Col"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void Basic_CA2227_Test_Internal()
        {
            VerifyBasic(@"
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
        public void CSharp_CA2227_Inherited()
        {
            VerifyCSharp(@"
using System;

public class A<T>
{
    public System.Collections.Generic.List<T> Col { get; set; }
}
", GetCSharpResultAt(6, 47, "Col"));
        }

        [Fact]
        public void CSharp_CA2227_NotPublic()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA2227_Array()
        {
            VerifyCSharp(@"
using System;

public class A
{
    public int[] Col { get; set; }
}
");
        }

        [Fact]
        public void CSharp_CA2227_Indexer()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA2227_NonCollection()
        {
            VerifyCSharp(@"
using System;

public class A
{
    public string Name { get; set; }
}
");
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public void CSharp_CA2227_ReadOnlyCollections()
        {
            // NOTE: ReadOnlyCollection<T> and ReadOnlyDictionary<Key, Value> implement ICollection and hence are flagged.
            //       IReadOnlyCollection<T> does not implement ICollection or ICollection<T>, hence is not flagged.
            VerifyCSharp(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class A
{
    public IReadOnlyCollection<byte> P1 { get; set; }
    public ReadOnlyCollection<byte> P2 { get; set; }
    public ReadOnlyDictionary<string, byte> P3 { get; set; }
}
",
            // Test0.cs(8,37): warning CA2227: Change 'P2' to be read-only by removing the property setter.
            GetCSharpResultAt(8, 37, "P2"),
            // Test0.cs(9,45): warning CA2227: Change 'P3' to be read-only by removing the property setter.
            GetCSharpResultAt(9, 45, "P3"));
        }

        [Fact, WorkItem(1900, "https://github.com/dotnet/roslyn-analyzers/issues/1900")]
        public void CSharp_CA2227_ImmutableCollection()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA2227_ImmutableCollection_02()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA2227_ImmutableCollection_03()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA2227_DataMember()
        {
            VerifyCSharp(@"
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
    }
}