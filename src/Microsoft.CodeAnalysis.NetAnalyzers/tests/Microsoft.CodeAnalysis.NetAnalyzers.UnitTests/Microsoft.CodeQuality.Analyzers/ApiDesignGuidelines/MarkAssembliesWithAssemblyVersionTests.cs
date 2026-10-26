// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
    public class MarkAssembliesWithAssemblyVersionAttributeTests
    {
        [Fact]
        public async Task CA1016BasicTestWithNoComplianceAttributeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(
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
                VerifyVB.Diagnostic(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule));
        }

        [Fact]
        public async Task CA1016CSharpTestWithVersionAttributeNotFromBCLAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
                VerifyCS.Diagnostic(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule));
        }

        [Fact]
        public async Task CA1016CSharpTestWithNoVersionAttributeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
[assembly:System.CLSCompliantAttribute(true)]

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
",
                VerifyCS.Diagnostic(MarkAssembliesWithAttributesDiagnosticAnalyzer.CA1016Rule));
        }

        [Fact]
        public async Task CA1016CSharpTestWithVersionAttributeAsync()
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
        public async Task CA1016CSharpTestWithTwoFilesWithAttributeAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
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
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1016CSharpTestWithVersionAttributeTruncatedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1016CSharpTestWithVersionAttributeFullyQualifiedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(
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
        public async Task CA1016CSharpTestWithRazorCompiledItemAttributeAsync()
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
    }
}
