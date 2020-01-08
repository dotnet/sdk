// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriParametersShouldNotBeStringsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new UriParametersShouldNotBeStringsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UriParametersShouldNotBeStringsAnalyzer();
        }

        [Fact]
        public void CA1054NoWarningWithUrlNotStringType()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public static void Method(int url) { }
    }
");
        }

        [Fact]
        public void CA1054WarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public static void Method(string url) { }
    }
", GetCA1054CSharpResultAt(6, 42, "url", "A.Method(string)"));
        }

        [Fact]
        public void CA1054NoWarningWithUrlWithOverload()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public static void Method(string url) { }
        public static void Method(Uri url) { }
    }
");
        }

        [Fact, WorkItem(1495, "https://github.com/dotnet/roslyn-analyzers/issues/1495")]
        public void CA1054NoWarningWithUrlWithOverload_IdenticalTypeParameters()
        {
            VerifyCSharp(@"
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
        public void CA1054WarningWithUrlWithOverload_DifferingTypeParameters()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(7, 51, UriParametersShouldNotBeStringsAnalyzer.Rule, "url", "A.Method1<T1, T2>(string, T1)"),
            // Test0.cs(11,46): warning CA1054: Change the type of parameter url of method A.Method2<T>(string, B<T>) from string to System.Uri, or provide an overload to A.Method2<T>(string, B<T>) that allows url to be passed as a System.Uri object.
            GetCSharpResultAt(11, 46, UriParametersShouldNotBeStringsAnalyzer.Rule, "url", "A.Method2<T>(string, B<T>)"));
        }

        [Fact]
        public void CA1054MultipleWarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public static void Method(string url, string url2) { }
    }
", GetCA1054CSharpResultAt(6, 42, "url", "A.Method(string, string)")
 , GetCA1054CSharpResultAt(6, 54, "url2", "A.Method(string, string)"));
        }

        [Fact]
        public void CA1054NoMultipleWarningWithUrlWithOverload()
        {
            VerifyCSharp(@"
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
        public void CA1054MultipleWarningWithUrlWithOverload()
        {
            // Following original FxCop implementation. but this seems strange.
            VerifyCSharp(@"
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
        public void CA1054NoWarningNotPublic()
        {
            VerifyCSharp(@"
    using System;

    internal class A
    {
        public static void Method(string url) { }
    }
");
        }

        [Fact]
        public void CA1054NoWarningDerivedFromAttribute()
        {
            VerifyCSharp(@"
    using System;

    internal class A : Attribute
    {
        public void Method(string url) { }
    }
");
        }

        [Fact]
        public void CA1054WarningVB()
        {
            // C# and VB shares same implementation. so just one vb test
            VerifyBasic(@"
    Imports System
    
    Public Module A
        Public Sub Method(firstUri As String)
        End Sub
    End Module
", GetCA1054BasicResultAt(5, 27, "firstUri", "A.Method(String)"));
        }

        private static DiagnosticResult GetCA1054CSharpResultAt(int line, int column, params string[] args)
        {
            return GetCSharpResultAt(line, column, UriParametersShouldNotBeStringsAnalyzer.Rule, args);
        }

        private static DiagnosticResult GetCA1054BasicResultAt(int line, int column, params string[] args)
        {
            return GetBasicResultAt(line, column, UriParametersShouldNotBeStringsAnalyzer.Rule, args);
        }
    }
}