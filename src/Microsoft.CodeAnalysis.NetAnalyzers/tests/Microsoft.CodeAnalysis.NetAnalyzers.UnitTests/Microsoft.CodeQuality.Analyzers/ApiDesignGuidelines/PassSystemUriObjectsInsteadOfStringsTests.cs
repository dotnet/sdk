// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpPassSystemUriObjectsInsteadOfStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicPassSystemUriObjectsInsteadOfStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class PassSystemUriObjectsInsteadOfStringsTests
    {
        [Fact]
        public async Task CA2234NoWarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(1);
        }

        public static void Method(int url) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithUriAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(1);
        }

        public static void Method(int uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithUrnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(1);
        }

        public static void Method(int urn) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithUriButNoStringAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(1);
        }

        public static void Method(int urn) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithStringButNoUriAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithStringButNoUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string url) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningWithStringButNoUrnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string urn) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234WarningWithUriAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string uri) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri)", "A.Method(string)"));
        }

        [Fact]
        public async Task CA2234WarningWithUrlAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string url) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri)", "A.Method(string)"));
        }

        [Fact]
        public async Task CA2234WarningWithUrnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string urn) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri)", "A.Method(string)"));
        }

        [Fact]
        public async Task CA2234WarningWithCompoundUriAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string myUri) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri)", "A.Method(string)"));
        }

        [Fact]
        public async Task CA2234NoWarningWithSubstringAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string myuri) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234WarningWithMultipleParameter1Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", ""test"", ""test"");
        }

        public static void Method(string param1, string param2, string lastUrl) { }
        public static void Method(string param1, string param2, Uri lastUrl) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(string, string, Uri)", "A.Method(string, string, string)"));
        }

        [Fact]
        public async Task CA2234WarningWithMultipleParameter2Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", 0, ""test"");
        }

        public static void Method(string firstUri, int i, string lastUrl) { }
        public static void Method(Uri uri, int i, string lastUrl) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri, int, string)", "A.Method(string, int, string)"));
        }

        [Fact]
        public async Task CA2234NoWarningForSelfAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", null);
        }

        public static void Method(string firstUri, Uri lastUri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningForSelf2Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", null);
        }

        public static void Method(string firstUri, Uri lastUri) { }
        public static void Method(int other, Uri lastUri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234WarningWithMultipleUriAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", null);
        }

        public static void Method(string firstUri, Uri lastUrl) { }
        public static void Method(Uri uri, Uri lastUrl) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
}
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri, Uri)", "A.Method(string, Uri)"));
        }

        [Fact]
        public async Task CA2234WarningWithMultipleOverloadAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", ""test2"");
        }

        public static void Method(string firstUri, string lastUrl) { }
        public static void Method(Uri uri, string lastUrl) { }
        public static void Method(string uri, Uri lastUrl) { }
        public static void Method(Uri uri, Uri lastUrl) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
", GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(Uri, string)", "A.Method(string, string)")
 , GetCA2234CSharpResultAt(8, 13, "A.Method()", "A.Method(string, Uri)", "A.Method(string, string)"));
        }

        [Fact]
        public async Task CA2234NoWarningSignatureMismatchingNumberOfParameterAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", null);
        }

        public static void Method(string firstUri, string lastUrl) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningSignatureMismatchingParameterTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method()
        {
            Method(""test"", null);
        }

        public static void Method(string firstUri, string lastUrl) { }
        public static void Method(Uri uri, int i) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234NoWarningNotPublicAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal class A : IComparable
    {
        public static void Method()
        {
            Method(""test"");
        }

        public static void Method(string uri) { }
        public static void Method(Uri uri) { }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA2234WarningVBAsync()
        {
            // since VB and C# shares almost all code except to get method overload group expression
            // we only need to test that part
            await VerifyVB.VerifyAnalyzerAsync(@"
    Imports System
    
    Public Module A
        Public Sub Method()
            Method(""test"", 0, ""test"")
        End Sub

        Public Sub Method(firstUri As String, i As Integer, lastUrl As String)
        End Sub

        Public Sub Method(Uri As Uri, i As Integer, lastUrl As String)
        End Sub
    End Module
", GetCA2234BasicResultAt(6, 13, "A.Method()", "A.Method(Uri, Integer, String)", "A.Method(String, Integer, String)"));
        }

        [Fact, WorkItem(2688, "https://github.com/dotnet/roslyn-analyzers/issues/2688")]
        public async Task CA2234NoWarningInvocationInUriOverloadAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable
    {
        public static void Method(string uri) { }
        public static void Method(Uri uri) { Method(uri.ToString()); }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        private static DiagnosticResult GetCA2234CSharpResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCA2234BasicResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);
    }
}