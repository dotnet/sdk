// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriParametersShouldNotBeStringsTests
    {
        [Fact]
        public async Task CA1054NoWarningWithUrlNotStringTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(int url) { }
    }
");
        }

        [Fact]
        public async Task CA1054WarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(string url) { }
    }
", GetCA1054CSharpResultAt(6, 42, "url", "A.Method(string)"));
        }

        [Fact]
        public async Task CA1054NoWarningWithUrlWithOverloadAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(string url) { }
        public static void Method(Uri url) { }
    }
");
        }

        [Fact, WorkItem(1495, "https://github.com/dotnet/roslyn-analyzers/issues/1495")]
        public async Task CA1054NoWarningWithUrlWithOverload_IdenticalTypeParametersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        // Identical type parameters with no constraints
        public static void Method1<T>(string url, T param2) { }
        public static void Method1<V>(Uri url, V param2) { }

        // Identical type parameters with differing constraints, we consider them identical for our comparison purpose.
        public static void Method2<T>(string url, T param2) where T : class { }
        public static void Method2<V>(Uri url, V param2) where V : struct { }

        // Identical constructed types.
        public static void Method3<T>(string url, B<T> param2) { }
        public static void Method3<V>(Uri url, B<V> param2) { }
    }

    public class B<T> { }
");
        }

        [Fact, WorkItem(1495, "https://github.com/dotnet/roslyn-analyzers/issues/1495")]
        public async Task CA1054WarningWithUrlWithOverload_DifferingTypeParametersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        // Type parameters differing ordinal
        public static void Method1<T1, T2>(string url, T1 param2) { }
        public static void Method1<V1, V2>(Uri url, V2 param2) { }

        // Different constructed types.
        public static void Method2<T>(string url, B<T> param2) { }
        public static void Method2<V>(Uri url, C<V> param2) { }
    }

    public class B<T> { }
    public class C<T> { }
",
            // Test0.cs(7,51): warning CA1054: Change the type of parameter url of method A.Method1<T1, T2>(string, T1) from string to System.Uri, or provide an overload to A.Method1<T1, T2>(string, T1) that allows url to be passed as a System.Uri object.
            GetCA1054CSharpResultAt(7, 51, "url", "A.Method1<T1, T2>(string, T1)"),
            // Test0.cs(11,46): warning CA1054: Change the type of parameter url of method A.Method2<T>(string, B<T>) from string to System.Uri, or provide an overload to A.Method2<T>(string, B<T>) that allows url to be passed as a System.Uri object.
            GetCA1054CSharpResultAt(11, 46, "url", "A.Method2<T>(string, B<T>)"));
        }

        [Fact]
        public async Task CA1054MultipleWarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(string url, string url2) { }
    }
", GetCA1054CSharpResultAt(6, 42, "url", "A.Method(string, string)")
 , GetCA1054CSharpResultAt(6, 54, "url2", "A.Method(string, string)"));
        }

        [Fact]
        public async Task CA1054NoMultipleWarningWithUrlWithOverloadAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(string url, string url2) { }
        public static void Method(string url, Uri url2) { }
        public static void Method(Uri url, string url2) { }
        public static void Method(Uri url, Uri url2) { }
    }
");
        }

        [Fact]
        public async Task CA1054MultipleWarningWithUrlWithOverloadAsync()
        {
            // Following original FxCop implementation. but this seems strange.
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public static void Method(string url, string url2) { }
        public static void Method(Uri url, Uri url2) { }
    }
", GetCA1054CSharpResultAt(6, 42, "url", "A.Method(string, string)")
 , GetCA1054CSharpResultAt(6, 54, "url2", "A.Method(string, string)"));

        }

        [Fact, WorkItem(6371, "https://github.com/dotnet/roslyn-analyzers/issues/6371")]
        public async Task CA1054NoWarningsForInterfaceImplementationsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public interface IUrlInterface1
    {
        void Method(string url);
    }

    public interface IUrlInterface2
    {
        void Method(string url);
    }

    public class A : IUrlInterface1, IUrlInterface2
    {
        public void Method(string url) // Implements IUrlInterface1, implicitly
        {
        }

        void IUrlInterface2.Method(string url) // Implements IUrlInterface2, explicitly
        {
        }
    }
", GetCA1054CSharpResultAt(6, 28, "url", "IUrlInterface1.Method(string)")
 , GetCA1054CSharpResultAt(11, 28, "url", "IUrlInterface2.Method(string)"));
        }

        [Fact]
        public async Task CA1054NoWarningNotPublicAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal class A
    {
        public static void Method(string url) { }
    }
");
        }

        [Fact]
        public async Task CA1054NoWarningDerivedFromAttributeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal class A : Attribute
    {
        public void Method(string url) { }
    }
");
        }

        [Fact]
        public async Task CA1054WarningVBAsync()
        {
            // C# and VB shares same implementation. so just one vb test
            await VerifyVB.VerifyAnalyzerAsync(@"
    Imports System
    
    Public Module A
        Public Sub Method(firstUri As String)
        End Sub
    End Module
", GetCA1054BasicResultAt(5, 27, "firstUri", "A.Method(String)"));
        }

        [Theory, WorkItem(6005, "https://github.com/dotnet/roslyn-analyzers/issues/6005")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = Method")]
        [InlineData("dotnet_code_quality.CA1054.excluded_symbol_names = Method")]
        [InlineData("dotnet_code_quality.CA1054.excluded_symbol_names = Metho*")]
        public async Task CA1054_EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
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
    public static void Method(string url) { }
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
                csharpTest.ExpectedDiagnostics.Add(GetCA1054CSharpResultAt(6, 38, "url", "A.Method(string)"));
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
    Public Sub Method(url As String)
    End Sub
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
                basicTest.ExpectedDiagnostics.Add(GetCA1054BasicResultAt(5, 23, "url", "A.Method(String)"));
            }

            await basicTest.RunAsync();
        }

        private static DiagnosticResult GetCA1054CSharpResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCA1054BasicResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);
    }
}