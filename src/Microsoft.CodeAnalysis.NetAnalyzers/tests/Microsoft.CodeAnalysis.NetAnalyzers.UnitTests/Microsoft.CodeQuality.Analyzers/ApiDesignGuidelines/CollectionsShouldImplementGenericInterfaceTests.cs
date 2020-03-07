// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CollectionsShouldImplementGenericInterfaceTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new CollectionsShouldImplementGenericInterfaceAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CollectionsShouldImplementGenericInterfaceAnalyzer();
        }

        private DiagnosticResult GetCA1010CSharpResultAt(int line, int column, string typeName, string interfaceName)
        {
            return GetCSharpResultAt(line,
                                     column,
                                     CollectionsShouldImplementGenericInterfaceAnalyzer.Rule,
                                     typeName, interfaceName, $"{interfaceName}<T>");
        }

        private DiagnosticResult GetCA1010BasicResultAt(int line, int column, string typeName, string interfaceName)
        {
            return GetBasicResultAt(line,
                                    column,
                                    CollectionsShouldImplementGenericInterfaceAnalyzer.Rule,
                                    typeName, interfaceName, $"{interfaceName}(Of T)");
        }

        [Fact]
        public void Test_WithCollectionBase()
        {
            VerifyCSharp(@"
using System.Collections;

public class TestClass : CollectionBase { }",
                GetCA1010CSharpResultAt(4, 14, "TestClass", "IList"));

            VerifyBasic(@"
Imports System.Collections

Public Class TestClass 
    Inherits CollectionBase
End Class
",
                GetCA1010BasicResultAt(4, 14, "TestClass", "IList"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void Test_WithCollectionBase_Internal()
        {
            VerifyCSharp(@"
using System.Collections;

internal class TestClass : CollectionBase { }");

            VerifyBasic(@"
Imports System.Collections

Friend Class TestClass 
    Inherits CollectionBase
End Class
");
        }

        [Fact]
        public void Test_WithCollection()
        {
            VerifyCSharp(@"
using System;
using System.Collections;

public class TestClass : ICollection
{
    public int Count => 0;
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public IEnumerator GetEnumerator() { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }
}
",
                GetCA1010CSharpResultAt(5, 14, "TestClass", "ICollection"));

            VerifyBasic(@"
Imports System
Imports System.Collections

Public Class TestClass
	Implements ICollection

    Public ReadOnly Property Count As Integer Implements ICollection.Count
    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
        Throw New NotImplementedException
    End Sub
End Class
",
                GetCA1010BasicResultAt(5, 14, "TestClass", "ICollection"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void Test_WithCollection_Internal()
        {
            VerifyCSharp(@"
using System;
using System.Collections;

internal class TestClass : ICollection
{
    public int Count => 0;
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public IEnumerator GetEnumerator() { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections

Friend Class TestClass
	Implements ICollection

    Public ReadOnly Property Count As Integer Implements ICollection.Count
    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
        Throw New NotImplementedException
    End Sub
End Class
");
        }

        [Fact]
        public void Test_WithEnumerable()
        {
            VerifyCSharp(@"
using System;
using System.Collections;

public class TestClass : IEnumerable
{
    public IEnumerator GetEnumerator() { throw new NotImplementedException(); }
}
",
                GetCA1010CSharpResultAt(5, 14, "TestClass", "IEnumerable"));

            VerifyBasic(@"
Imports System
Imports System.Collections

Public Class TestClass
    Implements IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function
End Class
",
                GetCA1010BasicResultAt(5, 14, "TestClass", "IEnumerable"));
        }

        [Fact]
        public void Test_WithList()
        {
            VerifyCSharp(@"
using System;
using System.Collections;

public class TestClass : IList
{
    public int Count => 0;
    public object SyncRoot => null;
    public bool IsSynchronized => false;
    public bool IsReadOnly => false;
    public bool IsFixedSize => false;

    public object this[int index]
    {
        get { throw new NotImplementedException(); }
        set { throw new NotImplementedException(); }
    }

    public IEnumerator GetEnumerator() { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }
    public int Add(object value) { throw new NotImplementedException(); }
    public bool Contains(object value) { throw new NotImplementedException(); }
    public void Clear() { throw new NotImplementedException(); }
    public int IndexOf(object value) { throw new NotImplementedException(); }
    public void Insert(int index, object value) { throw new NotImplementedException(); }
    public void Remove(object value) { throw new NotImplementedException(); }
    public void RemoveAt(int index) { throw new NotImplementedException(); }
}
",
                GetCA1010CSharpResultAt(5, 14, "TestClass", "IList"));

            VerifyBasic(@"
Imports System
Imports System.Collections

Public Class TestClass
	Implements IList

    Default Public Property Item(index As Integer) As Object Implements IList.Item
        Get
            Throw New NotImplementedException
        End Get
        Set
            Throw New NotImplementedException
        End Set
    End Property

    Public ReadOnly Property Count As Integer Implements ICollection.Count
    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized
    Public ReadOnly Property IsReadOnly As Boolean Implements IList.IsReadOnly
    Public ReadOnly Property IsFixedSize As Boolean Implements IList.IsFixedSize

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
        Throw New NotImplementedException
    End Sub

    Public Function Add(value As Object) As Integer Implements IList.Add
        Throw New NotImplementedException
    End Function

    Public Function Contains(value As Object) As Boolean Implements IList.Contains
        Throw New NotImplementedException
    End Function

    Public Sub Clear() Implements IList.Clear
        Throw New NotImplementedException
    End Sub

    Public Function IndexOf(value As Object) As Integer Implements IList.IndexOf
        Throw New NotImplementedException
    End Function

    Public Sub Insert(index As Integer, value As Object) Implements IList.Insert
        Throw New NotImplementedException
    End Sub

    Public Sub Remove(value As Object) Implements IList.Remove
        Throw New NotImplementedException
    End Sub

    Public Sub RemoveAt(index As Integer) Implements IList.RemoveAt
        Throw New NotImplementedException
    End Sub
End Class
",
                GetCA1010BasicResultAt(5, 14, "TestClass", "IList"));
        }

        [Fact]
        public void Test_WithGenericCollection()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class TestClass : ICollection<int>
{
    public int Count => 0;
    public bool IsReadOnly => false;

    public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }
    IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
    public void Add(int item) { throw new NotImplementedException(); }
    public void Clear() { throw new NotImplementedException(); }
    public bool Contains(int item) { throw new NotImplementedException(); }
    public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }
    public bool Remove(int item) { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class TestClass
	Implements ICollection(Of Integer)

    Public ReadOnly Property Count As Integer Implements ICollection(Of Integer).Count
    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly

    Public Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
        Throw New NotImplementedException
    End Sub

    Public Sub Clear() Implements ICollection(Of Integer).Clear
        Throw New NotImplementedException
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Integer(), arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
        Throw New NotImplementedException
    End Sub

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Throw New NotImplementedException
    End Function
End Class
");
        }

        [Fact]
        public void Test_WithGenericEnumerable()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class TestClass : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }
    IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class TestClass
	Implements IEnumerable(Of Integer)

    Public Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function
End Class
");
        }

        [Fact]
        public void Test_WithGenericList()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class TestClass : IList<int>
{
    public int this[int index]
    {
        get { throw new NotImplementedException(); }
        set { throw new NotImplementedException(); }
    }

    public int Count => 0;
    public bool IsReadOnly => false;

    public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }
    IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
    public void Add(int item) { throw new NotImplementedException(); }
    public void Clear() { throw new NotImplementedException(); }
    public bool Contains(int item) { throw new NotImplementedException(); }
    public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }
    public bool Remove(int item) { throw new NotImplementedException(); }
    public int IndexOf(int item) { throw new NotImplementedException(); }
    public void Insert(int index, int item) { throw new NotImplementedException(); }
    public void RemoveAt(int index) { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class TestClass
	Implements IList(Of Integer)

    Default Public Property Item(index As Integer) As Integer Implements IList(Of Integer).Item
        Get
            Throw New NotImplementedException
        End Get
        Set
            Throw New NotImplementedException
        End Set
    End Property

    Public ReadOnly Property Count As Integer Implements ICollection(Of Integer).Count
    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly

    Public Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
        Throw New NotImplementedException
    End Sub

    Public Sub Clear() Implements ICollection(Of Integer).Clear
        Throw New NotImplementedException
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Integer(), arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
        Throw New NotImplementedException
    End Sub

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Throw New NotImplementedException
    End Function

    Public Function IndexOf(item As Integer) As Integer Implements IList(Of Integer).IndexOf
        Throw New NotImplementedException
    End Function

    Public Sub Insert(index As Integer, item As Integer) Implements IList(Of Integer).Insert
        Throw New NotImplementedException
    End Sub

    Public Sub RemoveAt(index As Integer) Implements IList(Of Integer).RemoveAt
        Throw New NotImplementedException
    End Sub
End Class
");
        }

        [Fact]
        public void Test_WithCollectionBaseAndGenerics()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class TestClass : CollectionBase, ICollection<int>, IEnumerable<int>, IList<int>
{
    public int this[int index]
    {
        get { throw new NotImplementedException(); }
        set { throw new NotImplementedException(); }
    }

    public bool IsReadOnly => false;

    public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }

    public void Add(int item) { throw new NotImplementedException(); }

    public bool Contains(int item) { throw new NotImplementedException(); }

    public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }

    public bool Remove(int item) { throw new NotImplementedException(); }

    public int IndexOf(int item) { throw new NotImplementedException(); }

    public void Insert(int index, int item) { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class TestClass
	Inherits CollectionBase
    Implements ICollection(Of Integer)
	Implements IEnumerable(Of Integer)
	Implements IList(Of Integer)

    Default Public Property Item(index As Integer) As Integer Implements IList(Of Integer).Item
        Get
            Throw New NotImplementedException
        End Get
        Set
            Throw New NotImplementedException
        End Set
    End Property

    Public ReadOnly Property Count As Integer Implements ICollection(Of Integer).Count
    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly

    Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
        Throw New NotImplementedException
    End Sub

    Public Sub Clear() Implements ICollection(Of Integer).Clear
        Throw New NotImplementedException
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Integer(), arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
        Throw New NotImplementedException
    End Sub

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Throw New NotImplementedException
    End Function

    Public Function IndexOf(item As Integer) As Integer Implements IList(Of Integer).IndexOf
        Throw New NotImplementedException
    End Function

    Public Sub Insert(index As Integer, item As Integer) Implements IList(Of Integer).Insert
        Throw New NotImplementedException
    End Sub

    Public Sub RemoveAt(index As Integer) Implements IList(Of Integer).RemoveAt
        Throw New NotImplementedException
    End Sub
End Class
");
        }

        [Fact]
        public void Test_WithCollectionAndGenericCollection()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class TestClass : ICollection, ICollection<int>
{
    int ICollection<int>.Count
    {
        get { throw new NotImplementedException(); }
    }

    int ICollection.Count
    {
        get { throw new NotImplementedException(); }
    }

    public bool IsReadOnly => false;
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }
    IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }
    public void Add(int item) { throw new NotImplementedException(); }
    public void Clear() { throw new NotImplementedException(); }
    public bool Contains(int item) { throw new NotImplementedException(); }
    public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }
    public bool Remove(int item) { throw new NotImplementedException(); }
}
");

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class TestClass
	Implements ICollection
	Implements ICollection(Of Integer)

    Public ReadOnly Property ICollection_Count As Integer Implements ICollection(Of Integer).Count

    Public ReadOnly Property Count As Integer Implements ICollection.Count
    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly
    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized

    Public Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
        Throw New NotImplementedException
    End Sub

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
        Throw New NotImplementedException
    End Sub

    Public Sub Clear() Implements ICollection(Of Integer).Clear
        Throw New NotImplementedException
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Integer(), arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
        Throw New NotImplementedException
    End Sub

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Throw New NotImplementedException
    End Function
End Class
");
        }

        [Fact]
        public void Test_WithBaseAndDerivedClassFailureCase()
        {
            VerifyCSharp(@"
using System;
using System.Collections;

public class BaseClass : ICollection
{
    public int Count => 0;
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public IEnumerator GetEnumerator() { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }
}

public class IntCollection : BaseClass
{
}
",
                GetCA1010CSharpResultAt(5, 14, "BaseClass", "ICollection"),
                GetCA1010CSharpResultAt(15, 14, "IntCollection", "ICollection"));

            VerifyBasic(@"
Imports System
Imports System.Collections

Public Class BaseClass
	Implements ICollection

    Public ReadOnly Property Count As Integer Implements ICollection.Count
    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException
    End Function

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
        Throw New NotImplementedException
    End Sub
End Class

Public Class IntCollection
	Inherits BaseClass
End Class
",
                GetCA1010BasicResultAt(5, 14, "BaseClass", "ICollection"),
                GetCA1010BasicResultAt(21, 14, "IntCollection", "ICollection"));
        }

        [Fact]
        public void Test_InheritsCollectionBaseAndReadOnlyCollectionBase()
        {
            VerifyCSharp(@"
using System.Collections;

public class C : CollectionBase { }

public class R : ReadOnlyCollectionBase { }
",
                GetCA1010CSharpResultAt(4, 14, "C", "IList"),
                GetCA1010CSharpResultAt(6, 14, "R", "ICollection"));

            VerifyBasic(@"
Imports System.Collections

Public Class C
    Inherits CollectionBase
End Class

Public Class R
    Inherits ReadOnlyCollectionBase
End Class
",
                GetCA1010BasicResultAt(4, 14, "C", "IList"),
                GetCA1010BasicResultAt(8, 14, "R", "ICollection"));
        }

        [Fact]
        public void Test_InheritsCollectionBaseAndReadOnlyCollectionBase_DoesFullyImplementGenerics()
        {
            VerifyCSharp(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class C : CollectionBase, ICollection<int>
{
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public bool IsReadOnly => throw new NotImplementedException();

    public void CopyTo(int[] array, int index) { throw new NotImplementedException(); }
    public void CopyTo(Array array, int index) { throw new NotImplementedException(); }

    public void Add(int item)
    {
        throw new NotImplementedException();
    }

    public bool Contains(int item)
    {
        throw new NotImplementedException();
    }

    public bool Remove(int item)
    {
        throw new NotImplementedException();
    }

    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

public class R : ReadOnlyCollectionBase, IEnumerable<int>
{
    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
",
                GetCA1010CSharpResultAt(6, 14, "C", "IList"),
                GetCA1010CSharpResultAt(37, 14, "R", "ICollection"));

            VerifyBasic(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Class C
    Inherits CollectionBase
    Implements ICollection(Of Integer)

    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property ICollection_Count As Integer Implements ICollection(Of Integer).Count
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
        Throw New NotImplementedException()
    End Sub

    Public Sub CopyTo(array() As Integer, arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
        Throw New NotImplementedException()
    End Sub

    Private Sub ICollection_Clear() Implements ICollection(Of Integer).Clear
        Throw New NotImplementedException()
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Throw New NotImplementedException()
    End Function

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Throw New NotImplementedException()
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Public Class R
    Inherits ReadOnlyCollectionBase
    Implements IEnumerable(Of Integer)

    Private Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class
",
                GetCA1010BasicResultAt(6, 14, "C", "IList"),
                GetCA1010BasicResultAt(47, 14, "R", "ICollection"));
        }


        [Fact]
        public void Test_InheritsCollectionBaseAndReadOnlyCollectionBaseAndGenericIEnumerable_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.Collections;
using System.Collections.Generic;

class C : CollectionBase, IEnumerable<int>
{
    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        throw new System.NotImplementedException();
    }
}

class R : ReadOnlyCollectionBase, IEnumerable<int>
{
    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        throw new System.NotImplementedException();
    }
}
");

            VerifyBasic(@"
Imports System.Collections
Imports System.Collections.Generic

Class C
    Inherits CollectionBase
    Implements IEnumerable(Of Integer)

    Private Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New System.NotImplementedException()
    End Function
End Class

Class R
    Inherits ReadOnlyCollectionBase
    Implements IEnumerable(Of Integer)

    Private Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

    }
}