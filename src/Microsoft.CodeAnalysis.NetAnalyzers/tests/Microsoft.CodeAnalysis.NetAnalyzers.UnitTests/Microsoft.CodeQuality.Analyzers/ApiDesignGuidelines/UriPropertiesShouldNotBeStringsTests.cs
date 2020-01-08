// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriPropertiesShouldNotBeStringsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new UriPropertiesShouldNotBeStringsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UriPropertiesShouldNotBeStringsAnalyzer();
        }

        [Fact]
        public void CA1056NoWarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public Uri SampleUri { get; set; }
    }
");
        }

        [Fact]
        public void CA1056NoWarningWithUrlNotStringType()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public int SampleUri { get; set; }
    }
");
        }

        [Fact]
        public void CA1056WarningWithUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public string SampleUri { get; set; }
    }
", GetCA1056CSharpResultAt(6, 23, "A.SampleUri"));
        }

        [Fact]
        public void CA1056NoWarningWithNoUrl()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        public string Sample { get; set; }
    }
");
        }

        [Fact]
        public void CA1056NoWarningNotPublic()
        {
            VerifyCSharp(@"
    using System;

    public class A
    {
        private string SampleUrl { get; set; }

        public int CompareTo(object obj) { throw new NotImplementedException(); }
    }
");
        }

        [Fact]
        public void CA1056NoWarningDerivedFromAttribute()
        {
            VerifyCSharp(@"
    using System;

    public class A : Attribute
    {
        private string SampleUrl { get; set; }
    }
");
        }

        [Fact]
        public void CA1056NoWarningOverride()
        {
            // warning is from base type not overriden one
            VerifyCSharp(@"
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
        public void CA1056WarningVB()
        {
            // C# and VB shares same implementation. so just one vb test
            VerifyBasic(@"
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

        private static DiagnosticResult GetCA1056CSharpResultAt(int line, int column, params string[] args)
        {
            return GetCSharpResultAt(line, column, UriPropertiesShouldNotBeStringsAnalyzer.Rule, args);
        }

        private static DiagnosticResult GetCA1056BasicResultAt(int line, int column, params string[] args)
        {
            return GetBasicResultAt(line, column, UriPropertiesShouldNotBeStringsAnalyzer.Rule, args);
        }
    }
}