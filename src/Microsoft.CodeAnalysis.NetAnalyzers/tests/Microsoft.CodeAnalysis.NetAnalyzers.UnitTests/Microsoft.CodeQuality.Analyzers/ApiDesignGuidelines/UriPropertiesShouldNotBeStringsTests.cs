// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriPropertiesShouldNotBeStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriPropertiesShouldNotBeStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriPropertiesShouldNotBeStringsTests
    {
        [Fact]
        public async Task CA1056NoWarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public Uri SampleUri { get; set; }
    }
");
        }

        [Fact]
        public async Task CA1056NoWarningWithUrlNotStringTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public int SampleUri { get; set; }
    }
");
        }

        [Fact]
        public async Task CA1056WarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public string SampleUri { get; set; }
    }
", GetCA1056CSharpResultAt(6, 23, "A.SampleUri"));
        }

        [Fact]
        public async Task CA1056NoWarningWithNoUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public string Sample { get; set; }
    }
");
        }

        [Fact]
        public async Task CA1056NoWarningNotPublicAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        private string SampleUrl { get; set; }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1056NoWarningDerivedFromAttributeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : Attribute
    {
        private string SampleUrl { get; set; }
    }
");
        }

        [Fact]
        public async Task CA1056NoWarningOverrideAsync()
        {
            // warning is from base type not overriden one
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class Base
    {
        protected virtual string SampleUrl { get; set; }
    }

    public class A : Base
    {
        protected override string SampleUrl { get; set; }
    }
", GetCA1056CSharpResultAt(6, 34, "Base.SampleUrl"));
        }

        [Fact]
        public async Task CA1056WarningVBAsync()
        {
            // C# and VB shares same implementation. so just one vb test
            await VerifyVB.VerifyAnalyzerAsync(@"
    Imports System
    
    Public Module A
        Public ReadOnly Property SampleUrl As String
                Get
                    Return Nothing
                End Get
            End Property
    End Module
", GetCA1056BasicResultAt(5, 34, "A.SampleUrl"));
        }

        [Fact, WorkItem(3146, "https://github.com/dotnet/roslyn-analyzers/issues/3146")]
        public async Task DoNotReportOnInterfaceImplementationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface IPath
{
    string UrlPathSegment { get; }
}

public class SomeClass : IPath
{
    public string UrlPathSegment { get; }
}",
                GetCA1056CSharpResultAt(4, 12, "IPath.UrlPathSegment"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface IPath
    Property UrlPathSegment As String
End Interface

Public Class SomeClass
    Implements IPath

    Public Property UrlPathSegment As String Implements IPath.UrlPathSegment
End Class",
                GetCA1056BasicResultAt(3, 14, "IPath.UrlPathSegment"));
        }

        [Theory, WorkItem(6005, "https://github.com/dotnet/roslyn-analyzers/issues/6005")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = SampleUri")]
        [InlineData("dotnet_code_quality.CA1056.excluded_symbol_names = SampleUri")]
        [InlineData("dotnet_code_quality.CA1056.excluded_symbol_names = Sample*")]
        public async Task CA1056_EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class A
{
    public string SampleUri { get; set; }
}
"                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetCA1056CSharpResultAt(6, 19, "A.SampleUri"));
            }

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System

Public Module A
    Public ReadOnly Property SampleUri As String
        Get
            Return Nothing
        End Get
    End Property
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                basicTest.ExpectedDiagnostics.Add(GetCA1056BasicResultAt(5, 30, "A.SampleUri"));
            }

            await basicTest.RunAsync();
        }

        private static DiagnosticResult GetCA1056CSharpResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCA1056BasicResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);
    }
}