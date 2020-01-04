// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UseIntegralOrStringArgumentForIndexersTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new UseIntegralOrStringArgumentForIndexersAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UseIntegralOrStringArgumentForIndexersAnalyzer();
        }

        [Fact]
        public void TestBasicUseIntegralOrStringArgumentForIndexersWarning1()
        {
            VerifyBasic(@"
    Imports System

    Public Class Months
        Private month() As String = {""Jan"", ""Feb"", ""...""}
        Default ReadOnly Property Item(index As Single) As String
            Get
                Return month(index)
            End Get
        End Property
    End Class
", CreateBasicResult(6, 35));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestBasicUseIntegralOrStringArgumentForIndexersNoWarning_Internal()
        {
            VerifyBasic(@"
    Imports System

    Friend Class Months
        Private month() As String = {""Jan"", ""Feb"", ""...""}
        Public Default ReadOnly Property Item(index As Single) As String
            Get
                Return month(index)
            End Get
        End Property
    End Class

    Public Class Months2
        Private month() As String = {""Jan"", ""Feb"", ""...""}
        Friend Default ReadOnly Property Item(index As Single) As String
            Get
                Return month(index)
            End Get
        End Property
    End Class
");
        }

        [Fact]
        public void TestBasicUseIntegralOrStringArgumentForIndexersNoWarning1()
        {
            VerifyBasic(@"
    Public Class Months
        Private month() As String = {""Jan"", ""Feb"", ""...""}
        Default ReadOnly Property Item(index As String) As String
            Get
                Return month(index)
            End Get
        End Property
    End Class
");
        }

        [Fact]
        public void TestCSharpUseIntegralOrStringArgumentForIndexersWarning1()
        {
            VerifyCSharp(@"
    public class Months
    {
        string[] month = new string[] {""Jan"", ""Feb"", ""...""};
        public string this[char index]
        {
            get
            {
                return month[index];
            }
        }
    }", CreateCSharpResult(5, 23));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestCSharpUseIntegralOrStringArgumentForIndexersNoWarning_Internal()
        {
            VerifyCSharp(@"
    internal class Months
    {
        string[] month = new string[] {""Jan"", ""Feb"", ""...""};
        public string this[char index]
        {
            get
            {
                return month[index];
            }
        }
    }

    public class Months2
    {
        string[] month = new string[] {""Jan"", ""Feb"", ""...""};
        internal string this[char index]
        {
            get
            {
                return month[index];
            }
        }
    }");
        }

        [Fact]
        public void TestCSharpUseIntegralOrStringArgumentForIndexersNoWarning1()
        {
            VerifyCSharp(@"
    public class Months
    {
        string[] month = new string[] {""Jan"", ""Feb"", ""...""};
        public string this[int index]
        {
            get
            {
                return month[index];
            }
        }
    }");
        }

        [Fact]
        public void TestCSharpGenericIndexer()
        {
            VerifyCSharp(@"
    public class Months<T>
    {
        public string this[T index]
        {
            get
            {
                return null;
            }
        }
    }");
        }

        [Fact]
        public void TestBasicGenericIndexer()
        {
            VerifyBasic(@"
    Public Class Months(Of T)
        Default Public ReadOnly Property Item(index As T)
            Get
                Return Nothing
            End Get
        End Property
    End Class");
        }

        [Fact]
        public void TestCSharpEnumIndexer()
        {
            VerifyCSharp(@"
    public class Months<T>
    {
        public enum Foo { }

        public string this[Foo index]
        {
            get
            {
                return null;
            }
        }
    }");
        }

        [Fact]
        public void TestBasicEnumIndexer()
        {
            VerifyBasic(@"
    Public Class Months(Of T)
        Public Enum Foo
            Val1
        End Enum

        Default Public ReadOnly Property Item(index As Foo)
            Get
                Return Nothing
            End Get
        End Property
    End Class");
        }

        private static DiagnosticResult CreateCSharpResult(int line, int col)
        {
            return GetCSharpResultAt(line, col, UseIntegralOrStringArgumentForIndexersAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.UseIntegralOrStringArgumentForIndexersMessage);
        }

        private static DiagnosticResult CreateBasicResult(int line, int col)
        {
            return GetBasicResultAt(line, col, UseIntegralOrStringArgumentForIndexersAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.UseIntegralOrStringArgumentForIndexersMessage);
        }
    }
}