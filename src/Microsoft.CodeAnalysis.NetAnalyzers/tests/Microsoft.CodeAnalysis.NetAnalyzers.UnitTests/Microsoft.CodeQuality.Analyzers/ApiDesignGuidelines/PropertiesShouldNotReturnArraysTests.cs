// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotReturnArraysAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotReturnArraysAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class PropertiesShouldNotReturnArraysTests
    {
        [Fact]
        public async Task TestCSharpPropertiesShouldNotReturnArraysWarning1()
        {
            //Verify return type is array, warning...
            await VerifyCS.VerifyAnalyzerAsync(@"
    public class Book
    {
        private string[] _Pages;
        public string[] Pages
        {
            get { return _Pages; }
        }
    }
 ", CreateCSharpResult(5, 25));
        }

        [Fact]
        public async Task TestCSharpPropertiesShouldNotReturnArraysNoWarning1()
        {
            //Verify if property is override, then no warning...
            await VerifyCS.VerifyAnalyzerAsync(@"
    public abstract class Base
    {
        public virtual string[] Pages { get; }
    }

    public class Book : Base
    {
        public override string[] Pages
        {
            get { return null; }
        }
    }
", CreateCSharpResult(4, 33));
        }

        [Fact]
        public async Task TestCSharpPropertiesShouldNotReturnArraysNoWarning2()
        {
            //No warning if property definition has no outside visibility
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Outer
{
    private class Book
    {
        public string[] Pages
        {
            get { return null; }
        }
    }
}
");
        }

        [Fact]
        public async Task TestCSharpPropertiesShouldNotReturnArraysNoWarning3()
        {
            //Attributes can contain properties that return arrays
            await VerifyCS.VerifyAnalyzerAsync(@"
    public class Book : System.Attribute
    {
        public string[] Pages 
        {
            get { return null; }
        }
    }
");
        }

        [Fact]
        public async Task TestBasicPropertiesShouldNotReturnArraysWarning1()
        {
            //Display warning for property return type is Array
            await VerifyVB.VerifyAnalyzerAsync(@"
    Public Class Book
        Private _Pages As String()
        Public ReadOnly Property Pages() As String()
            Get
                Return _Pages
            End Get
        End Property
    End Class", CreateBasicResult(4, 34));
        }

        [Fact]
        public async Task TestBasicPropertiesShouldNotReturnArraysNoWarning1()
        {
            //No warning if property definition is override
            await VerifyVB.VerifyAnalyzerAsync(@"
    Public MustInherit Class Base
        Public Overridable ReadOnly Property Pages() As String()
    End Class

    Public Class Book
        Inherits Base

        Private _Pages As String()

        Public Overrides ReadOnly Property Pages() As String()
            Get
                Return _Pages
            End Get
        End Property
    End Class"
, CreateBasicResult(3, 46));
        }

        [Fact]
        public async Task TestBasicPropertiesShouldNotReturnArraysWarning2()
        {
            //No warning if property has no outside visibility
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Outer
    Private Class Book
        Private _Pages As String()
        Public ReadOnly Property Pages() As String()
            Get
                Return _Pages
            End Get
        End Property
    End Class
End Class");
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