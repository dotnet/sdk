// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
    public class MarkAssembliesWithCLSCompliantAttributeTests
    {
        [Fact]
        public async Task CA1014CA1016BasicTestWithCLSCompliantAttributeNone()
        {
            await VerifyVB.VerifyAnalyzerAsync(
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
        public async Task CA1014BasicTestWithNoVersionAttribute()
        {
            await VerifyVB.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithComplianceAttributeNotFromBCL()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithNoCLSComplianceAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithCLSCompliantAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithTwoFilesWithAttribute()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
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
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1014CSharpTestWithCLSCompliantAttributeTruncated()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithCLSCompliantAttributeFullyQualified()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithCLSCompliantAttributeNone()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1014CSharpTestWithRazorCompiledItemAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
