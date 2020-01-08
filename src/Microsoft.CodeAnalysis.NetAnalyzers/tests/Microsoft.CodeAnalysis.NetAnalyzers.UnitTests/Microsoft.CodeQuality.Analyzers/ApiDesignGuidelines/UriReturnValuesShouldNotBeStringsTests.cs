// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriReturnValuesShouldNotBeStringsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new UriReturnValuesShouldNotBeStringsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UriReturnValuesShouldNotBeStringsAnalyzer();
        }

        [Fact]
        public void CA1055NoWarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public Uri GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1055NoWarningWithUrlNotStringType()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public int GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1055WarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public string GetUrl() { throw new NotImplementedException(); }
    }
", GetCA1055CSharpResultAt(6, 23, "A.GetUrl()"));
        }

        [Fact]
        public void CA1055NoWarningWithNoUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public string GetMethod() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1055NoWarningNotPublic()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        private string GetUrl() { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1055NoWarningWithUrlParameter()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public string GetUrl(Uri u) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1055NoWarningOverride()
        {
            // warning is from base type not overriden one
            VerifyCSharp(@"
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
        public void CA1055WarningVB()
        {
            // C# and VB shares same implementation. so just one vb test
            VerifyBasic(@"
    Imports System
    
    Public Module A
        Function GetUrl() As String
        End Function
    End Module
", GetCA1055BasicResultAt(5, 18, "A.GetUrl()"));
        }

        private static DiagnosticResult GetCA1055CSharpResultAt(int line, int column, params string[] args)
        {
            return GetCSharpResultAt(line, column, UriReturnValuesShouldNotBeStringsAnalyzer.Rule, args);
        }

        private static DiagnosticResult GetCA1055BasicResultAt(int line, int column, params string[] args)
        {
            return GetBasicResultAt(line, column, UriReturnValuesShouldNotBeStringsAnalyzer.Rule, args);
        }
    }
}