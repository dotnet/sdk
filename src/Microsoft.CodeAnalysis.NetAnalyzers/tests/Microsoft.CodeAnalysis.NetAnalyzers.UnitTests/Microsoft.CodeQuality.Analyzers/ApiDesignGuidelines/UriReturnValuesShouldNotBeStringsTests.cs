// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriReturnValuesShouldNotBeStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriReturnValuesShouldNotBeStringsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriReturnValuesShouldNotBeStringsTests
    {
        [Fact]
        public async Task CA1055NoWarningWithUrl()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public Uri GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1055NoWarningWithUrlNotStringType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public int GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1055WarningWithUrl()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public string GetUrl() { throw new NotImplementedException(); }
    }
", GetCA1055CSharpResultAt(6, 23, "A.GetUrl()"));
        }

        [Fact]
        public async Task CA1055NoWarningWithNoUrl()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public string GetMethod() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1055NoWarningNotPublic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        private string GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1055NoWarningWithUrlParameter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A
    {
        public string GetUrl(Uri u) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public async Task CA1055NoWarningOverride()
        {
            // warning is from base type not overriden one
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class Base
    {
        protected virtual string GetUrl() { throw new NotImplementedException(); }
    }

    public class A : Base
    {
        protected override string GetUrl() { throw new NotImplementedException(); }
    }
", GetCA1055CSharpResultAt(6, 34, "Base.GetUrl()"));
        }

        [Fact]
        public async Task CA1055WarningVB()
        {
            // C# and VB shares same implementation. so just one vb test
            await VerifyVB.VerifyAnalyzerAsync(@"
    Imports System
    
    Public Module A
        Function GetUrl() As String
        End Function
    End Module
", GetCA1055BasicResultAt(5, 18, "A.GetUrl()"));
        }

        private static DiagnosticResult GetCA1055CSharpResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCA1055BasicResultAt(int line, int column, params string[] args)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(args);
    }
}