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
        public async Task CA1054NoWarningWithUrlNotStringType()
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
        public async Task CA1054WarningWithUrl()
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
        public async Task CA1054NoWarningWithUrlWithOverload()
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
        public async Task CA1054NoWarningWithUrlWithOverload_IdenticalTypeParameters()
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
        public async Task CA1054WarningWithUrlWithOverload_DifferingTypeParameters()
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
        public async Task CA1054MultipleWarningWithUrl()
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
        public async Task CA1054NoMultipleWarningWithUrlWithOverload()
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
        public async Task CA1054MultipleWarningWithUrlWithOverload()
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

        [Fact]
        public async Task CA1054NoWarningNotPublic()
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
        public async Task CA1054NoWarningDerivedFromAttribute()
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
        public async Task CA1054WarningVB()
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

        private static DiagnosticResult GetCA1054CSharpResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCA1054BasicResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);
    }
}