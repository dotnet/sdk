// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldHaveCorrectSuffixAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldHaveCorrectSuffixFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldHaveCorrectSuffixAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldHaveCorrectSuffixFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldHaveCorrectSuffixTests
    {
        [Fact]
        public async Task CA1710_AllScenarioDiagnostics_CSharp()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

public class EventsItemsEventArgs : EventArgs { }
public class EventsItemsDerived : EventsItemsEventArgs { }

public class EventsItems : EventArgs { }

public delegate void EventCallback(object sender, EventArgs e);

public class EventHandlerTest
{
    public event EventCallback EventOne;
}

[Serializable]
public class DiskError : Exception
{
    public DiskError() { }

    public DiskError(string message) : base(message) { }
    public DiskError(string message, Exception innerException) : base(message, innerException) { }
    protected DiskError(SerializationInfo info, StreamingContext context) : base(info, context) { }
}


[AttributeUsage(AttributeTargets.Class)]
public sealed class Verifiable : Attribute
{
}

public class ConditionClass : IMembershipCondition
{
    public bool Check(Evidence evidence) { return false; }
    public IMembershipCondition Copy() { return (IMembershipCondition)null; }
    public void FromXml(SecurityElement e, PolicyLevel level) { }
    public SecurityElement ToXml(PolicyLevel level) { return (SecurityElement)null; }
    public void FromXml(SecurityElement e) { }
    public SecurityElement ToXml() { return (SecurityElement)null; }
}

[Serializable]
public class MyTable<TKey, TValue> : System.Collections.Generic.Dictionary<TKey, TValue>
{
    protected MyTable(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

[Serializable]
public class MyStringObjectHashtable : System.Collections.Generic.Dictionary<string, object>
{
    protected MyStringObjectHashtable(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

public class MyList<T> : System.Collections.ObjectModel.Collection<T> { }

public class StringGrouping<T> : System.Collections.ObjectModel.Collection<T> { }

public class LastInFirstOut<T> : System.Collections.Generic.Stack<T> { }

public class StackOfIntegers : System.Collections.Generic.Stack<int> { }

public class FirstInFirstOut<T> : System.Collections.Generic.Queue<T> { }

public class QueueOfNumbers : System.Collections.Generic.Queue<int> { }

public class MyDataStructure : Stack { }

public class AnotherDataStructure : Queue { }

public class WronglyNamedPermissionClass : CodeAccessPermission
{
    public override IPermission Copy() { return (IPermission)null; }
    public override void FromXml(SecurityElement e) { }
    public override IPermission Intersect(IPermission target) { return (IPermission)null; }
    public override bool IsSubsetOf(IPermission target) { return false; }
    public override SecurityElement ToXml() { return (SecurityElement)null; }
}

public class WronglyNamedIPermissionClass : IPermission
{
    public IPermission Copy() { return (IPermission)null; }
    public void FromXml(SecurityElement e) { }
    public IPermission Intersect(IPermission target) { return (IPermission)null; }
    public bool IsSubsetOf(IPermission target) { return false; }
    public SecurityElement ToXml() { return (SecurityElement)null; }
    public IPermission Union(IPermission target) { return (IPermission)null; }
    public void Demand() { }
}

public class WronglyNamedType : Stream
{
    public override bool CanRead { get { return false; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return 0; } }
    public override long Position { get { return 0; } set { } }
    public override void Close() { base.Close(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) { return 0; }
    public override long Seek(long offset, SeekOrigin origin) { return 0; }
    public override void SetLength(long value) { }
    public override void Write(byte[] buffer, int offset, int count) { }
}

public class MyCollectionIsEnumerable : IEnumerable
{
    public void CopyTo(Array destination, int index)
    {
        System.Console.WriteLine(this);
        System.Console.WriteLine(destination);
        System.Console.WriteLine(index);
    }
    public int Count
    {
        get
        {
            Console.WriteLine(this);
            return 0;
        }
        set
        {
            System.Console.WriteLine(this);
            System.Console.WriteLine(value);
        }
    }
    public bool IsSynchronized
    {
        get
        {
            Console.WriteLine(this);
            return true;
        }
        set
        {
            System.Console.WriteLine(this);
            System.Console.WriteLine(value);
        }
    }
    public object SyncRoot
    {
        get
        {
            return this;
        }
        set
        {
            System.Console.WriteLine(this);
            System.Console.WriteLine(value);
        }
    }
    public IEnumerator GetEnumerator() { return null; }
}

public class CollectionDoesNotEndInCollectionClass : StringCollection { }

[Serializable]
public class DictionaryDoesNotEndInDictionaryClass : Hashtable
{
    protected DictionaryDoesNotEndInDictionaryClass(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}

public class MyTest<T> : List<T>
{
}

[Serializable]
public class DataSetWithWrongSuffix : DataSet
{
    protected DataSetWithWrongSuffix(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}

[Serializable]
public class DataTableWithWrongSuffix : DataTable
{
    protected DataTableWithWrongSuffix(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1710.exclude_indirect_base_types = false") },
                    ExpectedDiagnostics =
                    {
                        GetCA1710CSharpResultAt(line: 16, column: 14, typeName: "EventsItemsDerived", suffix: "EventArgs"),
                        GetCA1710CSharpResultAt(line: 18, column: 14, typeName: "EventsItems", suffix: "EventArgs"),
                        GetCA1710CSharpResultAt(line: 24, column: 32, typeName: "EventCallback", suffix: "EventHandler"),
                        GetCA1710CSharpResultAt(line: 28, column: 14, typeName: "DiskError", suffix: "Exception"),
                        GetCA1710CSharpResultAt(line: 39, column: 21, typeName: "Verifiable", suffix: "Attribute"),
                        GetCA1710CSharpResultAt(line: 43, column: 14, typeName: "ConditionClass", suffix: "Condition"),
                        GetCA1710CSharpResultAt(line: 54, column: 14, typeName: "MyTable<TKey, TValue>", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 60, column: 14, typeName: "MyStringObjectHashtable", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 65, column: 14, typeName: "MyList<T>", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710CSharpResultAt(line: 67, column: 14, typeName: "StringGrouping<T>", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710CSharpResultAt(line: 69, column: 14, typeName: "LastInFirstOut<T>", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 71, column: 14, typeName: "StackOfIntegers", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 73, column: 14, typeName: "FirstInFirstOut<T>", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 75, column: 14, typeName: "QueueOfNumbers", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 77, column: 14, typeName: "MyDataStructure", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 79, column: 14, typeName: "AnotherDataStructure", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 81, column: 14, typeName: "WronglyNamedPermissionClass", suffix: "Permission"),
                        GetCA1710CSharpResultAt(line: 90, column: 14, typeName: "WronglyNamedIPermissionClass", suffix: "Permission"),
                        GetCA1710CSharpResultAt(line: 101, column: 14, typeName: "WronglyNamedType", suffix: "Stream"),
                        GetCA1710CSharpResultAt(line: 116, column: 14, typeName: "MyCollectionIsEnumerable", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710CSharpResultAt(line: 165, column: 14, typeName: "CollectionDoesNotEndInCollectionClass", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710CSharpResultAt(line: 168, column: 14, typeName: "DictionaryDoesNotEndInDictionaryClass", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710CSharpResultAt(line: 174, column: 14, typeName: "MyTest<T>", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710CSharpResultAt(line: 179, column: 14, typeName: "DataSetWithWrongSuffix", suffix: "DataSet"),
                        GetCA1710CSharpResultAt(line: 186, column: 14, typeName: "DataTableWithWrongSuffix", suffix: "DataTable", additionalSuffixes: "Collection"),
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1710_NoDiagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

[Serializable]
public class MyDictionary<TKey, TValue> : System.Collections.Generic.Dictionary<TKey, TValue>
{
    protected MyDictionary(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

[Serializable]
public class MyStringObjectDictionary : System.Collections.Generic.Dictionary<string, object>
{
    protected MyStringObjectDictionary(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

public class MyCollection<T> : System.Collections.ObjectModel.Collection<T> { }

public class MyStringCollection : System.Collections.ObjectModel.Collection<string> { }

public class MyStack<T> : System.Collections.Generic.Stack<T> { }

public class MyIntegerStack : System.Collections.Generic.Stack<int> { }

public class StackCollection<T> : System.Collections.Generic.Stack<T> { }

public class IntegerStackCollection : System.Collections.Generic.Stack<int> { }

public class MyQueue<T> : System.Collections.Generic.Queue<T> { }

public class MyIntegerQueue : System.Collections.Generic.Queue<int> { }

public class QueueCollection<T> : System.Collections.Generic.Queue<T> { }

public class IntegerQueueCollection : System.Collections.Generic.Queue<int> { }

public delegate void SimpleEventHandler(object sender, EventArgs e);

public class EventHandlerTest
{
    public event SimpleEventHandler EventOne;

    public event EventHandler<EventArgs> EventTwo;
}

[Serializable]
public class DiskErrorException : Exception
{
    public DiskErrorException() { }
    public DiskErrorException(string message) : base(message) { }
    public DiskErrorException(string message, Exception innerException) : base(message, innerException) { }
    protected DiskErrorException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}


[AttributeUsage(AttributeTargets.Class)]
public sealed class VerifiableAttribute : Attribute
{
}

public class MyCondition : IMembershipCondition
{
    public bool Check(Evidence evidence) { return false; }
    public IMembershipCondition Copy() { return (IMembershipCondition)null; }
    public void FromXml(SecurityElement e, PolicyLevel level) { }
    public SecurityElement ToXml(PolicyLevel level) { return (SecurityElement)null; }
    public void FromXml(SecurityElement e) { }
    public SecurityElement ToXml() { return (SecurityElement)null; }
}

public class CorrectlyNamedPermission : CodeAccessPermission
{
    public override IPermission Copy() { return (IPermission)null; }
    public override void FromXml(SecurityElement e) { }
    public override IPermission Intersect(IPermission target) { return (IPermission)null; }
    public override bool IsSubsetOf(IPermission target) { return false; }
    public override SecurityElement ToXml() { return (SecurityElement)null; }
}

public class CorrectlyNamedIPermission : IPermission
{
    public IPermission Copy() { return (IPermission)null; }
    public void FromXml(SecurityElement e) { }
    public IPermission Intersect(IPermission target) { return (IPermission)null; }
    public bool IsSubsetOf(IPermission target) { return false; }
    public SecurityElement ToXml() { return (SecurityElement)null; }
    public IPermission Union(IPermission target) { return (IPermission)null; }
    public void Demand() { }
}


public class CorrectlyNamedTypeStream : Stream
{
    public override bool CanRead { get { return false; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return 0; } }
    public override long Position { get { return 0; } set { } }
    public override void Close() { base.Close(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) { return 0; }
    public override long Seek(long offset, SeekOrigin origin) { return 0; }
    public override void SetLength(long value) { }
    public override void Write(byte[] buffer, int offset, int count) { }
}


public class CollectionEndsInCollection : StringCollection { }

[Serializable]
public class DictionaryEndsInDictionary : Hashtable
{
    public DictionaryEndsInDictionary() { }
    protected DictionaryEndsInDictionary(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}

[Serializable]
public class MySpecialQueue : Queue { }

public class QueueCollection : Queue { }

[Serializable]
public class MyStack : Stack { }

public class StackCollection : Stack { }

[Serializable]
public class MyDataSet : DataSet
{
    public MyDataSet() { }

    protected MyDataSet(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public void DoWork() { Console.WriteLine(this); }
}

[Serializable]
public class MyDataTable : DataTable
{
    public MyDataTable() { }

    protected MyDataTable(SerializationInfo info, StreamingContext context)
        : base(info, context) { }

    public void DoWork() { Console.WriteLine(this); }
}

[Serializable]
public class MyCollectionDataTable : DataTable, IEnumerable
{
    public MyCollectionDataTable() { }

    protected MyCollectionDataTable(SerializationInfo info, StreamingContext context)
        : base(info, context) { }

    public void DoWork() { Console.WriteLine(this); }

    public IEnumerator GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task CA1710_AllScenarioDiagnostics_VisualBasic()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.Data
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Security
Imports System.Security.Policy

Public Class AnotherDataStructure
    Inherits Queue

End Class
Public Class CollectionDoesNotEndInCollectionClass
    Inherits StringCollection

End Class

Public Class ConditionClass
    Implements IMembershipCondition, ISecurityEncodable, ISecurityPolicyEncodable

    ' Methods
    Public Function Check(ByVal evidence As Evidence) As Boolean Implements IMembershipCondition.Check
        Return False
    End Function

    Public Function Copy() As IMembershipCondition Implements IMembershipCondition.Copy
        Return Nothing
    End Function

    Public Sub FromXml(ByVal e As SecurityElement) Implements ISecurityEncodable.FromXml

    End Sub

    Public Sub FromXml(ByVal e As SecurityElement, ByVal level As PolicyLevel) Implements ISecurityPolicyEncodable.FromXml

    End Sub

    Public Function ToXml() As SecurityElement Implements ISecurityEncodable.ToXml
        Return Nothing
    End Function

    Public Function ToXml(ByVal level As PolicyLevel) As SecurityElement Implements ISecurityPolicyEncodable.ToXml
        Return Nothing
    End Function

    Public Overrides Function ToString() As String Implements IMembershipCondition.ToString
        Return MyBase.ToString()
    End Function

    Public Overrides Function Equals(ByVal obj As Object) As Boolean Implements IMembershipCondition.Equals
        Return MyBase.Equals(obj)
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return MyBase.GetHashCode()
    End Function
End Class

<Serializable()>
Public Class DataSetWithWrongSuffix
    Inherits DataSet

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

<Serializable()>
Public Class DataTableWithWrongSuffix
    Inherits DataTable

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

<Serializable()>
Public Class DictionaryDoesNotEndInDictionaryClass
    Inherits Hashtable

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

<Serializable()>
Public Class DiskError
    Inherits Exception

    ' Methods
    Public Sub New()
    End Sub

    Public Sub New(ByVal message As String)
        MyBase.New(message)
    End Sub

    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

    Public Sub New(ByVal message As String, ByVal innerException As Exception)
        MyBase.New(message, innerException)
    End Sub

End Class

Public Delegate Sub EventCallback(ByVal sender As Object, ByVal e As EventArgs)

Public Class EventHandlerTest
    ' Events
    Public Event EventOne As EventCallback
End Class

Public Class EventsItems
    Inherits EventArgs

End Class

Public Class FirstInFirstOut(Of T)
    Inherits Queue(Of T)

End Class

Public Class LastInFirstOut(Of T)
    Inherits Stack(Of T)

End Class

Public Class MyCollectionIsEnumerable
    Implements IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class

Public Class MyDataStructure
    Inherits Stack

End Class

Public Class MyList(Of T)
    Inherits Collection(Of T)

End Class

<Serializable()>
Public Class MyStringObjectHashtable
    Inherits Dictionary(Of String, Object)

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

<Serializable()>
Public Class MyTable(Of TKey, TValue)
    Inherits Dictionary(Of TKey, TValue)

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

Public Class MyTest(Of T)
    Inherits List(Of T)

End Class

Public Class QueueOfNumbers
    Inherits Queue(Of Integer)

End Class

Public Class StackOfIntegers
    Inherits Stack(Of Integer)

End Class

Public Class StringGrouping(Of T)
    Inherits Collection(Of T)

End Class

<AttributeUsage(AttributeTargets.Class)>
Public NotInheritable Class Verifiable
    Inherits Attribute

End Class

Public Class WronglyNamedIPermissionClass
    Implements IPermission, ISecurityEncodable

    ' Methods
    Public Function Copy() As IPermission Implements IPermission.Copy
        Return Nothing
    End Function

    Public Sub Demand() Implements IPermission.Demand
    End Sub

    Public Sub FromXml(ByVal e As SecurityElement) Implements ISecurityEncodable.FromXml
    End Sub

    Public Function Intersect(ByVal target As IPermission) As IPermission Implements IPermission.Intersect
        Return Nothing
    End Function

    Public Function IsSubsetOf(ByVal target As IPermission) As Boolean Implements IPermission.IsSubsetOf
        Return False
    End Function

    Public Function ToXml() As SecurityElement Implements ISecurityEncodable.ToXml
        Return Nothing
    End Function

    Public Function Union(ByVal target As IPermission) As IPermission Implements IPermission.Union
        Return Nothing
    End Function

End Class

Public Class WronglyNamedPermissionClass
    Inherits CodeAccessPermission

    ' Methods
    Public Overrides Function Copy() As IPermission
        Return Nothing
    End Function

    Public Overrides Sub FromXml(ByVal e As SecurityElement)
    End Sub

    Public Overrides Function Intersect(ByVal target As IPermission) As IPermission
        Return Nothing
    End Function

    Public Overrides Function IsSubsetOf(ByVal target As IPermission) As Boolean
        Return False
    End Function

    Public Overrides Function ToXml() As SecurityElement
        Return Nothing
    End Function

End Class

Public Class WronglyNamedType
    Inherits Stream

    ' Methods
    Public Overrides Sub Close()
        MyBase.Close()
    End Sub

    Public Overrides Sub Flush()
    End Sub

    Public Overrides Function Read(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer) As Integer
        Return 0
    End Function

    Public Overrides Function Seek(ByVal offset As Long, ByVal origin As SeekOrigin) As Long
        Return CType(0, Long)
    End Function

    Public Overrides Sub SetLength(ByVal value As Long)
    End Sub

    Public Overrides Sub Write(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer)
    End Sub


    ' Properties
    Public Overrides ReadOnly Property CanRead() As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property CanSeek() As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property CanWrite() As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property Length() As Long
        Get
            Return CType(0, Long)
        End Get
    End Property

    Public Overrides Property Position() As Long
        Get
            Return CType(0, Long)
        End Get
        Set(ByVal value As Long)
        End Set
    End Property

End Class",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1710.exclude_indirect_base_types = false") },
                    ExpectedDiagnostics =
                    {
                        GetCA1710BasicResultAt(line: 13, column: 14, typeName: "AnotherDataStructure", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 17, column: 14, typeName: "CollectionDoesNotEndInCollectionClass", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710BasicResultAt(line: 22, column: 14, typeName: "ConditionClass", suffix: "Condition"),
                        GetCA1710BasicResultAt(line: 64, column: 14, typeName: "DataSetWithWrongSuffix", suffix: "DataSet"),
                        GetCA1710BasicResultAt(line: 75, column: 14, typeName: "DataTableWithWrongSuffix", suffix: "DataTable", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 86, column: 14, typeName: "DictionaryDoesNotEndInDictionaryClass", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 97, column: 14, typeName: "DiskError", suffix: "Exception"),
                        GetCA1710BasicResultAt(line: 122, column: 18, typeName: "EventCallback", suffix: "EventHandler"),
                        GetCA1710BasicResultAt(line: 125, column: 14, typeName: "EventsItems", suffix: "EventArgs"),
                        GetCA1710BasicResultAt(line: 130, column: 14, typeName: "FirstInFirstOut(Of T)", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 135, column: 14, typeName: "LastInFirstOut(Of T)", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 140, column: 14, typeName: "MyCollectionIsEnumerable", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710BasicResultAt(line: 148, column: 14, typeName: "MyDataStructure", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 153, column: 14, typeName: "MyList(Of T)", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710BasicResultAt(line: 159, column: 14, typeName: "MyStringObjectHashtable", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 170, column: 14, typeName: "MyTable(Of TKey, TValue)", suffix: "Dictionary", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 180, column: 14, typeName: "MyTest(Of T)", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710BasicResultAt(line: 185, column: 14, typeName: "QueueOfNumbers", suffix: "Queue", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 190, column: 14, typeName: "StackOfIntegers", suffix: "Stack", additionalSuffixes: "Collection"),
                        GetCA1710BasicResultAt(line: 195, column: 14, typeName: "StringGrouping(Of T)", suffix: "Collection", additionalSuffixes: "Dictionary', 'Set', 'Stack', 'Queue"),
                        GetCA1710BasicResultAt(line: 201, column: 29, typeName: "Verifiable", suffix: "Attribute"),
                        GetCA1710BasicResultAt(line: 206, column: 14, typeName: "WronglyNamedIPermissionClass", suffix: "Permission"),
                        GetCA1710BasicResultAt(line: 238, column: 14, typeName: "WronglyNamedPermissionClass", suffix: "Permission"),
                        GetCA1710BasicResultAt(line: 263, column: 14, typeName: "WronglyNamedType", suffix: "Stream"),
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1710_NoDiagnostics_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.Data
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Security
Imports System.Security.Policy

Public Class CollectionEndsInCollection
    Inherits StringCollection

End Class

Public Class CorrectlyNamedIPermission
    Implements IPermission, ISecurityEncodable


    Public Function Copy() As System.Security.IPermission Implements System.Security.IPermission.Copy
        Return Me

    End Function

    Public Sub Demand() Implements System.Security.IPermission.Demand

    End Sub

    Public Function Intersect(ByVal target As System.Security.IPermission) As System.Security.IPermission Implements System.Security.IPermission.Intersect
        Return Nothing

    End Function

    Public Function IsSubsetOf(ByVal target As System.Security.IPermission) As Boolean Implements System.Security.IPermission.IsSubsetOf
        Return False
    End Function

    Public Function Union(ByVal target As System.Security.IPermission) As System.Security.IPermission Implements System.Security.IPermission.Union
        Return Me

    End Function

    Public Sub FromXml(ByVal e As System.Security.SecurityElement) Implements System.Security.ISecurityEncodable.FromXml

    End Sub

    Public Function ToXml() As System.Security.SecurityElement Implements System.Security.ISecurityEncodable.ToXml

    End Function
End Class

Public Class CorrectlyNamedPermission
    Inherits CodeAccessPermission

    Public Overrides Function Copy() As System.Security.IPermission

    End Function

    Public Overrides Sub FromXml(ByVal elem As System.Security.SecurityElement)

    End Sub

    Public Overrides Function Intersect(ByVal target As System.Security.IPermission) As System.Security.IPermission

    End Function

    Public Overrides Function IsSubsetOf(ByVal target As System.Security.IPermission) As Boolean

    End Function

    Public Overrides Function ToXml() As System.Security.SecurityElement

    End Function
End Class

Public Class CorrectlyNamedTypeStream
    Inherits FileStream

    Public Sub New()
        MyBase.New("""", FileMode.Open)
    End Sub
End Class

<Serializable()>
Public Class DictionaryEndsInDictionary
    Inherits Hashtable

    ' Methods
    Public Sub New()
    End Sub

    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

<Serializable()>
Public Class DiskErrorException
    Inherits Exception

    ' Methods
    Public Sub New()
    End Sub

    Public Sub New(ByVal message As String)
        MyBase.New(message)
    End Sub

    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

    Public Sub New(ByVal message As String, ByVal innerException As Exception)
        MyBase.New(message, innerException)
    End Sub

End Class

Public Class EventHandlerTest
    ' Events
    Public Event EventOne As SimpleEventHandler
End Class

Public Class EventsItemsEventArgs
    Inherits EventArgs

End Class

Public Class IntegerQueueCollection
    Inherits Queue(Of Integer)

End Class

Public Class IntegerStackCollection
    Inherits Stack(Of Integer)

End Class

Public Class MyCollection(Of T)
    Inherits Collection(Of T)

End Class

Public Class MyCondition
    Implements IMembershipCondition, ISecurityEncodable, ISecurityPolicyEncodable

    Public Sub FromXml(ByVal e As System.Security.SecurityElement) Implements System.Security.ISecurityEncodable.FromXml

    End Sub

    Public Function ToXml() As System.Security.SecurityElement Implements System.Security.ISecurityEncodable.ToXml

    End Function

    Public Sub FromXml1(ByVal e As System.Security.SecurityElement, ByVal level As System.Security.Policy.PolicyLevel) Implements System.Security.ISecurityPolicyEncodable.FromXml

    End Sub

    Public Function ToXml1(ByVal level As System.Security.Policy.PolicyLevel) As System.Security.SecurityElement Implements System.Security.ISecurityPolicyEncodable.ToXml

    End Function

    Public Function Check(ByVal evidence As System.Security.Policy.Evidence) As Boolean Implements System.Security.Policy.IMembershipCondition.Check

    End Function

    Public Function Copy() As System.Security.Policy.IMembershipCondition Implements System.Security.Policy.IMembershipCondition.Copy

    End Function

    Public Function Equals1(ByVal obj As Object) As Boolean Implements System.Security.Policy.IMembershipCondition.Equals

    End Function

    Public Function ToString1() As String Implements System.Security.Policy.IMembershipCondition.ToString

    End Function
End Class

<Serializable()>
Public Class MyDataSet
    Inherits DataSet

    ' Methods
    Public Sub New()
    End Sub

    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

    Public Sub DoWork()
        Console.WriteLine(Me)
    End Sub

End Class

<Serializable()>
Public Class MyDataTable
    Inherits DataTable

    ' Methods
    Public Sub New()
    End Sub

    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

    Public Sub DoWork()
        Console.WriteLine(Me)
    End Sub

End Class

<Serializable()>
Public Class MyDictionary(Of TKey, TValue)
    Inherits Dictionary(Of TKey, TValue)

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

Public Class MyIntegerQueue
    Inherits Queue(Of Integer)

End Class

Public Class MyIntegerStack
    Inherits Stack(Of Integer)

End Class

Public Class MyQueue(Of T)
    Inherits Queue(Of T)

End Class

<Serializable()>
Public Class MySpecialQueue
    Inherits Queue

End Class

Public Class MyStack(Of T)
    Inherits Stack(Of T)

End Class

<Serializable()>
Public Class MyStack
    Inherits Stack

End Class

Public Class MyStringCollection
    Inherits Collection(Of String)

End Class

<Serializable()>
Public Class MyStringObjectDictionary
    Inherits Dictionary(Of String, Object)

    ' Methods
    Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)
        MyBase.New(info, context)
    End Sub

End Class

Public Class QueueCollection(Of T)
    Inherits Queue(Of T)

End Class

Public Class QueueCollection
    Inherits Queue

End Class

Public Delegate Sub SimpleEventHandler(ByVal sender As Object, ByVal e As EventArgs)

Public Class StackCollection(Of T)
    Inherits Stack(Of T)

End Class

Public Class StackCollection
    Inherits Stack

End Class

<AttributeUsage(AttributeTargets.Class)>
Public NotInheritable Class VerifiableAttribute
    Inherits Attribute

End Class");
        }

        [Fact, WorkItem(1822, "https://github.com/dotnet/roslyn-analyzers/issues/1822")]
        public async Task CA1710_SystemAction_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public event Action MyEvent;
}");
        }

        [Fact, WorkItem(1822, "https://github.com/dotnet/roslyn-analyzers/issues/1822")]
        public async Task CA1710_CustomDelegate_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public delegate void MyDelegate(int param);
    public event MyDelegate MyEvent;
}");
        }

        [Fact, WorkItem(2955, "https://github.com/dotnet/roslyn-analyzers/issues/2955")]
        public async Task CA1710_IReadOnlyDictionary()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class C : IReadOnlyDictionary<int, string>
{
    public string this[int key] => throw new System.NotImplementedException();

    public IEnumerable<int> Keys => throw new System.NotImplementedException();

    public IEnumerable<string> Values => throw new System.NotImplementedException();

    public int Count => throw new System.NotImplementedException();

    public bool ContainsKey(int key) => throw new System.NotImplementedException();
    public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => throw new System.NotImplementedException();
    public bool TryGetValue(int key, out string value) => throw new System.NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
}",
                GetCA1710CSharpResultAt(6, 14, "C", "Dictionary", "Collection"));
        }

        [Fact, WorkItem(2955, "https://github.com/dotnet/roslyn-analyzers/issues/2955")]
        public async Task CA1710_IReadOnlyCollection_IncludeIndirectBaseTypes()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Collections;
using System.Collections.Generic;

public class C : IReadOnlyCollection<int>
{
    public int Count => throw new System.NotImplementedException();

    public IEnumerator<int> GetEnumerator() => throw new System.NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1710.exclude_indirect_base_types = false") },
                    ExpectedDiagnostics =
                    {
                        GetCA1710CSharpResultAt(6, 14, "C", "Collection", "Dictionary', 'Set', 'Stack', 'Queue"),
                    },
                }
            }.RunAsync();
        }

        [Theory, WorkItem(3065, "https://github.com/dotnet/roslyn-analyzers/issues/3065")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = MyNamespace.SomeClass->FirstSuffix")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:MyNamespace.SomeClass->FirstSuffix")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = SomeOtherClass->ABC")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:SomeOtherClass->ABC")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = MyNamespace.SomeClass->FirstSuffix|MyNamespace.IMyInterface->Interface")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:MyNamespace.SomeClass->FirstSuffix|T:MyNamespace.IMyInterface->Interface")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = invalid")]
        // In case of duplicated entries, only the first is kept
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = MyNamespace.SomeClass->FirstSuffix|MyNamespace.SomeClass->SecondSuffix")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:MyNamespace.SomeClass->FirstSuffix|T:MyNamespace.SomeClass->SecondSuffix")]
        public async Task CA1710_AdditionalSuffixes(string editorConfigText)
        {
            editorConfigText = $@"dotnet_code_quality.CA1710.exclude_indirect_base_types = false
{editorConfigText}";

            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

namespace MyNamespace
{
    public interface IMyInterface {}
    public class SomeClass {}

    public class SomeSubClass : SomeClass {}
    public class SomeSubSubClass : SomeSubClass {}

    public class C : ICloneable, IMyInterface
    {
        public object Clone() => null;
    }
}

public class SomeOtherClass
{
}

public class SomeOtherSubClass : SomeOtherClass {}"},
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            };

            if (editorConfigText.EndsWith("Suffix", StringComparison.Ordinal))
            {
                csharpTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710CSharpResultAt(9, 18, "MyNamespace.SomeSubClass", "FirstSuffix"),
                    GetCA1710CSharpResultAt(10, 18, "MyNamespace.SomeSubSubClass", "FirstSuffix"),
                });
            }
            else if (editorConfigText.EndsWith("ABC", StringComparison.Ordinal))
            {
                csharpTest.ExpectedDiagnostics.Add(GetCA1710CSharpResultAt(22, 14, "SomeOtherSubClass", "ABC"));
            }
            else if (editorConfigText.EndsWith("Interface", StringComparison.Ordinal))
            {
                csharpTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710CSharpResultAt(9, 18, "MyNamespace.SomeSubClass", "FirstSuffix"),
                    GetCA1710CSharpResultAt(10, 18, "MyNamespace.SomeSubSubClass", "FirstSuffix"),
                    GetCA1710CSharpResultAt(12, 18, "MyNamespace.C", "Interface"),
                });
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System

Namespace MyNamespace
    Interface IMyInterface
    End Interface

    Public Class SomeClass
    End Class

    Public Class SomeSubClass
        Inherits SomeClass
    End Class

    Public Class SomeSubSubClass
        Inherits SomeSubClass
    End Class

    Public Class C
        Implements ICloneable, IMyInterface

        Public Function Clone() As Object Implements ICloneable.Clone
            Return Nothing
        End Function
    End Class
End Namespace

Public Class SomeOtherClass
End Class

Public Class SomeOtherSubClass
    Inherits SomeOtherClass
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            };

            if (editorConfigText.EndsWith("Suffix", StringComparison.Ordinal))
            {
                vbTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710BasicResultAt(11, 18, "MyNamespace.SomeSubClass", "FirstSuffix"),
                    GetCA1710BasicResultAt(15, 18, "MyNamespace.SomeSubSubClass", "FirstSuffix"),
                });
            }
            else if (editorConfigText.EndsWith("ABC", StringComparison.Ordinal))
            {
                vbTest.ExpectedDiagnostics.Add(GetCA1710CSharpResultAt(31, 14, "SomeOtherSubClass", "ABC"));
            }
            else if (editorConfigText.EndsWith("Interface", StringComparison.Ordinal))
            {
                vbTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710BasicResultAt(11, 18, "MyNamespace.SomeSubClass", "FirstSuffix"),
                    GetCA1710BasicResultAt(15, 18, "MyNamespace.SomeSubSubClass", "FirstSuffix"),
                    GetCA1710BasicResultAt(19, 18, "MyNamespace.C", "Interface"),
                });
            }

            await vbTest.RunAsync();
        }

        [Theory, WorkItem(3065, "https://github.com/dotnet/roslyn-analyzers/issues/3065")]
        // methods are not handled
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = M:MyNamespace.SomeClass.MyMethod()->Suffix")]
        // namespaces are not handled
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = N:MyNamespace:Suffix")]
        // more than one -> is not handled
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:MyNamespace.SomeClass->Suffix1->Suffix2")]
        // no suffix
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:MyNamespace.SomeClass")]
        public async Task CA1710_InvalidSyntaxNoSuffix(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
namespace MyNamespace
{
    public class SomeClass
    {
        public void MyMethod() {}
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Namespace MyNamespace
    Public Class SomeClass
        Public Sub MyMethod()
        End Sub
    End Class
End Namespace"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(3065, "https://github.com/dotnet/roslyn-analyzers/issues/3065")]
        public async Task CA1710_UserMappingWinsOverHardcoded()
        {
            var editorConfigText = @"dotnet_code_quality.CA1710.exclude_indirect_base_types = false
dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Collections.Generic.IDictionary`2->MySuffix";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Collections.Generic;

public class SomeClass : Dictionary<string, string>
{
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                    ExpectedDiagnostics =
                    {
                        GetCA1710CSharpResultAt(4, 14, "SomeClass", "MySuffix"),
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System.Collections.Generic

Public Class SomeClass
    Inherits Dictionary(Of String, String)
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                    ExpectedDiagnostics =
                    {
                        GetCA1710BasicResultAt(4, 14, "SomeClass", "MySuffix"),
                    }
                }
            }.RunAsync();
        }

        [Fact, WorkItem(1818, "https://github.com/dotnet/roslyn-analyzers/issues/1818")]
        public async Task CA1710_DefaultValueForExclusion()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class FreezableList : ReadOnlyCollection<int>
{
    public FreezableList(IList<int> list) : base(list)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections
Imports System.Collections.Generic
Imports System.Collections.ObjectModel

Public Class FreezableList
    Inherits ReadOnlyCollection(Of Integer)

    Public Sub New(ByVal list As IList(Of Integer))
        MyBase.New(list)
    End Sub
End Class");
        }

        [Theory, WorkItem(1818, "https://github.com/dotnet/roslyn-analyzers/issues/1818")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader->{}")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader-> {}")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader->{} ")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader-> {} ")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader->{ }")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader-> { }")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader->{ } ")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader-> { } ")]
        [InlineData("dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader-> {     } ")]
        public async Task CA1710_AllowEmptySuffix(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Data;

public class SomeClass : IDataReader
{
    public object this[int i] => throw new NotImplementedException();

    public object this[string name] => throw new NotImplementedException();

    public int Depth => throw new NotImplementedException();

    public bool IsClosed => throw new NotImplementedException();

    public int RecordsAffected => throw new NotImplementedException();

    public int FieldCount => throw new NotImplementedException();

    public void Close() => throw new NotImplementedException();
    public void Dispose() => throw new NotImplementedException();
    public bool GetBoolean(int i) => throw new NotImplementedException();
    public byte GetByte(int i) => throw new NotImplementedException();
    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public char GetChar(int i) => throw new NotImplementedException();
    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public IDataReader GetData(int i) => throw new NotImplementedException();
    public string GetDataTypeName(int i) => throw new NotImplementedException();
    public DateTime GetDateTime(int i) => throw new NotImplementedException();
    public decimal GetDecimal(int i) => throw new NotImplementedException();
    public double GetDouble(int i) => throw new NotImplementedException();
    public Type GetFieldType(int i) => throw new NotImplementedException();
    public float GetFloat(int i) => throw new NotImplementedException();
    public Guid GetGuid(int i) => throw new NotImplementedException();
    public short GetInt16(int i) => throw new NotImplementedException();
    public int GetInt32(int i) => throw new NotImplementedException();
    public long GetInt64(int i) => throw new NotImplementedException();
    public string GetName(int i) => throw new NotImplementedException();
    public int GetOrdinal(string name) => throw new NotImplementedException();
    public DataTable GetSchemaTable() => throw new NotImplementedException();
    public string GetString(int i) => throw new NotImplementedException();
    public object GetValue(int i) => throw new NotImplementedException();
    public int GetValues(object[] values) => throw new NotImplementedException();
    public bool IsDBNull(int i) => throw new NotImplementedException();
    public bool NextResult() => throw new NotImplementedException();
    public bool Read() => throw new NotImplementedException();
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Data

Public Class SomeClass
    Implements System.Data.IDataReader

    Public ReadOnly Property Depth As Integer Implements IDataReader.Depth
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property IsClosed As Boolean Implements IDataReader.IsClosed
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property RecordsAffected As Integer Implements IDataReader.RecordsAffected
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property FieldCount As Integer Implements IDataRecord.FieldCount
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Default Public ReadOnly Property Item(i As Integer) As Object Implements IDataRecord.Item
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Default Public ReadOnly Property Item(name As String) As Object Implements IDataRecord.Item
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Sub Close() Implements IDataReader.Close
        Throw New NotImplementedException()
    End Sub

    Public Function GetSchemaTable() As DataTable Implements IDataReader.GetSchemaTable
        Throw New NotImplementedException()
    End Function

    Public Function NextResult() As Boolean Implements IDataReader.NextResult
        Throw New NotImplementedException()
    End Function

    Public Function Read() As Boolean Implements IDataReader.Read
        Throw New NotImplementedException()
    End Function

    Public Function GetBoolean(i As Integer) As Boolean Implements IDataRecord.GetBoolean
        Throw New NotImplementedException()
    End Function

    Public Function GetByte(i As Integer) As Byte Implements IDataRecord.GetByte
        Throw New NotImplementedException()
    End Function

    Public Function GetBytes(i As Integer, fieldOffset As Long, buffer() As Byte, bufferoffset As Integer, length As Integer) As Long Implements IDataRecord.GetBytes
        Throw New NotImplementedException()
    End Function

    Public Function GetChar(i As Integer) As Char Implements IDataRecord.GetChar
        Throw New NotImplementedException()
    End Function

    Public Function GetChars(i As Integer, fieldoffset As Long, buffer() As Char, bufferoffset As Integer, length As Integer) As Long Implements IDataRecord.GetChars
        Throw New NotImplementedException()
    End Function

    Public Function GetData(i As Integer) As IDataReader Implements IDataRecord.GetData
        Throw New NotImplementedException()
    End Function

    Public Function GetDataTypeName(i As Integer) As String Implements IDataRecord.GetDataTypeName
        Throw New NotImplementedException()
    End Function

    Public Function GetDateTime(i As Integer) As Date Implements IDataRecord.GetDateTime
        Throw New NotImplementedException()
    End Function

    Public Function GetDecimal(i As Integer) As Decimal Implements IDataRecord.GetDecimal
        Throw New NotImplementedException()
    End Function

    Public Function GetDouble(i As Integer) As Double Implements IDataRecord.GetDouble
        Throw New NotImplementedException()
    End Function

    Public Function GetFieldType(i As Integer) As Type Implements IDataRecord.GetFieldType
        Throw New NotImplementedException()
    End Function

    Public Function GetFloat(i As Integer) As Single Implements IDataRecord.GetFloat
        Throw New NotImplementedException()
    End Function

    Public Function GetGuid(i As Integer) As Guid Implements IDataRecord.GetGuid
        Throw New NotImplementedException()
    End Function

    Public Function GetInt16(i As Integer) As Short Implements IDataRecord.GetInt16
        Throw New NotImplementedException()
    End Function

    Public Function GetInt32(i As Integer) As Integer Implements IDataRecord.GetInt32
        Throw New NotImplementedException()
    End Function

    Public Function GetInt64(i As Integer) As Long Implements IDataRecord.GetInt64
        Throw New NotImplementedException()
    End Function

    Public Function GetName(i As Integer) As String Implements IDataRecord.GetName
        Throw New NotImplementedException()
    End Function

    Public Function GetOrdinal(name As String) As Integer Implements IDataRecord.GetOrdinal
        Throw New NotImplementedException()
    End Function

    Public Function GetString(i As Integer) As String Implements IDataRecord.GetString
        Throw New NotImplementedException()
    End Function

    Public Function GetValue(i As Integer) As Object Implements IDataRecord.GetValue
        Throw New NotImplementedException()
    End Function

    Public Function GetValues(values() As Object) As Integer Implements IDataRecord.GetValues
        Throw New NotImplementedException()
    End Function

    Public Function IsDBNull(i As Integer) As Boolean Implements IDataRecord.IsDBNull
        Throw New NotImplementedException()
    End Function

#Region ""IDisposable Support""
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                ' TODO: dispose managed state (managed objects).
            End If

            ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            ' TODO: set large fields to null.
        End If
        disposedValue = True
    End Sub

    ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    'Protected Overrides Sub Finalize()
    '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
    '    Dispose(False)
    '    MyBase.Finalize()
    'End Sub

    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(True)
        ' TODO: uncomment the following line if Finalize() is overridden above.
        ' GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(5035, "https://github.com/dotnet/roslyn-analyzers/issues/5035")]
        public async Task CA1710_AllowEmptySuffix2()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Collections;
using System.Collections.Generic;

public class C : IReadOnlyDictionary<string, object>
{
    public object this[string key] => throw new System.NotImplementedException();

    public IEnumerable<string> Keys => throw new System.NotImplementedException();

    public IEnumerable<object> Values => throw new System.NotImplementedException();

    public int Count => throw new System.NotImplementedException();

    public bool ContainsKey(string key)
    {
        throw new System.NotImplementedException();
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        throw new System.NotImplementedException();
    }

    public bool TryGetValue(string key, out object value)
    {
        throw new System.NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new System.NotImplementedException();
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Collections.Generic.IReadOnlyDictionary`2->{}
")  },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(5035, "https://github.com/dotnet/roslyn-analyzers/issues/5035")]
        public async Task CA1710_AllowEmptySuffix3()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Collections;
using System.Collections.Generic;

public class C : IReadOnlyDictionary<string, object>, ICollection<KeyValuePair<string, object>>
{
    public object this[string key] => throw new System.NotImplementedException();

    public IEnumerable<string> Keys => throw new System.NotImplementedException();

    public IEnumerable<object> Values => throw new System.NotImplementedException();

    public int Count => throw new System.NotImplementedException();

    public bool IsReadOnly => throw new System.NotImplementedException();

    public void Add(KeyValuePair<string, object> item) => throw new System.NotImplementedException();

    public void Clear() => throw new System.NotImplementedException();

    public bool Contains(KeyValuePair<string, object> item) => throw new System.NotImplementedException();

    public bool ContainsKey(string key) => throw new System.NotImplementedException();

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new System.NotImplementedException();

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw new System.NotImplementedException();

    public bool Remove(KeyValuePair<string, object> item) => throw new System.NotImplementedException();

    public bool TryGetValue(string key, out object value) => throw new System.NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Collections.Generic.IReadOnlyDictionary`2->{}
")  },
                    ExpectedDiagnostics = { GetCA1710CSharpResultAt(5, 14, "C", "Collection") }
                }
            }.RunAsync();
        }

        [Theory, WorkItem(3065, "https://github.com/dotnet/roslyn-analyzers/issues/3065")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA1710.exclude_indirect_base_types = false")]
        [InlineData("dotnet_code_quality.CA1710.exclude_indirect_base_types = true")]
        [InlineData("dotnet_code_quality.CA1710.exclude_indirect_base_types = invalid")]
        [InlineData(@"dotnet_code_quality.CA1710.exclude_indirect_base_types = true
                      dotnet_code_quality.CA1710.additional_required_suffixes = SomeClass->Suffix1")]
        [InlineData(@"dotnet_code_quality.CA1710.exclude_indirect_base_types = false
                      dotnet_code_quality.CA1710.additional_required_suffixes = SomeClass->Suffix1")]
        public async Task CA1710_ExcludeIndirectTypes(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class C : Exception {}
public class Sub : C {}

public class FreezableList : ReadOnlyCollection<int>
{
    public FreezableList(IList<int> list) : base(list)
    {
    }
}

public class SomeClass {}
public class SomeSubClass : SomeClass {}
public class SomeSubSubClass : SomeSubClass {}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                    ExpectedDiagnostics = { GetCA1710CSharpResultAt(7, 14, "C", "Exception") },
                }
            };

            if (editorConfigText.Contains("exclude_indirect_base_types = false", StringComparison.Ordinal))
            {
                csharpTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710CSharpResultAt(8, 14, "Sub", "Exception"),
                    GetCA1710CSharpResultAt(10, 14, "FreezableList", "Collection", "Dictionary', 'Set', 'Stack', 'Queue"),
                });

                if (editorConfigText.EndsWith("Suffix1", StringComparison.Ordinal))
                {
                    csharpTest.ExpectedDiagnostics.AddRange(new[]
                    {
                        GetCA1710CSharpResultAt(18, 14, "SomeSubClass", "Suffix1"),
                        GetCA1710CSharpResultAt(19, 14, "SomeSubSubClass", "Suffix1"),
                    });
                }
            }
            else
            {
                if (editorConfigText.EndsWith("Suffix1", StringComparison.Ordinal))
                {
                    csharpTest.ExpectedDiagnostics.Add(GetCA1710CSharpResultAt(18, 14, "SomeSubClass", "Suffix1"));
                }
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Collections.ObjectModel

Public Class C
    Inherits Exception
End Class

Public Class [Sub]
    Inherits C
End Class

Public Class FreezableList
    Inherits ReadOnlyCollection(Of Integer)

    Public Sub New(ByVal list As IList(Of Integer))
        MyBase.New(list)
    End Sub
End Class

Public Class SomeClass
End Class

Public Class SomeSubClass
    Inherits SomeClass
End Class

Public Class SomeSubSubClass
    Inherits SomeSubClass
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")  },
                    ExpectedDiagnostics = { GetCA1710BasicResultAt(7, 14, "C", "Exception") },
                }
            };

            if (editorConfigText.Contains("exclude_indirect_base_types = false", StringComparison.Ordinal))
            {
                vbTest.ExpectedDiagnostics.AddRange(new[]
                {
                    GetCA1710BasicResultAt(11, 14, "[Sub]", "Exception"),
                    GetCA1710BasicResultAt(15, 14, "FreezableList", "Collection", "Dictionary', 'Set', 'Stack', 'Queue"),
                });

                if (editorConfigText.EndsWith("Suffix1", StringComparison.Ordinal))
                {
                    vbTest.ExpectedDiagnostics.AddRange(new[]
                    {
                        GetCA1710BasicResultAt(26, 14, "SomeSubClass", "Suffix1"),
                        GetCA1710BasicResultAt(30, 14, "SomeSubSubClass", "Suffix1"),
                    });
                }
            }
            else
            {
                if (editorConfigText.EndsWith("Suffix1", StringComparison.Ordinal))
                {
                    vbTest.ExpectedDiagnostics.Add(GetCA1710BasicResultAt(26, 14, "SomeSubClass", "Suffix1"));
                }
            }

            await vbTest.RunAsync();
        }

        [Fact, WorkItem(3414, "https://github.com/dotnet/roslyn-analyzers/issues/3414")]
        public async Task CA1710_Interfaces()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
}");
        }

        [Fact]
        public async Task EventArgsNotInheritingFromSystemEventArgs_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
// Reproduce UWP specific EventArgs
namespace Windows.UI.Xaml
{
    public class RoutedEventArgs {}
}
public class C
{
    public delegate void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e);
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
' Reproduce UWP specific EventArgs
Namespace Windows.UI.Xaml
    Public Class RoutedEventArgs
    End Class
End Namespace

Public Class C
    Public Delegate Sub Page_Loaded(ByVal sender As Object, ByVal e As Windows.UI.Xaml.RoutedEventArgs)
End Class");
        }

        [Theory, WorkItem(4513, "https://github.com/dotnet/roslyn-analyzers/issues/4513")]
        [InlineData("")]
        [InlineData("Set")]
        [InlineData("Collection")]
        public async Task CA1710_ISet_IReadOnlySet(string typeNameSuffix)
        {
            var test = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

public class {|#0:First" + typeNameSuffix + @"|} : ISet<int>
{
    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public bool Add(int item)
    {
        throw new NotImplementedException();
    }

    public void Clear()
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

    public void ExceptWith(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public void IntersectWith(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsProperSubsetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsProperSupersetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsSubsetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsSupersetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool Overlaps(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool Remove(int item)
    {
        throw new NotImplementedException();
    }

    public bool SetEquals(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public void SymmetricExceptWith(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public void UnionWith(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    void ICollection<int>.Add(int item)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

public class {|#1:Second" + typeNameSuffix + @"|} : IReadOnlySet<int>
{
    public int Count => throw new NotImplementedException();

    public bool Contains(int item)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool IsProperSubsetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsProperSupersetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsSubsetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool IsSupersetOf(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool Overlaps(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    public bool SetEquals(IEnumerable<int> other)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}",
            };

            if (typeNameSuffix.Length == 0)
            {
                test.ExpectedDiagnostics.AddRange(new[]
                {
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(0)
                        .WithArguments("First", "Set", "Collection"),
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(1)
                        .WithArguments("Second", "Set", "Collection"),
                });
            }

            await test.RunAsync();
        }

        [Theory, WorkItem(4513, "https://github.com/dotnet/roslyn-analyzers/issues/4513")]
        [InlineData("")]
        [InlineData("Collection")]
        public async Task CA1710_IReadOnlyCollection(string typeNameSuffix)
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

public class {|#0:C" + typeNameSuffix + @"|} : IReadOnlyCollection<int>
{
    public int Count => throw new System.NotImplementedException();

    public IEnumerator<int> GetEnumerator() => throw new System.NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
}",
            };

            if (typeNameSuffix.Length == 0)
            {
                test.ExpectedDiagnostics.Add(
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(0)
                        .WithArguments("C", "Collection", "Dictionary', 'Set', 'Stack', 'Queue"));
            }

            await test.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Dictionary")]
        [InlineData("Collection")]
        public async Task CA1710_IDictionary_IDictionary2_IReadOnlyDictionary2(string typeNameSuffix)
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

public class {|#0:First" + typeNameSuffix + @"|} : IDictionary
{
    public object this[object key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool IsFixedSize => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public ICollection Keys => throw new NotImplementedException();

    public ICollection Values => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool IsSynchronized => throw new NotImplementedException();

    public object SyncRoot => throw new NotImplementedException();

    public void Add(object key, object value)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(object key)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(Array array, int index)
    {
        throw new NotImplementedException();
    }

    public IDictionaryEnumerator GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public void Remove(object key)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

public class {|#1:Second" + typeNameSuffix + @"|} : IDictionary<int, string>
{
    public string this[int key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public ICollection<int> Keys => throw new NotImplementedException();

    public ICollection<string> Values => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public void Add(int key, string value)
    {
        throw new NotImplementedException();
    }

    public void Add(KeyValuePair<int, string> item)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(KeyValuePair<int, string> item)
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(int key)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(KeyValuePair<int, string>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<int, string>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool Remove(int key)
    {
        throw new NotImplementedException();
    }

    public bool Remove(KeyValuePair<int, string> item)
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(int key, out string value)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

public class {|#2:Third" + typeNameSuffix + @"|} : IReadOnlyDictionary<int, string>
{
    public string this[int key] => throw new NotImplementedException();

    public IEnumerable<int> Keys => throw new NotImplementedException();

    public IEnumerable<string> Values => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool ContainsKey(int key)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<int, string>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(int key, out string value)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}",
            };

            if (typeNameSuffix.Length == 0)
            {
                test.ExpectedDiagnostics.AddRange(new[]
                {
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(0)
                        .WithArguments("First", "Dictionary", "Collection"),
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(1)
                        .WithArguments("Second", "Dictionary", "Collection"),
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(2)
                        .WithArguments("Third", "Dictionary", "Collection"),
                });
            }

            await test.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Queue")]
        [InlineData("Collection")]
        public async Task CA1710_Queue_Queue1(string typeNameSuffix)
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

public class {|#0:First" + typeNameSuffix + @"|} : Queue
{
}

public class {|#1:Second" + typeNameSuffix + @"|} : Queue<int>
{
}",
            };

            if (typeNameSuffix.Length == 0)
            {
                test.ExpectedDiagnostics.AddRange(new[]
                {
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(0)
                        .WithArguments("First", "Queue", "Collection"),
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(1)
                        .WithArguments("Second", "Queue", "Collection"),
                });
            }

            await test.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Stack")]
        [InlineData("Collection")]
        public async Task CA1710_Stack_Stack1(string typeNameSuffix)
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

public class {|#0:First" + typeNameSuffix + @"|} : Stack
{
}

public class {|#1:Second" + typeNameSuffix + @"|} : Stack<int>
{
}",
            };

            if (typeNameSuffix.Length == 0)
            {
                test.ExpectedDiagnostics.AddRange(new[]
                {
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(0)
                        .WithArguments("First", "Stack", "Collection"),
                    VerifyCS.Diagnostic(IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule)
                        .WithLocation(1)
                        .WithArguments("Second", "Stack", "Collection"),
                });
            }

            await test.RunAsync();
        }

        private static DiagnosticResult GetCA1710BasicResultAt(int line, int column, string typeName, string suffix, params string[] additionalSuffixes)
        {
            var args = new[] { typeName, suffix }.Concat(additionalSuffixes).ToArray();
            return VerifyVB.Diagnostic(additionalSuffixes.Length > 0 ? IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule : IdentifiersShouldHaveCorrectSuffixAnalyzer.OneSuffixRule)
                .WithLocation(line, column)
                .WithArguments(args);
        }

        private static DiagnosticResult GetCA1710CSharpResultAt(int line, int column, string typeName, string suffix, params string[] additionalSuffixes)
        {
            var args = new[] { typeName, suffix }.Concat(additionalSuffixes).ToArray();
            return VerifyCS.Diagnostic(additionalSuffixes.Length > 0 ? IdentifiersShouldHaveCorrectSuffixAnalyzer.MultipleSuffixesRule : IdentifiersShouldHaveCorrectSuffixAnalyzer.OneSuffixRule)
                .WithLocation(line, column)
                .WithArguments(args);
        }
    }
}
