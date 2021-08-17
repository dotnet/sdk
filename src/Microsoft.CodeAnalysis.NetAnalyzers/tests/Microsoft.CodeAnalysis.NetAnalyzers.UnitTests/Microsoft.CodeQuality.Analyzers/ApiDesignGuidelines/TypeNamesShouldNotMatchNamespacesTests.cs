// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypeNamesShouldNotMatchNamespacesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpTypeNamesShouldNotMatchNamespacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypeNamesShouldNotMatchNamespacesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicTypeNamesShouldNotMatchNamespacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class TypeNamesShouldNotMatchNamespacesTests
    {
        private static DiagnosticResult CSharpDefaultResultAt(int line, int column, string typeName, string namespaceName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(TypeNamesShouldNotMatchNamespacesAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, namespaceName);

        private static DiagnosticResult CSharpSystemResultAt(int line, int column, string typeName, string namespaceName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(TypeNamesShouldNotMatchNamespacesAnalyzer.SystemRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, namespaceName);

        private static DiagnosticResult BasicDefaultResultAt(int line, int column, string typeName, string namespaceName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(TypeNamesShouldNotMatchNamespacesAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, namespaceName);

        private static DiagnosticResult BasicSystemResultAt(int line, int column, string typeName, string namespaceName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(TypeNamesShouldNotMatchNamespacesAnalyzer.SystemRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, namespaceName);

        [Fact]
        public async Task CA1724CSharpValidNameAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
}");
        }

        [Fact]
        public async Task CA1724CSharpInvalidNameMatchingFormsNamespaceInSystemRuleAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Forms
{
}",
        CSharpSystemResultAt(2, 14, "Forms", "System.Windows.Forms"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1724CSharpInvalidNameMatchingFormsNamespaceInSystemRule_Internal_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class Forms
{
}

public class Outer
{
    private class Forms
    {
    }
}

internal class Outer2
{
    public class Forms
    {
    }
}
");
        }

        [Fact]
        public async Task CA1724CSharpInvalidNameMatchingSdkNamespaceInDefaultRuleAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    { @"
public class Sdk
{
}
",
                    },
                    AdditionalReferences =  { MetadataReference.CreateFromFile(typeof(Xunit.Sdk.AllException).Assembly.Location) }
                },
                ExpectedDiagnostics =
                {
                    CSharpDefaultResultAt(2, 14, "Sdk", "Xunit.Sdk"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_NoDiagnostic_NamespaceWithNoTypesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A.B
{
}

namespace D
{
    public class A {}
}");
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_NoDiagnostic_NamespaceWithNoExternallyVisibleTypesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A
{
    internal class C { }
}

namespace D
{
    public class A {}
}");
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_NoDiagnostic_NamespaceWithNoExternallyVisibleTypes_02Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A
{
    namespace B
    {
        internal class C { }
    }
}

namespace D
{
    public class A {}
}");
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_NoDiagnostic_ClashingTypeIsNotExternallyVisibleAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A
{
    namespace B
    {
        public class C { }
    }
}

namespace D
{
    internal class A {}
}");
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_Diagnostic_NamespaceWithExternallyVisibleTypeMemberAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A
{
    public class C { }
}

namespace D
{
    public class A {}
}",
            // Test0.cs(9,18): warning CA1724: The type name A conflicts in whole or in part with the namespace name 'A'. Change either name to eliminate the conflict.
            CSharpDefaultResultAt(9, 18, "A", "A"));
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_Diagnostic_NamespaceWithExternallyVisibleTypeMember_02Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace B
{
    namespace A
    {
        public class C { }
    }
}

namespace D
{
    public class A {}
}",
            // Test0.cs(12,18): warning CA1724: The type name A conflicts in whole or in part with the namespace name 'B.A'. Change either name to eliminate the conflict.
            CSharpDefaultResultAt(12, 18, "A", "B.A"));
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_Diagnostic_NamespaceWithExternallyVisibleTypeMember_InChildNamespaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A
{
    namespace B
    {
        public class C { }
    }
}

namespace D
{
    public class A {}
}",
            // Test0.cs(12,18): warning CA1724: The type name A conflicts in whole or in part with the namespace name 'A'. Change either name to eliminate the conflict.
            CSharpDefaultResultAt(12, 18, "A", "A"));
        }

        [Fact, WorkItem(1673, "https://github.com/dotnet/roslyn-analyzers/issues/1673")]
        public async Task CA1724CSharp_Diagnostic_NamespaceWithExternallyVisibleTypeMember_InChildNamespace_02Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace A.B
{
    public class C { }
}

namespace D
{
    public class A {}
}",
            // Test0.cs(9,18): warning CA1724: The type name A conflicts in whole or in part with the namespace name 'A'. Change either name to eliminate the conflict.
            CSharpDefaultResultAt(9, 18, "A", "A"));
        }

        [Fact]
        public async Task CA1724VisualBasicValidNameAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
End Class");
        }

        [Fact]
        public async Task CA1724VisualBasicInvalidNameMatchingFormsNamespaceInSystemRuleAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Forms
End Class",
        BasicSystemResultAt(2, 14, "Forms", "System.Windows.Forms"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1724VisualBasicInvalidNameMatchingFormsNamespaceInSystemRule_Internal_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class Forms
End Class

Public Class Outer
    Private Class Forms
    End Class
End Class

Friend Class Outer2
    Public Class Forms
    End Class
End Class
");
        }

        [Fact]
        public async Task CA1724VisualBasicInvalidNameMatchingSdkNamespaceInDefaultRuleAsync()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class Sdk
End Class"
                    },
                    AdditionalReferences = { MetadataReference.CreateFromFile(typeof(Xunit.Sdk.AllException).Assembly.Location) }
                },
                ExpectedDiagnostics =
                {
                    BasicDefaultResultAt(2, 14, "Sdk", "Xunit.Sdk"),
                }
            }.RunAsync();
        }
    }
}
