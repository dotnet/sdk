// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithAttributesDiagnosticAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpMarkAssembliesWithAssemblyVersionFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithAttributesDiagnosticAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicMarkAssembliesWithAssemblyVersionFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class MarkAssembliesWithAssemblyVersionAttributeTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new MarkAssembliesWithAttributesDiagnosticAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MarkAssembliesWithAttributesDiagnosticAnalyzer();
        }

        [Fact]
        public void CA1016BasicTestWithNoComplianceAttribute()
        {
            VerifyBasic(
@"
imports System.IO
imports System.Reflection
imports System

< Assembly: CLSCompliant(true)>
    class Program
    
        Sub Main
        End Sub
    End class
",
                s_diagnostic);
        }

        [Fact]
        public void CA1016CSharpTestWithVersionAttributeNotFromBCL()
        {
            VerifyCSharp(
@"
using System;
[assembly:System.CLSCompliantAttribute(true)]
[assembly:AssemblyVersion(""1.2.3.4"")]
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
class AssemblyVersionAttribute : Attribute {
    public AssemblyVersionAttribute(string s) {}
}
",
                s_diagnostic);
        }

        [Fact]
        public void CA1016CSharpTestWithNoVersionAttribute()
        {
            VerifyCSharp(
@"
[assembly:System.CLSCompliantAttribute(true)]

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
",
                s_diagnostic);
        }

        [Fact]
        public void CA1016CSharpTestWithVersionAttribute()
        {
            VerifyCSharp(
@"
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]
[assembly:System.CLSCompliantAttribute(true)]

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
");
        }

        [Fact]
        public void CA1016CSharpTestWithTwoFilesWithAttribute()
        {
            VerifyCSharp(new[]
                {
@"
[assembly:System.CLSCompliantAttribute(true)]

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
",
@"
using System.Reflection;
[assembly: AssemblyVersionAttribute(""1.2.3.4"")]
"
                });
        }

        [Fact]
        public void CA1016CSharpTestWithVersionAttributeTruncated()
        {
            VerifyCSharp(
@"
using System.Reflection;
[assembly:AssemblyVersion(""1.2.3.4"")]
[assembly:System.CLSCompliantAttribute(true)]
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
");
        }

        [Fact]
        public void CA1016CSharpTestWithVersionAttributeFullyQualified()
        {
            VerifyCSharp(
@"
[assembly:System.CLSCompliantAttribute(true)]

[assembly:System.Reflection.AssemblyVersion(""1.2.3.4"")]
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
");
        }

        [Fact, WorkItem(2143, "https://github.com/dotnet/roslyn-analyzers/issues/2143")]
        public void CA1016CSharpTestWithRazorCompiledItemAttribute()
        {
            VerifyCSharp(
@"using System;

[assembly:Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute((Type)null, null, null)]

namespace Microsoft.AspNetCore.Razor.Hosting
{
    public class RazorCompiledItemAttribute : Attribute
    {
        public RazorCompiledItemAttribute(Type type, string kind, string identifier)
        {
        }
    }
}

public class C
{
}
");
        }

        private static readonly DiagnosticResult s_diagnostic = new DiagnosticResult(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule.Id, MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule.DefaultSeverity)
            .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithAssemblyVersionMessage);
    }
}
