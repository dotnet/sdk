// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotHaveIncorrectSuffixAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotHaveIncorrectSuffixFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotHaveIncorrectSuffixAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotHaveIncorrectSuffixFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldNotHaveIncorrectSuffixTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromAttribute()
        {
            VerifyCSharp(
@"public class MyBadAttribute {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadAttribute",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.AttributeSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromAttribute()
        {
            VerifyBasic(
@"Public Class MyBadAttribute
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadAttribute",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.AttributeSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromAttribute()
        {
            VerifyCSharp(
@"using System;

public class MyAttribute : Attribute {}
public class MyOtherAttribute : MyAttribute {}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromAttribute()
        {
            VerifyBasic(
@"Imports System

Public Class MyAttribute
    Inherits Attribute
End Class

Public Class MyOtherAttribute
    Inherits MyAttribute
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromEventArgs()
        {
            VerifyCSharp(
                "public class MyBadEventArgs {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEventArgs",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EventArgsSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromEventArgs()
        {
            VerifyBasic(
@"Public Class MyBadEventArgs
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEventArgs",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EventArgsSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromEventArgs()
        {
            VerifyCSharp(
@"using System;

public class MyEventArgs : EventArgs {}
public class MyOtherEventArgs : MyEventArgs {}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromEventArgs()
        {
            VerifyBasic(
@"Imports System

Public Class MyEventArgs
    Inherits EventArgs
End Class

Public Class MyOtherEventArgs
    Inherits MyEventArgs
End Class");
        }

        // There's no need for a test case where the type is derived from EventHandler
        // or EventHandler<T>, because they're sealed.
        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithEventHandler()
        {
            VerifyCSharp(
@"public class MyBadEventHandler {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEventHandler",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EventHandlerSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithEventHandler()
        {
            VerifyBasic(
@"Public Class MyBadEventHandler
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEventHandler",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EventHandlerSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromException()
        {
            VerifyCSharp(
@"public class MyBadException {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadException",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExceptionSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromException()
        {
            VerifyBasic(
@"Public Class MyBadException
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadException",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExceptionSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromException()
        {
            VerifyCSharp(
@"using System;

public class MyException : Exception {}
public class MyOtherException : MyException {}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromException()
        {
            VerifyBasic(
@"Imports System

Public Class MyException
    Inherits Exception
End Class

Public Class MyOtherException
    Inherits MyException
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromIPermission()
        {
            VerifyCSharp(
@"public class MyBadPermission {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadPermission",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.PermissionSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromIPermission()
        {
            VerifyBasic(
@"Public Class MyBadPermission
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadPermission",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.PermissionSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromIPermission()
        {
            VerifyCSharp(
@"using System.Security;

public class MyPermission : IPermission
{
    public IPermission Copy() { return null; }
    public void Demand() {}
    public void FromXml(SecurityElement e) {}
    public IPermission Intersect(IPermission other) { return null; }
    public bool IsSubsetOf(IPermission target) { return false; }
    public SecurityElement ToXml() { return null; }
    public IPermission Union(IPermission other) { return null; }
}

public class MyOtherPermission : MyPermission {}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromIPermission()
        {
            VerifyBasic(
@"Imports System.Security

Public Class MyPermission
    Implements IPermission

    Public Sub Demand() Implements IPermission.Demand
    End Sub

    Public Sub FromXml(e As SecurityElement) Implements ISecurityEncodable.FromXml
    End Sub

    Public Function Copy() As IPermission Implements IPermission.Copy
        Return Nothing
    End Function

    Public Function Intersect(target As IPermission) As IPermission Implements IPermission.Intersect
        Return Nothing
    End Function

    Public Function IsSubsetOf(target As IPermission) As Boolean Implements IPermission.IsSubsetOf
        Return False
    End Function

    Public Function ToXml() As SecurityElement Implements ISecurityEncodable.ToXml
        Return Nothing
    End Function

    Public Function Union(target As IPermission) As IPermission Implements IPermission.Union
        Return Nothing
    End Function
End Class

Public Class MyOtherPermission
    Inherits MyPermission
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromStream()
        {
            VerifyCSharp(
@"public class MyBadStream {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadStream",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.StreamSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromStream()
        {
            VerifyBasic(
@"Public Class MyBadStream
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadStream",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.StreamSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromStream()
        {
            VerifyCSharp(
@"using System.IO;

public class MyStream : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => 0L;

    public override long Position
    {
        get { return 0L; }
        set { }
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return 0L;
    }

    public override void SetLength(long value) { }

    public override void Write(byte[] buffer, int offset, int count) { }
}

public class MyOtherStream : MyStream { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromStream()
        {
            VerifyBasic(
@"Imports System.IO

Public Class MyStream
    Inherits Stream

    Public Overrides ReadOnly Property CanRead As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property CanSeek As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property CanWrite As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property Length As Long
        Get
            Return 0
        End Get
    End Property

    Public Overrides Property Position As Long
        Get
            Return 0
        End Get
        Set(value As Long)
        End Set
    End Property

    Public Overrides Sub Flush()
    End Sub

    Public Overrides Sub SetLength(value As Long)
    End Sub

    Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
    End Sub

    Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
        Return 0
    End Function

    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        Return 0
    End Function

    Public Sub SomeMethodNotPresentInStream()
    End Sub
End Class

Public Class MyOtherStream
    Inherits MyStream
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithDelegate()
        {
            VerifyCSharp(
@"public delegate void MyBadDelegate();",
                GetCSharpResultAt(
                    1, 22,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDelegate",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.DelegateSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithDelegate()
        {
            VerifyBasic(
@"Public Delegate Sub MyBadDelegate()",
                GetBasicResultAt(
                    1, 21,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDelegate",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.DelegateSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithEnum()
        {
            VerifyCSharp(
@"public enum MyBadEnum { X }",
                GetCSharpResultAt(
                    1, 13,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEnum",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EnumSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithEnum()
        {
            VerifyBasic(
@"Public Enum MyBadEnum
    X
End Enum",
                GetBasicResultAt(
                    1, 13,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadEnum",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.EnumSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithImpl()
        {
            VerifyCSharp(
@"public class MyClassImpl {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithImpl()
        {
            VerifyBasic(
@"Public Class MyClassImpl
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeNamingRulesAreCaseSensitiveEvenInVB()
        {
            VerifyBasic(
@"Public Class MyClassimpl
End Class");
        }

        [Fact]
        public void CSharp_No_Diagnostic_MisnamedTypeIsInternal()
        {
            VerifyCSharp(
@"internal class MyClassImpl {}");
        }

        [Fact]
        public void Basic_No_Diagnostic_MisnamedTypeIsInternal()
        {
            VerifyBasic(
@"Friend Class MyClassImpl
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MisnamedTypeIsNestedPublic()
        {
            VerifyCSharp(
@"public class MyClass
{
    public class MyNestedClassImpl {}
}",
                GetCSharpResultAt(
                    3, 18,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyNestedClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MisnamedTypeIsNestedPublic()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public Class MyNestedClassImpl
    End Class
End Class",
                GetBasicResultAt(
                    2, 18,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyNestedClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MisnamedTypeIsNestedProtected()
        {
            VerifyCSharp(
@"public class MyClass
{
    protected class MyNestedClassImpl {}
}",
                GetCSharpResultAt(
                    3, 21,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyNestedClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MisnamedTypeIsNestedProtected()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Protected Class MyNestedClassImpl
    End Class
End Class",
                GetBasicResultAt(
                    2, 21,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyNestedClassImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_MisnamedTypeIsNestedPrivate()
        {
            VerifyCSharp(
@"public class MyClass
{
    private class MyNestedClassImpl {}
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_MisnamedTypeIsNestedPrivate()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Private Class MyNestedClassImpl
    End Class
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromDictionary()
        {
            VerifyCSharp(
@"public class MyBadDictionary {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDictionary",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.DictionarySuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromDictionary()
        {
            VerifyBasic(
@"Public Class MyBadDictionary
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDictionary",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.DictionarySuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsIReadOnlyDictionary()
        {
            VerifyCSharp(
@"using System.Collections;
using System.Collections.Generic;

public class MyReadOnlyDictionary : IReadOnlyDictionary<string, int>
{
    public int this[string key] => 0;
    public int Count => 0;
    public IEnumerable<string> Keys => new string[0];
    public IEnumerable<int> Values => new int[0];
    
    public bool ContainsKey(string key) { return false; }
    public IEnumerator<KeyValuePair<string, int>> GetEnumerator() { return null; }
    IEnumerator IEnumerable.GetEnumerator() { return null; }

    public bool TryGetValue(string key, out int value)
    {
        value = -1;
        return false;
    }
}");
        }

        [Fact]
        public void CA1711_BasicNoDiagnostic_TypeImplementsIReadOnlyDictionary()
        {
            VerifyBasic(
@"Imports System.Collections
Imports System.Collections.Generic

Public Class MyReadOnlyDictionary
    Implements IReadOnlyDictionary(Of String, Integer)

    Public ReadOnly Property Count As Integer Implements IReadOnlyCollection(Of KeyValuePair(Of String, Integer)).Count
        Get
            Return 0
        End Get
    End Property

    Default Public ReadOnly Property Item(key As String) As Integer Implements IReadOnlyDictionary(Of String, Integer).Item
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property Keys As IEnumerable(Of String) Implements IReadOnlyDictionary(Of String, Integer).Keys
        Get
            Return New String() { }
        End Get
    End Property

    Public ReadOnly Property Values As IEnumerable(Of Integer) Implements IReadOnlyDictionary(Of String, Integer).Values
        Get
            Return New Integer() { }
        End Get
    End Property

    Public Function ContainsKey(key As String) As Boolean Implements IReadOnlyDictionary(Of String, Integer).ContainsKey
        Return False
    End Function

    Public Function GetEnumerator() As IEnumerator(Of KeyValuePair(Of String, Integer)) Implements IEnumerable(Of KeyValuePair(Of String, Integer)).GetEnumerator
        Return Nothing
    End Function

    Public Function TryGetValue(key As String, ByRef value As Integer) As Boolean Implements IReadOnlyDictionary(Of String, Integer).TryGetValue
        value = -1
        Return False
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsGenericIDictionary()
        {
            VerifyCSharp(
@"using System;
using System.Collections;
using System.Collections.Generic;

public class MyGenericDictionary : IDictionary<string, string>
{
    public string this[string key]
    {
        get { return null; }
        set { }
    }

    public int Count => 0;
    public bool IsReadOnly => true;
    public ICollection<string> Keys => null;
    public ICollection<string> Values => null;
    public void Add(string key, string value) { }
    public void Add(KeyValuePair<string, string> item) { }
    public void Clear() { }
    public void CopyTo(KeyValuePair<string, string>[] array, int index) { }

    public bool Remove(string key) 
    { 
        return false;
    }

    public bool Remove(KeyValuePair<string, string> item) 
    { 
        return false;
    }

    public bool TryGetValue(string key, out string value)
    {
        value = null;
        return false;
    }

    public bool Contains(KeyValuePair<string, string> item)
    {
        return false;
    }

    public bool ContainsKey(string key)
    {
        return false;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return null;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeImplementsGenericIDictionary()
        {
            VerifyBasic(
@"Imports System.Collections
Imports System.Collections.Generic

Public Class MyGenericDictionary
    Implements IDictionary(Of String, String)

    Default Public Property Item(key As String) As String Implements IDictionary(Of String, String).Item
        Get
            Return Nothing
        End Get
        Set(value As String)
        End Set
    End Property

    Public ReadOnly Property Count As Integer Implements ICollection(Of KeyValuePair(Of String, String)).Count
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of KeyValuePair(Of String, String)).IsReadOnly
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property Keys As ICollection(Of String) Implements IDictionary(Of String, String).Keys
        Get
            Return Nothing
        End Get
    End Property

    Public ReadOnly Property Values As ICollection(Of String) Implements IDictionary(Of String, String).Values
        Get
            Return Nothing
        End Get
    End Property

    Public Sub Add(key As String, value As String) Implements IDictionary(Of String, String).Add
    End Sub

    Public Sub Add(item As KeyValuePair(Of String, String)) Implements ICollection(Of KeyValuePair(Of String, String)).Add
    End Sub

    Public Sub Clear() Implements ICollection(Of KeyValuePair(Of String, String)).Clear
    End Sub

    Public Sub CopyTo(array() As KeyValuePair(Of String, String), arrayIndex As Integer) Implements ICollection(Of KeyValuePair(Of String, String)).CopyTo
    End Sub

    Public Function Remove(key As String) As Boolean Implements IDictionary(Of String, String).Remove
        Return False
    End Function

    Public Function Remove(item As KeyValuePair(Of String, String)) As Boolean Implements ICollection(Of KeyValuePair(Of String, String)).Remove
        Return False
    End Function

    Public Function TryGetValue(key As String, ByRef value As String) As Boolean Implements IDictionary(Of String, String).TryGetValue
        Return False
    End Function

    Public Function Contains(item As KeyValuePair(Of String, String)) As Boolean Implements ICollection(Of KeyValuePair(Of String, String)).Contains
        Return False
    End Function

    Public Function ContainsKey(key As String) As Boolean Implements IDictionary(Of String, String).ContainsKey
        Return False
    End Function

    Public Function GetEnumerator() As IEnumerator(Of KeyValuePair(Of String, String)) Implements IEnumerable(Of KeyValuePair(Of String, String)).GetEnumerator
        Return Nothing
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsNonGenericIDictionary()
        {
            VerifyCSharp(
@"using System;
using System.Collections;
using System.Runtime.Serialization;

public class MyNonGenericDictionary : IDictionary
{
    protected MyNonGenericDictionary(SerializationInfo info, StreamingContext context) { }

    public object this[object key]
    {
        get { return null; }
        set { }
    }

    public int Count => 0;
    public bool IsFixedSize => true;
    public bool IsReadOnly => true;
    public bool IsSynchronized => false;
    public ICollection Keys => null;
    public object SyncRoot => null;
    public ICollection Values => null;
    public void Add(object key, object value) { }
    public void Clear() { }
    public void CopyTo(Array array, int index) { }
    public void Remove(object key) { }

    public bool Contains(object key)
    {
        return false;
    }

    public IDictionaryEnumerator GetEnumerator()
    {
        return null;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeImplementsNonGenericIDictionary()
        {
            VerifyBasic(
@"Imports System
Imports System.Collections
Imports System.Runtime.Serialization

Public Class MyNonGenericDictionary
    Implements IDictionary

    Protected Sub New(info As SerializationInfo, context As StreamingContext)
    End Sub

    Public ReadOnly Property Count As Integer Implements ICollection.Count
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property IsFixedSize As Boolean Implements IDictionary.IsFixedSize
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property IsReadOnly As Boolean Implements IDictionary.IsReadOnly
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized
        Get
            Return False
        End Get
    End Property

    Default Public Property Item(key As Object) As Object Implements IDictionary.Item
        Get
            Return Nothing
        End Get
        Set(value As Object)
        End Set
    End Property

    Public ReadOnly Property Keys As ICollection Implements IDictionary.Keys
        Get
            Return Nothing
        End Get
    End Property

    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
        Get
            Return Nothing
        End Get
    End Property

    Public ReadOnly Property Values As ICollection Implements IDictionary.Values
        Get
            Return Nothing
        End Get
    End Property

    Public Sub Add(key As Object, value As Object) Implements IDictionary.Add
    End Sub

    Public Sub Clear() Implements IDictionary.Clear
    End Sub

    Public Sub CopyTo(array As Array, index As Integer) Implements ICollection.CopyTo
    End Sub

    Public Sub Remove(key As Object) Implements IDictionary.Remove
    End Sub

    Public Function Contains(key As Object) As Boolean Implements IDictionary.Contains
        Return False
    End Function

    Public Function GetEnumerator() As IDictionaryEnumerator Implements IDictionary.GetEnumerator
        Return Nothing
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromGenericDictionary()
        {
            VerifyCSharp(
@"using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[Serializable]
public class MyGenericDictionary<K, V> : Dictionary<K, V>
{
    protected MyGenericDictionary(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromGenericDictionary()
        {
            VerifyBasic(
@"Imports System
Imports System.Collections.Generic
Imports System.Runtime.Serialization

<Serializable>
Public Class MyGenericDictionary(Of K, V)
    Inherits Dictionary(Of K, V)
    Protected Sub New(info As SerializationInfo, context As StreamingContext)
        MyBase.New(info, context)
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromPartiallyInstantiatedGenericDictionary()
        {
            VerifyCSharp(
@"using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[Serializable]
public class MyStringDictionary<V> : Dictionary<string, V>
{
    protected MyStringDictionary(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromPartiallyInstantiatedGenericDictionary()
        {
            VerifyBasic(
@"Imports System
Imports System.Collections.Generic
Imports System.Runtime.Serialization

<Serializable>
Public Class MyStringDictionary(Of V)
    Inherits Dictionary(Of String, V)
    Protected Sub New(info As SerializationInfo, context As StreamingContext)
        MyBase.New(info, context)
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromFullyInstantiatedGenericDictionary()
        {
            VerifyCSharp(
@"using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[Serializable]
public class MyStringToIntDictionary : Dictionary<string, int>
{
    protected MyStringToIntDictionary(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromFullyInstantiatedGenericDictionary()
        {
            VerifyBasic(
@"Imports System
Imports System.Collections.Generic
Imports System.Runtime.Serialization

<Serializable>
Public Class MyStringToIntDictionary
    Inherits Dictionary(Of String, Integer)
    Protected Sub New(info As SerializationInfo, context As StreamingContext)
        MyBase.New(info, context)
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromCollection()
        {
            VerifyCSharp(
@"public class MyBadCollection {}",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromCollection()
        {
            VerifyBasic(
@"Public Class MyBadCollection
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsNonGenericICollection()
        {
            VerifyCSharp(
@"using System.Collections;

public class MyNonGenericCollection : ICollection
{
    public int Count => 0;
    public bool IsSynchronized => true;
    public object SyncRoot => null;
    public void CopyTo(System.Array array, int index) { }

    public IEnumerator GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeImplementsNonGenericICollection()
        {
            VerifyBasic(
@"Imports System.Collections

Public Class MyNonGenericCollection
    Implements ICollection

    Public ReadOnly Property Count As Integer Implements ICollection.Count
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property IsSynchronized As Boolean Implements ICollection.IsSynchronized
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SyncRoot As Object Implements ICollection.SyncRoot
        Get
            Return Nothing
        End Get
    End Property

    Public Sub CopyTo(array As System.Array, index As Integer) Implements ICollection.CopyTo
    End Sub

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class
");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsNonGenericIEnumerable()
        {
            VerifyCSharp(
@"using System.Collections;

public class MyEnumerableCollection : IEnumerable
{
    public IEnumerator GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeImplementsNonGenericIEnumerable()
        {
            VerifyBasic(
@"Imports System.Collections

Public Class MyEnumerableCollection
    Implements IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeImplementsInstantiatedGenericICollection()
        {
            VerifyCSharp(
@"using System.Collections;
using System.Collections.Generic;

public class MyIntCollection : ICollection<int>
{
    public int Count => 0;
    public bool IsReadOnly => true;

    public void Add(int item) { }
    public void Clear() { }

    public bool Contains(int item)
    {
        return false;
    }

    public void CopyTo(int[] array, int arrayIndex) { }

    public IEnumerator<int> GetEnumerator()
    {
        return null;
    }

    public bool Remove(int item)
    {
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return null;
    }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeImplementsInstantiatedGenericICollection()
        {
            VerifyBasic(
@"Imports System.Collections
Imports System.Collections.Generic

Public Class MyIntCollection
    Implements ICollection(Of Integer)

    Public ReadOnly Property Count As Integer Implements ICollection(Of Integer).Count
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property IsReadOnly As Boolean Implements ICollection(Of Integer).IsReadOnly
        Get
            Return True
        End Get
    End Property

    Public Sub Add(item As Integer) Implements ICollection(Of Integer).Add
    End Sub

    Public Sub Clear() Implements ICollection(Of Integer).Clear
    End Sub

    Public Sub CopyTo(array() As Integer, arrayIndex As Integer) Implements ICollection(Of Integer).CopyTo
    End Sub

    Public Function Contains(item As Integer) As Boolean Implements ICollection(Of Integer).Contains
        Return False
    End Function

    Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Return Nothing
    End Function

    Public Function Remove(item As Integer) As Boolean Implements ICollection(Of Integer).Remove
        Return False
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromNonGenericQueue()
        {
            VerifyCSharp(
@"using System.Collections;

public class MyNonGenericQueue : Queue { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromNonGenericQueue()
        {
            VerifyBasic(
@"Imports System.Collections

Public Class MyNonGenericQueue
    Inherits Queue
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromGenericQueue()
        {
            VerifyCSharp(
@"using System.Collections.Generic;

public class MyGenericQueue<T> : Queue<T> { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromGenericQueue()
        {
            VerifyBasic(
@"Imports System.Collections.Generic

Public Class MyGenericQueue(Of T)
    Inherits Queue(Of T)
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromInstantiatedGenericQueue()
        {
            VerifyCSharp(
@"using System.Collections.Generic;

public class MyIntQueue : Queue<int> { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromInstantiatedGenericQueue()
        {
            VerifyBasic(
@"Imports System.Collections.Generic

Public Class MyIntQueue
    Inherits Queue(Of Integer)
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromQueue()
        {
            VerifyCSharp(
@"public class MyBadQueue { }",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadQueue",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.QueueSuffix)
                );
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromQueue()
        {
            VerifyBasic(
@"Public Class MyBadQueue
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadQueue",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.QueueSuffix)
                );
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromNonGenericStack()
        {
            VerifyCSharp(
@"using System.Collections;

public class MyNonGenericStack : Stack { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromNonGenericStack()
        {
            VerifyBasic(
@"Imports System.Collections

Public Class MyNonGenericStack
    Inherits Stack
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromGenericStack()
        {
            VerifyCSharp(
@"using System.Collections.Generic;

public class MyGenericStack<T> : Stack<T> { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromGenericStack()
        {
            VerifyBasic(
@"Imports System.Collections.Generic

Public Class MyGenericStack(Of T)
    Inherits Stack(Of T)
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeDerivesFromInstantiatedGenericStack()
        {
            VerifyCSharp(
@"using System.Collections.Generic;

public class MyIntStack : Stack<int> { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeDerivesFromInstantiatedGenericStack()
        {
            VerifyBasic(
@"Imports System.Collections.Generic

Public Class MyIntStack
    Inherits Stack(Of Integer)
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDoesNotDeriveFromStack()
        {
            VerifyCSharp(
@"public class MyBadStack { }",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadStack",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.StackSuffix)
                );
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDoesNotDeriveFromStack()
        {
            VerifyBasic(
@"Public Class MyBadStack
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadStack",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.StackSuffix)
                );
        }

        // MSDN says that DataSet and DataTable can be called "Collection",
        // but FxCop disagrees.
        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDerivesFromDataSet()
        {
            VerifyCSharp(
@"using System;
using System.Data;

[Serializable]
public class MyBadDataSetCollection : DataSet { }",
                GetCSharpResultAt(
                    5, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDataSetCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDerivesFromDataSet()
        {
            VerifyBasic(
@"Imports System
Imports System.Data

<Serializable>
Public Class MyBadDataSetCollection
    Inherits DataSet
End Class",
                GetBasicResultAt(
                    5, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDataSetCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeDerivesFromDataTable()
        {
            VerifyCSharp(
@"using System;
using System.Data;

[Serializable]
public class MyBadDataTableCollection : DataTable { }",
                GetCSharpResultAt(
                    5, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDataTableCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeDerivesFromDataTable()
        {
            VerifyBasic(
@"Imports System
Imports System.Data

<Serializable>
Public Class MyBadDataTableCollection
    Inherits DataTable
End Class",
                GetBasicResultAt(
                    5, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNoAlternateRule,
                    "MyBadDataTableCollection",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CollectionSuffix));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyClassEx { }",
                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyClassEx"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class MyClassEx
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyClassEx"));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_TypeNameNameIsEx()
        {
            VerifyCSharp(
@"public class Ex { }");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_TypeNameNameIsEx()
        {
            VerifyBasic(
@"Public Class Ex
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_TypeNameEndsWithNew()
        {
            VerifyCSharp(
@"public class MyClassNew { }",

                GetCSharpResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyClassNew"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_TypeNameEndsWithNew()
        {
            VerifyBasic(
@"Public Class MyClassNew
End Class",
                GetBasicResultAt(
                    1, 14,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.TypeNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyClassNew"));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_MethodNameEndsWithNew()
        {
            VerifyCSharp(
@"public class MyClass
{
    public void MyMethodNew() { }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_MethodNameEndsWithNew()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public Sub MyMethodNew()
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MethodNameEndsWithNewAndMethodNameWithoutNewExistsInSameClass()
        {
            VerifyCSharp(
@"public class MyBaseClass
{
    public void MyMethod() { }
    public void MyMethodNew() { }
}",
                GetCSharpResultAt(
                    4, 17,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyMethodNew"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MethodNameEndsWithNewAndMethodNameWithoutNewExistsInSameClass()
        {
            VerifyBasic(
@"Public Class MyBaseClass
    Public Sub MyMethod()
    End Sub

    Public Sub MyMethodNew()
    End Sub
End Class",
                GetBasicResultAt(
                    5, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyMethodNew"));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MethodNameEndsWithNewAndMethodNameWithoutNewExistsInAncestorClass()
        {
            VerifyCSharp(
@"public class MyBaseClass
{
    public void MyMethod() { }
}

public class MyDerivedClass : MyBaseClass
{
}

public class MyClass : MyDerivedClass
{
    public void MyMethodNew() { }
}",
                GetCSharpResultAt(
                    12, 17,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyMethodNew"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MethodNameEndsWithNewAndMethodNameWithoutNewExistsInAncestorClass()
        {
            VerifyBasic(
@"Public Class MyBaseClass
    Public Sub MyMethod()
    End Sub
End Class

Public Class MyDerivedClass
    Inherits MyBaseClass
End Class

Public Class [MyClass]
    Inherits MyDerivedClass

    Public Sub MyMethodNew()
    End Sub
End Class",
                GetBasicResultAt(
                    13, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.NewSuffix,
                    "MyMethodNew"));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_MethodNameEndingWithNewImplementsInterfaceMethod()
        {
            VerifyCSharp(
@"public interface MyBaseInterface
{
    void MyMethodNew();
}

public interface MyDerivedInterface : MyBaseInterface
{
}

public class MyClass : MyDerivedInterface
{
    public void MyMethodNew() { }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_MethodNameEndsWithNewAndMethodNameWithoutNewExistsInImplementedInterface()
        {
            VerifyBasic(
@"Public Interface MyBaseInterface
    Sub MyMethodNew()
End Interface

Public Interface MyDerivedInterface
    Inherits MyBaseInterface
End Interface

Public Class [MyClass]
    Implements MyDerivedInterface
    Public Sub MyMethodNew() Implements MyBaseInterface.MyMethodNew
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MethodNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyClass
{
    public void MyMethodEx() { }
}",
                GetCSharpResultAt(
                    3, 17,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
            ;
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MethodNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public Sub MyMethodEx()
    End Sub
End Class",
                GetBasicResultAt(
                    2, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_PrivateMethodNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyClass
{
    private void MyMethodEx() { }
}");
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_PrivateMethodNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Private Sub MyMethodEx()
    End Sub
End Class");
        }

        [Fact]
        public void CA1711_CSharp_NoDiagnostic_OverriddenMethodNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyBaseClass
{
    public virtual void MyMethodEx() { }
}

public class MyClass : MyBaseClass
{
    public override void MyMethodEx() { }
}",
                // Diagnostic for the base class method, but none for the override.
                GetCSharpResultAt(
                    3, 25,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_MethodNameEndingWithExImplementsInterfaceMethod()
        {
            VerifyCSharp(
@"public interface MyBaseInterface
{
    void MyMethodEx();
}

public interface MyDerivedInterface : MyBaseInterface
{
}

public class MyClass : MyDerivedInterface
{
    public void MyMethodEx() { }
}",
                // Diagnostic for the interface method, but none for the implementation.
                GetCSharpResultAt(
                    3, 10,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MethodNameEndingWithExImplementsInterfaceMethod()
        {
            VerifyBasic(
@"Public Interface MyBaseInterface
    Sub MyMethodEx()
End Interface

Public Interface MyDerivedInterface
    Inherits MyBaseInterface
End Interface

Public Class [MyClass]
    Implements MyDerivedInterface
    Public Sub MyMethodEx() Implements MyBaseInterface.MyMethodEx
    End Sub
End Class",
                // Diagnostic for the interface method, but none for the implementation.
                GetBasicResultAt(
                    2, 9,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
        }


        [Fact]
        public void CA1711_CSharp_Diagnostic_MethodNameEndsWithImpl()
        {
            VerifyCSharp(
@"public class MyClass
{
    public void MyMethodImpl() { }
}",
                GetCSharpResultAt(
                    3, 17,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyMethodImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_MethodNameEndsWithImpl()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public Sub MyMethodImpl()
    End Sub
End Class",
                GetBasicResultAt(
                    2, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberWithAlternateRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ImplSuffix,
                    "MyMethodImpl",
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.CoreSuffix));
        }

        [Fact]
        public void CA1711_Basic_NoDiagnostic_OverriddenMethodNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class MyBaseClass
    Public Overridable Sub MyMethodEx()
    End Sub
End Class

Public Class [MyClass]
    Inherits MyBaseClass

    Public Overrides Sub MyMethodEx()
    End Sub
End Class",
                // Diagnostic for the base class method, but none for the override.
                GetBasicResultAt(
                    2, 28,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyMethodEx"));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_EventNameEndsWithEx()
        {
            VerifyCSharp(
@"using System;

public class MyClass
{
    public delegate void EventCallback(object sender, EventArgs e);
    public event EventCallback MyEventEx;
}",
                GetCSharpResultAt(
                    6, 32,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyEventEx"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_EventNameEndsWithEx()
        {
            VerifyBasic(
@"Imports System

Public Class [MyClass]
    Public Delegate Sub EventCallback(sender As Object, e As EventArgs)
    Public Event MyEventEx As EventCallback
End Class",
                GetBasicResultAt(
                    5, 18,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyEventEx"));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_PropertyNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyClass
{
    public int MyPropertyEx { get; set; }
}",
                GetCSharpResultAt(
                    3, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyPropertyEx"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_PropertyNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public Property MyPropertyEx As Integer
        Get
            Return 0
        End Get
        Set(value As Integer)
        End Set
    End Property
End Class",
                GetBasicResultAt(
                    2, 21,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyPropertyEx"));
        }

        [Fact]
        public void CA1711_CSharp_Diagnostic_FieldNameEndsWithEx()
        {
            VerifyCSharp(
@"public class MyClass
{
    public int MyFieldEx;
}",
                GetCSharpResultAt(
                    3, 16,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyFieldEx"));
        }

        [Fact]
        public void CA1711_Basic_Diagnostic_FieldNameEndsWithEx()
        {
            VerifyBasic(
@"Public Class [MyClass]
    Public MyFieldEx As Integer
End Class",
                GetBasicResultAt(
                    2, 12,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.MemberNewerVersionRule,
                    IdentifiersShouldNotHaveIncorrectSuffixAnalyzer.ExSuffix,
                    "MyFieldEx"));
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotHaveIncorrectSuffixAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotHaveIncorrectSuffixAnalyzer();
        }
    }
}