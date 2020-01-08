// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithAttributesDiagnosticAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpMarkAssembliesWithClsCompliantFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithAttributesDiagnosticAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicMarkAssembliesWithClsCompliantFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class MarkAssembliesWithCLSCompliantAttributeTests : DiagnosticAnalyzerTestBase
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
        public void CA1014CA1016BasicTestWithCLSCompliantAttributeNone()
        {
            VerifyBasic(
@"
imports System.Reflection

    class Program
    
        Sub Main
        End Sub
    End class
",
            s_diagnosticCA1016, s_diagnosticCA1014);
        }

        [Fact]
        public void CA1014BasicTestWithNoVersionAttribute()
        {
            VerifyBasic(
@"
imports System.Reflection

< Assembly: AssemblyVersionAttribute(""1.1.3.4"")>
    class Program
    
        Sub Main
        End Sub
    End class
",
                s_diagnosticCA1014);
        }

        [Fact]
        public void CA1014CSharpTestWithComplianceAttributeNotFromBCL()
        {
            VerifyCSharp(
@"
using System;
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]

[assembly:CLSCompliant(true)]
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
class CLSCompliantAttribute : Attribute {
    public CLSCompliantAttribute(bool s) {}
}
",
                s_diagnosticCA1014);
        }

        [Fact]
        public void CA1014CSharpTestWithNoCLSComplianceAttribute()
        {
            VerifyCSharp(
@"
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]

class Program
{
    static void Main(string[] args)
    {
    }
}
",
                s_diagnosticCA1014);
        }

        [Fact]
        public void CA1014CSharpTestWithCLSCompliantAttribute()
        {
            VerifyCSharp(
@"
using System;
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]

[assembly:CLSCompliantAttribute(true)]
class Program
{
    static void Main(string[] args)
    {
    }
}
");
        }

        [Fact]
        public void CA1014CSharpTestWithTwoFilesWithAttribute()
        {
            VerifyCSharp(new[]
                {
@"
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]

class Program
{
    static void Main(string[] args)
    {
    }
}
",
@"
using System;
[assembly:CLSCompliantAttribute(true)]
"
                });
        }

        [Fact]
        public void CA1014CSharpTestWithCLSCompliantAttributeTruncated()
        {
            VerifyCSharp(
@"
using System;
using System.Reflection;
[assembly:AssemblyVersionAttribute(""1.2.3.4"")]

[assembly:CLSCompliant(true)]
class Program
{
    static void Main(string[] args)
    {
    }
}
");
        }

        [Fact]
        public void CA1014CSharpTestWithCLSCompliantAttributeFullyQualified()
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
        public void CA1014CSharpTestWithCLSCompliantAttributeNone()
        {
            VerifyCSharp(
@"
using System.Reflection;
class Program
{
    static void Main(string[] args)
    {
    }
}
",
            s_diagnosticCA1016, s_diagnosticCA1014);
        }

        [Fact, WorkItem(2143, "https://github.com/dotnet/roslyn-analyzers/issues/2143")]
        public void CA1014CSharpTestWithRazorCompiledItemAttribute()
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

        private static readonly DiagnosticResult s_diagnosticCA1014 = new DiagnosticResult(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1014Rule);

        private static readonly DiagnosticResult s_diagnosticCA1016 = new DiagnosticResult(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule);
    }
}
