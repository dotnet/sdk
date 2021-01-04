// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UseIntegralOrStringArgumentForIndexersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UseIntegralOrStringArgumentForIndexersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UseIntegralOrStringArgumentForIndexersTests
    {
        [Fact]
        public async Task TestBasicUseIntegralOrStringArgumentForIndexersWarning1()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task TestBasicUseIntegralOrStringArgumentForIndexersNoWarning_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task TestBasicUseIntegralOrStringArgumentForIndexersNoWarning1()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task TestCSharpUseIntegralOrStringArgumentForIndexersWarning1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task TestCSharpUseIntegralOrStringArgumentForIndexersNoWarning_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task TestCSharpUseIntegralOrStringArgumentForIndexersNoWarning1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task TestCSharpGenericIndexer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task TestBasicGenericIndexer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
    Public Class Months(Of T)
        Default Public ReadOnly Property Item(index As T)
            Get
                Return Nothing
            End Get
        End Property
    End Class");
        }

        [Fact]
        public async Task TestCSharpEnumIndexer()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    public class Months<T>
    {
        public enum SomeEnum { }

        public string this[SomeEnum index]
        {
            get
            {
                return null;
            }
        }
    }");
        }

        [Fact]
        public async Task TestBasicEnumIndexer()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
    Public Class Months(Of T)
        Public Enum SomeEnum
            Val1
        End Enum

        Default Public ReadOnly Property Item(index As SomeEnum)
            Get
                Return Nothing
            End Get
        End Property
    End Class");
        }

        [Fact, WorkItem(3638, "https://github.com/dotnet/roslyn-analyzers/issues/3638")]
        public async Task CA1043_IndexerOfTypeSystemIndex_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp30,
                TestCode = @"
public class C
{
    public string this[System.Index index]
    {
        get => null;
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp30,
                TestCode = @"
Public Class Months
    Default ReadOnly Property Item(index As System.Index) As String
        Get
            Return Nothing
        End Get
    End Property
End Class",
            }.RunAsync();
        }

        [Fact, WorkItem(3638, "https://github.com/dotnet/roslyn-analyzers/issues/3638")]
        public async Task CA1043_IndexerOfTypeSystemRange_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp30,
                TestCode = @"
public class C
{
    public string this[System.Range range]
    {
        get => null;
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp30,
                TestCode = @"
Public Class Months
    Default ReadOnly Property Item(index As System.Range) As String
        Get
            Return Nothing
        End Get
    End Property
End Class",
            }.RunAsync();
        }

        private static DiagnosticResult CreateCSharpResult(int line, int col)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult CreateBasicResult(int line, int col)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}