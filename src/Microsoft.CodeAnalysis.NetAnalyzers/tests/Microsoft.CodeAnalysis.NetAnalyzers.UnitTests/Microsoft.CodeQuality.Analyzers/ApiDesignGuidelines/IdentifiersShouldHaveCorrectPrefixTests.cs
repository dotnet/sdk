// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldHaveCorrectPrefixTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IdentifiersShouldHaveCorrectPrefixAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new IdentifiersShouldHaveCorrectPrefixAnalyzer();
        }

        [Fact]
        public void TestInterfaceNamesCSharp()
        {
            VerifyCSharp(@"
public interface Controller
{
    void SomeMethod();
}

public interface 日本語
{
    void SomeMethod();
}

public interface _Controller
{
    void SomeMethod();
}

public interface _日本語
{
    void SomeMethod();
}

public interface Internet
{
    void SomeMethod();
}

public interface Iinternet
{
    void SomeMethod();
}

public class Class1
{
    public interface Controller
    {
        void SomeMethod();
    }
}

public interface IAmAnInterface
{
    void SomeMethod();
}
",
                GetCA1715InterfaceCSharpResultAt(2, 18, "Controller"),
                GetCA1715InterfaceCSharpResultAt(7, 18, "\u65E5\u672C\u8A9E"),
                GetCA1715InterfaceCSharpResultAt(12, 18, "_Controller"),
                GetCA1715InterfaceCSharpResultAt(17, 18, "_\u65E5\u672C\u8A9E"),
                GetCA1715InterfaceCSharpResultAt(22, 18, "Internet"),
                GetCA1715InterfaceCSharpResultAt(27, 18, "Iinternet"),
                GetCA1715InterfaceCSharpResultAt(34, 22, "Controller"));
        }

        [Fact]
        public void TestTypeParameterNamesCSharp()
        {
            VerifyCSharp(@"
using System;

public class IInterface<VSome>
{
}

public class IAnotherInterface<本語>
{
}

public delegate void Callback<VSome>();

public class Class2<VSome>
{
}

public class Class2<T, VSome>
{
}

public class Class3<Type>
{
}

public class Class3<T, Type>
{
}

public class Base<Key, Value>
{
}

public class Derived<Key, Value> : Base<Key, Value>
{
}

public class Class4<Type1>
{
    public void AnotherMethod<Type2>()
    {
        Console.WriteLine(typeof(Type2).ToString());
    }

    public void Method<Type2>(Type2 type)
    {
        Console.WriteLine(type);
    }

    public void Method<KType, VType>(KType key, VType value)
    {
        Console.WriteLine(key.ToString() + value.ToString());
    }
}

public class Class5<_Type1>
{
    public void Method<_K, _V>(_K key, _V value)
    {
        Console.WriteLine(key.ToString() + value.ToString());
    }
}

public class Class6<TTypeParameter>
{
}
",
                GetCA1715TypeParameterCSharpResultAt(4, 25, "VSome"),
                GetCA1715TypeParameterCSharpResultAt(8, 32, "\u672C\u8A9E"),
                GetCA1715TypeParameterCSharpResultAt(12, 31, "VSome"),
                GetCA1715TypeParameterCSharpResultAt(14, 21, "VSome"),
                GetCA1715TypeParameterCSharpResultAt(18, 24, "VSome"),
                GetCA1715TypeParameterCSharpResultAt(22, 21, "Type"),
                GetCA1715TypeParameterCSharpResultAt(26, 24, "Type"),
                GetCA1715TypeParameterCSharpResultAt(30, 19, "Key"),
                GetCA1715TypeParameterCSharpResultAt(30, 24, "Value"),
                GetCA1715TypeParameterCSharpResultAt(34, 22, "Key"),
                GetCA1715TypeParameterCSharpResultAt(34, 27, "Value"),
                GetCA1715TypeParameterCSharpResultAt(38, 21, "Type1"),
                GetCA1715TypeParameterCSharpResultAt(40, 31, "Type2"),
                GetCA1715TypeParameterCSharpResultAt(45, 24, "Type2"),
                GetCA1715TypeParameterCSharpResultAt(50, 24, "KType"),
                GetCA1715TypeParameterCSharpResultAt(50, 31, "VType"),
                GetCA1715TypeParameterCSharpResultAt(56, 21, "_Type1"),
                GetCA1715TypeParameterCSharpResultAt(58, 24, "_K"),
                GetCA1715TypeParameterCSharpResultAt(58, 28, "_V"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestInternalInterfaceNamesCSharp_NoDiagnostic()
        {
            VerifyCSharp(@"
internal interface Controller
{
    void SomeMethod();
}

internal class C
{
    public interface 日本語
    {
        void SomeMethod();
    }
}

public class C2
{
    private interface _Controller
    {
        void SomeMethod();
    }
}
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestTypeParameterNamesInternalCSharp_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;

internal class IInterface<VSome>
{
}

internal class C
{
    public class IAnotherInterface<本語>
    {
    }
}

public class C2
{
    private delegate void Callback<VSome>();
}
");
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Fact]
        public void TestTypeParameterNamesCSharp_SingleLetterCases_Default()
        {
            VerifyCSharp(@"
using System;

public class IInterface<V>
{
}

public delegate void Callback<V>();

public class Class2<T, V>
{
}

public class Class4<T>
{
    public void Method<K, V>(K key, V value)
    {
        Console.WriteLine(key.ToString() + value.ToString());
    }
}

public class Class6<TTypeParameter>
{
}
",
            GetCA1715TypeParameterCSharpResultAt(4, 25, "V"),
            GetCA1715TypeParameterCSharpResultAt(8, 31, "V"),
            GetCA1715TypeParameterCSharpResultAt(10, 24, "V"),
            GetCA1715TypeParameterCSharpResultAt(16, 24, "K"),
            GetCA1715TypeParameterCSharpResultAt(16, 27, "V"));
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Theory]
        [InlineData(@"")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = false")]
        [InlineData(@"dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = false")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = true
                      dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = false")]
        public void TestTypeParameterNamesCSharp_SingleLetterCases_EditorConfig_Diagnostic(string editorConfigText)
        {
            VerifyCSharp(@"
using System;

public class IInterface<V>
{
}

public delegate void Callback<V>();

public class Class2<T, V>
{
}

public class Class4<T>
{
    public void Method<K, V>(K key, V value)
    {
        Console.WriteLine(key.ToString() + value.ToString());
    }
}

public class Class6<TTypeParameter>
{
}
",
            GetEditorConfigAdditionalFile(editorConfigText),
            GetCA1715TypeParameterCSharpResultAt(4, 25, "V"),
            GetCA1715TypeParameterCSharpResultAt(8, 31, "V"),
            GetCA1715TypeParameterCSharpResultAt(10, 24, "V"),
            GetCA1715TypeParameterCSharpResultAt(16, 24, "K"),
            GetCA1715TypeParameterCSharpResultAt(16, 27, "V"));
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Theory]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = true")]
        [InlineData(@"dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = false
                      dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true")]
        public void TestTypeParameterNamesCSharp_SingleLetterCases_EditorConfig_NoDiagnostic(string editorConfigText)
        {
            VerifyCSharp(@"
using System;

public class IInterface<V>
{
}

public delegate void Callback<V>();

public class Class2<T, V>
{
}

public class Class4<T>
{
    public void Method<K, V>(K key, V value)
    {
        Console.WriteLine(key.ToString() + value.ToString());
    }
}

public class Class6<TTypeParameter>
{
}
", GetEditorConfigAdditionalFile(editorConfigText));
        }

        [Fact]
        public void TestInterfaceNamesBasic()
        {
            VerifyBasic(@"
Public Interface Controller
    Sub SomeMethod()
End Interface

Public Interface 日本語
    Sub SomeMethod()
End Interface

Public Interface _Controller
    Sub SomeMethod()
End Interface

Public Interface _日本語
    Sub SomeMethod()
End Interface

Public Interface Internet
    Sub SomeMethod()
End Interface

Public Interface Iinternet
    Sub SomeMethod()
End Interface

Public Class Class1
    Public Interface Controller
        Sub SomeMethod()
    End Interface
End Class

Public Interface IAmAnInterface
    Sub SomeMethod()
End Interface
",
                GetCA1715InterfaceBasicResultAt(2, 18, "Controller"),
                GetCA1715InterfaceBasicResultAt(6, 18, "\u65E5\u672C\u8A9E"),
                GetCA1715InterfaceBasicResultAt(10, 18, "_Controller"),
                GetCA1715InterfaceBasicResultAt(14, 18, "_\u65E5\u672C\u8A9E"),
                GetCA1715InterfaceBasicResultAt(18, 18, "Internet"),
                GetCA1715InterfaceBasicResultAt(22, 18, "Iinternet"),
                GetCA1715InterfaceBasicResultAt(27, 22, "Controller"));
        }

        [Fact]
        public void TestTypeParameterNamesBasic()
        {
            VerifyBasic(@"
Imports System

Public Class IInterface(Of VSome)
End Class

Public Class IAnotherInterface(Of 本語)
End Class

Public Delegate Sub Callback(Of VSome)()

Public Class Class2(Of VSome)
End Class

Public Class Class2(Of T, VSome)
End Class

Public Class Class3(Of Type)
End Class

Public Class Class3(Of T, Type)
End Class

Public Class Base(Of Key, Value)
End Class

Public Class Derived(Of Key, Value)
    Inherits Base(Of Key, Value)
End Class

Public Class Class4(Of Type1)
    Public Sub AnotherMethod(Of Type2)()
        Console.WriteLine(GetType(Type2).ToString())
    End Sub

    Public Sub Method(Of Type2)(type As Type2)
        Console.WriteLine(type)
    End Sub

    Public Sub Method(Of KType, VType)(key As KType, value As VType)
        Console.WriteLine(key.ToString() + value.ToString())
    End Sub
End Class

Public Class Class5(Of _Type1)
    Public Sub Method(Of _K, _V)(key As _K, value As _V)
        Console.WriteLine(key.ToString() + value.ToString())
    End Sub
End Class

Public Class Class6(Of TTypeParameter)
End Class
",
                GetCA1715TypeParameterBasicResultAt(4, 28, "VSome"),
                GetCA1715TypeParameterBasicResultAt(7, 35, "\u672C\u8A9E"),
                GetCA1715TypeParameterBasicResultAt(10, 33, "VSome"),
                GetCA1715TypeParameterBasicResultAt(12, 24, "VSome"),
                GetCA1715TypeParameterBasicResultAt(15, 27, "VSome"),
                GetCA1715TypeParameterBasicResultAt(18, 24, "Type"),
                GetCA1715TypeParameterBasicResultAt(21, 27, "Type"),
                GetCA1715TypeParameterBasicResultAt(24, 22, "Key"),
                GetCA1715TypeParameterBasicResultAt(24, 27, "Value"),
                GetCA1715TypeParameterBasicResultAt(27, 25, "Key"),
                GetCA1715TypeParameterBasicResultAt(27, 30, "Value"),
                GetCA1715TypeParameterBasicResultAt(31, 24, "Type1"),
                GetCA1715TypeParameterBasicResultAt(32, 33, "Type2"),
                GetCA1715TypeParameterBasicResultAt(36, 26, "Type2"),
                GetCA1715TypeParameterBasicResultAt(40, 26, "KType"),
                GetCA1715TypeParameterBasicResultAt(40, 33, "VType"),
                GetCA1715TypeParameterBasicResultAt(45, 24, "_Type1"),
                GetCA1715TypeParameterBasicResultAt(46, 26, "_K"),
                GetCA1715TypeParameterBasicResultAt(46, 30, "_V"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestInterfaceNamesInternalBasic_NoDiagnostic()
        {
            VerifyBasic(@"
Friend Interface Controller
    Sub SomeMethod()
End Interface

Friend Class C
    Public Interface 日本語
        Sub SomeMethod()
    End Interface
End Class

Public Class C2
    Private Interface _Controller
        Sub SomeMethod()
    End Interface
End Class
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestTypeParameterNamesInternalBasic_NoDiagnostic()
        {
            VerifyBasic(@"
Imports System

Friend Class IInterface(Of VSome)
End Class

Friend Class C
    Public Class IAnotherInterface(Of 本語)
    End Class
End Class

Friend Class C2
    Private Delegate Sub Callback(Of VSome)()
End Class
");
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Fact]
        public void TestTypeParameterNamesBasic_SingleLetterCases_Default()
        {
            VerifyBasic(@"
Imports System

Public Class IInterface(Of V)
End Class

Public Delegate Sub Callback(Of V)()

Public Class Class2(Of T, V)
End Class

Public Class Class4(Of T)
    Public Sub Method(Of K, V)(key As K, value As V)
        Console.WriteLine(key.ToString() + value.ToString())
    End Sub
End Class

Public Class Class6(Of TTypeParameter)
End Class
",
            GetCA1715TypeParameterBasicResultAt(4, 28, "V"),
            GetCA1715TypeParameterBasicResultAt(7, 33, "V"),
            GetCA1715TypeParameterBasicResultAt(9, 27, "V"),
            GetCA1715TypeParameterBasicResultAt(13, 26, "K"),
            GetCA1715TypeParameterBasicResultAt(13, 29, "V"));
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Theory]
        [InlineData(@"")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = false")]
        [InlineData(@"dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = false")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = true
                      dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = false")]
        public void TestTypeParameterNamesBasic_SingleLetterCases_EditorConfig_Diagnostic(string editorConfigText)
        {
            VerifyBasic(@"
Imports System

Public Class IInterface(Of V)
End Class

Public Delegate Sub Callback(Of V)()

Public Class Class2(Of T, V)
End Class

Public Class Class4(Of T)
    Public Sub Method(Of K, V)(key As K, value As V)
        Console.WriteLine(key.ToString() + value.ToString())
    End Sub
End Class

Public Class Class6(Of TTypeParameter)
End Class
",
            GetEditorConfigAdditionalFile(editorConfigText),
            GetCA1715TypeParameterBasicResultAt(4, 28, "V"),
            GetCA1715TypeParameterBasicResultAt(7, 33, "V"),
            GetCA1715TypeParameterBasicResultAt(9, 27, "V"),
            GetCA1715TypeParameterBasicResultAt(13, 26, "K"),
            GetCA1715TypeParameterBasicResultAt(13, 29, "V"));
        }

        [WorkItem(1604, "https://github.com/dotnet/roslyn-analyzers/issues/1604")]
        [Theory]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = true")]
        [InlineData(@"dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true")]
        [InlineData(@"dotnet_code_quality.exclude_single_letter_type_parameters = false
                      dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true")]
        public void TestTypeParameterNamesBasic_SingleLetterCases_EditorConfig_NoDiagnostic(string editorConfigText)
        {
            VerifyBasic(@"
Imports System

Public Class IInterface(Of V)
End Class

Public Delegate Sub Callback(Of V)()

Public Class Class2(Of T, V)
End Class

Public Class Class4(Of T)
    Public Sub Method(Of K, V)(key As K, value As V)
        Console.WriteLine(key.ToString() + value.ToString())
    End Sub
End Class

Public Class Class6(Of TTypeParameter)
End Class
", GetEditorConfigAdditionalFile(editorConfigText));
        }

        internal static readonly string CA1715InterfaceMessage = MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixMessageInterface;
        internal static readonly string CA1715TypeParameterMessage = MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixMessageTypeParameter;

        private static DiagnosticResult GetCA1715InterfaceCSharpResultAt(int line, int column, string name)
        {
            return GetCSharpResultAt(line, column, IdentifiersShouldHaveCorrectPrefixAnalyzer.InterfaceRule, name);
        }

        private static DiagnosticResult GetCA1715InterfaceBasicResultAt(int line, int column, string name)
        {
            return GetBasicResultAt(line, column, IdentifiersShouldHaveCorrectPrefixAnalyzer.InterfaceRule, name);
        }

        private static DiagnosticResult GetCA1715TypeParameterCSharpResultAt(int line, int column, string name)
        {
            return GetCSharpResultAt(line, column, IdentifiersShouldHaveCorrectPrefixAnalyzer.TypeParameterRule, name);
        }

        private static DiagnosticResult GetCA1715TypeParameterBasicResultAt(int line, int column, string name)
        {
            return GetBasicResultAt(line, column, IdentifiersShouldHaveCorrectPrefixAnalyzer.TypeParameterRule, name);
        }
    }
}