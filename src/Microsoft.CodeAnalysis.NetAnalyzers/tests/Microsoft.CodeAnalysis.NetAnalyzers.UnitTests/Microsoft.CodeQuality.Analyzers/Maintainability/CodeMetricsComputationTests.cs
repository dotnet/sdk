// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;
using Test.Utilities;
using Test.Utilities.CodeMetrics;
using Xunit;

namespace Microsoft.CodeAnalysis.CodeMetrics.UnitTests
{
    public class CodeMetricsComputationTests : CodeMetricsTestBase
    {
        protected override string GetMetricsDataString(Compilation compilation)
        {
            return CodeAnalysisMetricData.ComputeAsync(new CodeMetricsAnalysisContext(compilation, CancellationToken.None)).Result.ToString();
        }

        [Fact]
        public void EmptyCompilation()
        {
            var source = @"";

            var expectedMetricsText = @"
Assembly: (Lines: 0, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, DepthInherit: 0)";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void EmptyNamespace()
        {
            var source = @"
namespace N { }";

            var expectedMetricsText = @"
Assembly: (Lines: 0, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, DepthInherit: 0)";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void EmptyNamespaces()
        {
            var source = @"
namespace N1 { }

namespace N2
{
}";

            var expectedMetricsText = @"
Assembly: (Lines: 0, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, DepthInherit: 0)";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypeInNamespace()
        {
            var source = @"
namespace N1
{
    class C { }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   N1: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypeInGlobalNamespace()
        {
            var source = @"
class C
{
}";

            var expectedMetricsText = @"
Assembly: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypesInNamespaces()
        {
            var source = @"
namespace N1 { class C1 { } }

namespace N2
{
    class C2 { }
    class C3
    {
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 8, ExecutableLines: 0, MntIndex: 100, CycCxty: 3, DepthInherit: 1)
   N1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   N2: (Lines: 7, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
      C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C3: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypesInChildAndParentNamespaces()
        {
            var source = @"
namespace N1 { class C1 { } }

namespace N1.N2
{
    class C2 { }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 5, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
   N1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   N2: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypesInDifferentSourceFiles()
        {
            var source1 = @"
namespace N1 { class C1 { } }
";

            var source2 = @"
namespace N2
{
    class C2 { }
    class C3
    {
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 8, ExecutableLines: 0, MntIndex: 100, CycCxty: 3, DepthInherit: 1)
   N1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   N2: (Lines: 7, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
      C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C3: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(new[] { source1, source2 }, expectedMetricsText);
        }

        [Fact]
        public void PartialTypeDeclarationsInSameSourceFile()
        {
            var source = @"
partial class C1
{
    void M1(int x)
    {
        x = 0;
    }
}

partial class C1
{
    void M2(int x)
    {
        x = 0;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 14, ExecutableLines: 2, MntIndex: 95, CycCxty: 2, DepthInherit: 1)
    C1: (Lines: 14, ExecutableLines: 2, MntIndex: 95, CycCxty: 2, DepthInherit: 1)
        C1.M1(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
        C1.M2(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PartialTypeDeclarationsInDifferentSourceFiles()
        {
            var source1 = @"
partial class C1
{
    void M1(int x)
    {
        x = 0;
    }
}
";

            var source2 = @"
partial class C1
{
    void M2(int x)
    {
        x = 0;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 14, ExecutableLines: 2, MntIndex: 95, CycCxty: 2, DepthInherit: 1)
    C1: (Lines: 14, ExecutableLines: 2, MntIndex: 95, CycCxty: 2, DepthInherit: 1)
        C1.M1(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
        C1.M2(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
";

            VerifyCSharp(new[] { source1, source2 }, expectedMetricsText);
        }

        [Fact]
        public void NestedType()
        {
            var source = @"
namespace N1
{
    class C1
    {
        class NestedType
        {
            void M1(int x)
            {
                x = 0;
            }
        }
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 13, ExecutableLines: 1, MntIndex: 98, CycCxty: 2, DepthInherit: 1)
    N1: (Lines: 13, ExecutableLines: 1, MntIndex: 98, CycCxty: 2, DepthInherit: 1)
        C1: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
        NestedType: (Lines: 7, ExecutableLines: 1, MntIndex: 97, CycCxty: 1, DepthInherit: 1)
            N1.C1.NestedType.M1(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void GenericType()
        {
            var source = @"
namespace N1
{
    class C<T> { }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   N1: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C<T>: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void NestedTypeInTopLevelType()
        {
            var source = @"
class C1
{
    class NestedType
    {
        void M1(int x)
        {
            x = 0;
        }
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 1, MntIndex: 98, CycCxty: 2, DepthInherit: 1)
    C1: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    NestedType: (Lines: 7, ExecutableLines: 1, MntIndex: 97, CycCxty: 1, DepthInherit: 1)
        C1.NestedType.M1(int): (Lines: 4, ExecutableLines: 1, MntIndex: 97, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypesWithInheritance()
        {
            var source = @"
namespace N1
{
    class C1 { }

    class C2 : C1 { }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, CoupledTypes: {N1.C1}, DepthInherit: 2)
   N1: (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, CoupledTypes: {N1.C1}, DepthInherit: 2)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {N1.C1}, DepthInherit: 2)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypeWithCouplingFromBaseType()
        {
            var source = @"
namespace N1
{
    class C1 { }

    class C2<T> { }

    class C3 : C2<C1> { }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 8, ExecutableLines: 0, MntIndex: 100, CycCxty: 3, CoupledTypes: {N1.C1, N1.C2<T>}, DepthInherit: 2)
   N1: (Lines: 8, ExecutableLines: 0, MntIndex: 100, CycCxty: 3, CoupledTypes: {N1.C1, N1.C2<T>}, DepthInherit: 2)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C2<T>: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {N1.C1, N1.C2<T>}, DepthInherit: 2)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void TypeWithCouplingFromAttribute()
        {
            var source = @"
namespace N1
{
    class C1: System.Attribute { }

    [C1]
    class C2 { }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, CoupledTypes: {N1.C1, System.Attribute}, DepthInherit: 2)
   N1: (Lines: 7, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, CoupledTypes: {N1.C1, System.Attribute}, DepthInherit: 2)
      C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Attribute}, DepthInherit: 2)
      C2: (Lines: 2, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {N1.C1}, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyMethod()
        {
            var source = @"class C { void M() { } }";

            var expectedMetricsText = @"
Assembly: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C.M(): (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyMethod2()
        {
            var source = @"
class C
{
    void M()
    {
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C.M(): (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithIgnoreableParametersAndReturnType()
        {
            var source = @"
class C
{
    int M(string s, object o)
    {
        return 0;
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
        C.M(string, object): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithParametersAndReturnType()
        {
            var source = @"
class C
{
    C1 M(C2 s, C3 o)
    {
        return null;
    }
}

class C1 { }
class C2 { }
class C3 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 1, MntIndex: 100, CycCxty: 4, CoupledTypes: {C1, C2, C3}, DepthInherit: 1)
    C: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, C2, C3}, DepthInherit: 1)
        C.M(C2, C3): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, C2, C3})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithParameterInitializers()
        {
            var source = @"
class C1
{
    void M(int i = C2.MyConst)
    {
    }
}

class C2
{
    public const int MyConst = 0;
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 2, MntIndex: 93, CycCxty: 2, CoupledTypes: {C2}, DepthInherit: 1)
    C1: (Lines: 6, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C1.M(int): (Lines: 3, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2})
    C2: (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, DepthInherit: 1)
        C2.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithTypeReferencesInBody()
        {
            var source = @"
class C
{
    object M(C1 c)
    {
        C2 c2 = C4.MyC2;
        return (C3)null;
    }
}

class C1 { }
class C2 { }
class C3 : C1 { }
class C4 { public static C2 MyC2 = null; }
";

            var expectedMetricsText = @"
Assembly: (Lines: 12, ExecutableLines: 3, MntIndex: 95, CycCxty: 5, CoupledTypes: {C1, C2, C3, C4}, DepthInherit: 2)
    C: (Lines: 8, ExecutableLines: 2, MntIndex: 86, CycCxty: 1, CoupledTypes: {C1, C2, C3, C4}, DepthInherit: 1)
        C.M(C1): (Lines: 5, ExecutableLines: 2, MntIndex: 86, CycCxty: 1, CoupledTypes: {C1, C2, C3, C4})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    C4: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C4.MyC2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodsWithDifferentTypeReferencesInBody()
        {
            var source = @"
class C
{
    object M1(C1 c)
    {
        return (I)null;
    }

    object M2(C1 c)
    {
        return (C2)null;
    }
}

interface I { }
class C1: I { }
class C2 : C1 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 15, ExecutableLines: 2, MntIndex: 100, CycCxty: 5, CoupledTypes: {C1, C2, I}, DepthInherit: 2)
    C: (Lines: 12, ExecutableLines: 2, MntIndex: 100, CycCxty: 2, CoupledTypes: {C1, C2, I}, DepthInherit: 1)
        C.M1(C1): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, I})
        C.M2(C1): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, C2})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {I}, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    I: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void MethodWithAnonymousType()
        {
            var source = @"
class C
{
    object M1()
    {
        return new {a = 1};
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 1, MntIndex: 94, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 7, ExecutableLines: 1, MntIndex: 94, CycCxty: 1, DepthInherit: 1)
      C.M1(): (Lines: 4, ExecutableLines: 1, MntIndex: 94, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void MethodWithGenericTypes()
        {
            var source = @"
class C
{
    void M1()
    {
        G<int> a = null;
        G<string> b = null;
        G<C1> c = null;
        G<G<C2>> d = null;
        new object[]{a, b, c, d}.ToString(); // Avoid unused variable diagnostics
    }
}
class G<T> {}
class C1 {}
class C2 {}
";

            var expectedMetricsText = @"
Assembly: (Lines: 14, ExecutableLines: 5, MntIndex: 93, CycCxty: 4, CoupledTypes: {C1, C2, G<T>}, DepthInherit: 1)
   C: (Lines: 11, ExecutableLines: 5, MntIndex: 73, CycCxty: 1, CoupledTypes: {C1, C2, G<T>}, DepthInherit: 1)
      C.M1(): (Lines: 8, ExecutableLines: 5, MntIndex: 73, CycCxty: 1, CoupledTypes: {C1, C2, G<T>})
   C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   G<T>: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void CountCorrectlyGenericTypes()
        {
            var source = @"
using System.Collections.Generic;

public class A {}
public class B {}

public class C
{
    void M1()
    {
        IEnumerable<A> a = null;
        IEnumerable<B> b = null;
        new object[] { a, b }.ToString(); // Avoid unused variable diagnostics
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 11, ExecutableLines: 3, MntIndex: 93, CycCxty: 3, CoupledTypes: {A, B, System.Collections.Generic.IEnumerable<T>}, DepthInherit: 1)
    A: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    B: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C: (Lines: 9, ExecutableLines: 3, MntIndex: 80, CycCxty: 1, CoupledTypes: {A, B, System.Collections.Generic.IEnumerable<T>}, DepthInherit: 1)
        C.M1(): (Lines: 6, ExecutableLines: 3, MntIndex: 80, CycCxty: 1, CoupledTypes: {A, B, System.Collections.Generic.IEnumerable<T>})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void ExcludeCompilerGeneratedTypes()
        {
            var source = @"
[System.Runtime.CompilerServices.CompilerGeneratedAttribute]
public class A {}

[System.CodeDom.Compiler.GeneratedCodeAttribute(""SampleCodeGenerator"", ""2.0.0.0"")]
public class B {}

public class C
{
    void M1()
    {
        A a = null;
        B b = null;
        new object[] { a, b }.ToString(); // Avoid unused variable diagnostics
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 13, ExecutableLines: 5, MntIndex: 93, CycCxty: 3, CoupledTypes: {System.CodeDom.Compiler.GeneratedCodeAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute}, DepthInherit: 1)
    A: (Lines: 2, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Runtime.CompilerServices.CompilerGeneratedAttribute}, DepthInherit: 1)
    B: (Lines: 2, ExecutableLines: 2, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.CodeDom.Compiler.GeneratedCodeAttribute}, DepthInherit: 1)
    C: (Lines: 9, ExecutableLines: 3, MntIndex: 80, CycCxty: 1, DepthInherit: 1)
        C.M1(): (Lines: 6, ExecutableLines: 3, MntIndex: 80, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithClosureTypes()
        {
            var source = @"
class C
{
    void M1()
    {
        C1 a = new C1();
        new System.Action(() => {C2 b = new C2(a);})();
    }
}
class C1{}
class C2{ public C2(C1 a) {} }
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 3, MntIndex: 93, CycCxty: 3, CoupledTypes: {C1, C2, System.Action}, DepthInherit: 1)
   C: (Lines: 8, ExecutableLines: 3, MntIndex: 80, CycCxty: 1, CoupledTypes: {C1, C2, System.Action}, DepthInherit: 1)
      C.M1(): (Lines: 5, ExecutableLines: 3, MntIndex: 80, CycCxty: 1, CoupledTypes: {C1, C2, System.Action})
   C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 1)
      C2.C2(C1): (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void MethodWithLinqExpression()
        {
            var source = @"
using System.Linq;
using System.Collections.Generic;
class C
{
    IEnumerable<int> TestCa1506()
    {
        var ints = new[] { 1, 2 };
        return from a in ints
               from b in ints
               from c in ints
               from d in ints
               from e in ints
               from f in ints
               from g in ints 
               from h in ints
               from i in ints
               from j in ints
               from k in ints
               from l in ints
               from m in ints
               from n in ints
               from o in ints
               from p in ints
               select p;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 24, ExecutableLines: 18, MntIndex: 61, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>, System.Func<T, TResult>, System.Func<T1, T2, TResult>, System.Linq.Enumerable}, DepthInherit: 1)
   C: (Lines: 24, ExecutableLines: 18, MntIndex: 61, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>, System.Func<T, TResult>, System.Func<T1, T2, TResult>, System.Linq.Enumerable}, DepthInherit: 1)
      C.TestCa1506(): (Lines: 21, ExecutableLines: 18, MntIndex: 61, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>, System.Func<T, TResult>, System.Func<T1, T2, TResult>, System.Linq.Enumerable})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void MethodWithAsyncAwait()
        {
            var source = @"
using System.Threading.Tasks;
class C
{
    async Task M()
    {
        await Task.Yield();
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Runtime.CompilerServices.YieldAwaitable, System.Threading.Tasks.Task}, DepthInherit: 1)
   C: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Runtime.CompilerServices.YieldAwaitable, System.Threading.Tasks.Task}, DepthInherit: 1)
      C.M(): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Runtime.CompilerServices.YieldAwaitable, System.Threading.Tasks.Task})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact, WorkItem(2133, "https://github.com/dotnet/roslyn-analyzers/issues/2133")]
        public void MethodWithYieldReturn()
        {
            var source = @"
using System.Collections.Generic;
class C
{
    IEnumerable<int> M()
    {
        yield return 1;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>}, DepthInherit: 1)
   C: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>}, DepthInherit: 1)
      C.M(): (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Collections.Generic.IEnumerable<T>})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithTypeReferencesInAttributes()
        {
            var source = @"
class C1
{
    [CAttr(C2.MyConst)]
    [return: CAttr(C3.MyConst)]
    void M([CAttr(C4.MyConst)]int p)
    {
    }
}

class CAttr : System.Attribute { public CAttr(string s) { } }

class C2
{
    public const string MyConst = nameof(MyConst);
}

class C3
{
    public const string MyConst = nameof(MyConst);
}

class C4
{
    public const string MyConst = nameof(MyConst);
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 21, ExecutableLines: 5, MntIndex: 92, CycCxty: 5, CoupledTypes: {C2, C3, CAttr, System.Attribute}, DepthInherit: 2)
    C1: (Lines: 8, ExecutableLines: 2, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2, C3, CAttr}, DepthInherit: 1)
        C1.M(int): (Lines: 5, ExecutableLines: 2, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2, C3, CAttr})
    C2: (Lines: 4, ExecutableLines: 1, MntIndex: 90, CycCxty: 1, DepthInherit: 1)
        C2.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 90, CycCxty: 0)
    C3: (Lines: 4, ExecutableLines: 1, MntIndex: 90, CycCxty: 1, DepthInherit: 1)
        C3.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 90, CycCxty: 0)
    C4: (Lines: 4, ExecutableLines: 1, MntIndex: 90, CycCxty: 1, DepthInherit: 1)
        C4.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 90, CycCxty: 0)
    CAttr: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.Attribute}, DepthInherit: 2)
        CAttr.CAttr(string): (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldWithIgnoreableType()
        {
            var source = @"
public class C
{
    public int f;
}";

            var expectedMetricsText = @"
Assembly: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C.f: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldWithNamedType()
        {
            var source = @"
class C1
{
    private C2 f = new C2();
}

class C2 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 5, ExecutableLines: 1, MntIndex: 96, CycCxty: 2, CoupledTypes: {C2}, DepthInherit: 1)
    C1: (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C1.f: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldWithInitializer()
        {
            var source = @"
class C1
{
    private C2 f = new C2();
}

class C2 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 5, ExecutableLines: 1, MntIndex: 96, CycCxty: 2, CoupledTypes: {C2}, DepthInherit: 1)
    C1: (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C1.f: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldWithTypeReferencesInInitializer()
        {
            var source = @"
class C1
{
    private C2 f = (C3)C4.MyC2;
}

class C2 { }
class C3 : C2 { }
class C4 { public static C2 MyC2 = null; }
";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 2, MntIndex: 96, CycCxty: 4, CoupledTypes: {C2, C3, C4}, DepthInherit: 2)
    C1: (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2, C3, C4}, DepthInherit: 1)
        C1.f: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2, C3, C4})
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 2)
    C4: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C4.MyC2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldAndMethodWithTypeReferencesInInitializer()
        {
            var source = @"
class C1
{
    private C2 f = (C3)C4.MyC2;

    object M(C1 c)
    {
        C2 c2 = C4.MyC2;
        return (C3)null;
    }
}

class C2 { }
class C3 : C2 { }
class C4 { public static C2 MyC2 = null; }
";

            var expectedMetricsText = @"
Assembly: (Lines: 13, ExecutableLines: 4, MntIndex: 95, CycCxty: 4, CoupledTypes: {C2, C3, C4}, DepthInherit: 2)
    C1: (Lines: 10, ExecutableLines: 3, MntIndex: 87, CycCxty: 1, CoupledTypes: {C2, C3, C4}, DepthInherit: 1)
        C1.f: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2, C3, C4})
        C1.M(C1): (Lines: 5, ExecutableLines: 2, MntIndex: 86, CycCxty: 1, CoupledTypes: {C2, C3, C4})
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 2)
    C4: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C4.MyC2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ManySimpleFieldsAndOneComplexMethod()
        {
            var source = @"
public class C1
{
    public object f1 = null;
    public object f2 = null;
    public object f3 = null;
    public object f4 = null;
    public object f5 = null;
    public object f6 = null;
    public object f7 = null;
    public object f8 = null;
    public object f9 = null;
    public object f10 = null;
    public object f11 = null;
    public object f12 = null;
    public object f13 = null;
    public object f14 = null;
    public object f15 = null;
    public object f16 = null;
    public object f17 = null;
    public object f18 = null;
    public object f19 = null;
    public object f20 = null;    

    void MultipleLogicals(bool b1, bool b2, bool b3, bool b4, bool b5)
    {
        var x1 = b1 && b2 || b3;
        var x2 = b1 && (b2 && b3 || b4);
        var x3 = b3 && b4 || b5;
        var x4 = b1 && (b2 && b3 || b4 && b5);
        var x5 = b1 && (b2 && b3 || b4);
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 32, ExecutableLines: 7, MntIndex: 77, CycCxty: 15, DepthInherit: 1)
    C1: (Lines: 32, ExecutableLines: 7, MntIndex: 77, CycCxty: 15, DepthInherit: 1)
        C1.f1: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f3: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f4: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f5: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f6: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f7: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f8: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f9: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f10: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f11: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f12: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f13: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f14: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f15: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f16: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f17: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f18: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f19: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.f20: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
        C1.MultipleLogicals(bool, bool, bool, bool, bool): (Lines: 8, ExecutableLines: 5, MntIndex: 67, CycCxty: 15)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MultipleFieldsWithDifferentTypeReferencesInBody()
        {
            var source = @"
class C
{
    private object f1 = (I)new C1();
    private object f2 = (C2)new C1();
}

interface I { }
class C1: I { }
class C2 : C1 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 8, ExecutableLines: 2, MntIndex: 98, CycCxty: 4, CoupledTypes: {C1, C2, I}, DepthInherit: 2)
    C: (Lines: 5, ExecutableLines: 2, MntIndex: 93, CycCxty: 1, CoupledTypes: {C1, C2, I}, DepthInherit: 1)
        C.f1: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C1, I})
        C.f2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C1, C2})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {I}, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    I: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MultipleFieldsInSameDeclaration()
        {
            var source = @"
class C
{
    private object f1 = (I)new C1(), f2 = (C2)new C1();
}

interface I { }
class C1: I { }
class C2 : C1 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 7, ExecutableLines: 2, MntIndex: 98, CycCxty: 4, CoupledTypes: {C1, C2, I}, DepthInherit: 2)
    C: (Lines: 4, ExecutableLines: 2, MntIndex: 93, CycCxty: 1, CoupledTypes: {C1, C2, I}, DepthInherit: 1)
        C.f1: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C1, I})
        C.f2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C1, C2})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {I}, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    I: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 0)
    
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void FieldWithTypeReferencesInAttribute()
        {
            var source = @"
public class C1
{
    [System.Obsolete(C2.MyConst)]
    public object f = null;
}

public class C2
{
    public const string MyConst = nameof(MyConst);
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 9, ExecutableLines: 3, MntIndex: 88, CycCxty: 2, CoupledTypes: {C2, System.ObsoleteAttribute}, DepthInherit: 1)
    C1: (Lines: 5, ExecutableLines: 2, MntIndex: 87, CycCxty: 1, CoupledTypes: {C2, System.ObsoleteAttribute}, DepthInherit: 1)
        C1.f: (Lines: 1, ExecutableLines: 2, MntIndex: 87, CycCxty: 0, CoupledTypes: {C2, System.ObsoleteAttribute})
    C2: (Lines: 4, ExecutableLines: 1, MntIndex: 90, CycCxty: 1, DepthInherit: 1)
        C2.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 90, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyProperty()
        {
            var source = @"class C { int P { get; } }";

            var expectedMetricsText = @"
Assembly: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
   C: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
      C.P: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
         C.P.get: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyProperty2()
        {
            var source = @"
class C
{
    int P
    {
        get
        {
            return 0;
        }
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C: (Lines: 10, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
        C.P: (Lines: 7, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
            C.P.get: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyProperty3()
        {
            var source = @"class C { int P { get; set; } }";

            var expectedMetricsText = @"
Assembly: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
   C: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
      C.P: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 2)
         C.P.get: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
         C.P.set: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEmptyProperty4()
        {
            var source = @"
class C
{
    int P
    {
        get
        {
            return 0;
        }
        set
        {
        }
    }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 13, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
    C: (Lines: 13, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
        C.P: (Lines: 10, ExecutableLines: 1, MntIndex: 100, CycCxty: 2)
            C.P.get: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
            C.P.set: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertyWithIgnoreableParametersAndReturnType()
        {
            var source = @"
class C
{
    int this[object x] { get { return 0; } set { } }
}";

            var expectedMetricsText = @"
Assembly: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
    C: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
        C.this[object]: (Lines: 1, ExecutableLines: 1, MntIndex: 100, CycCxty: 2)
            C.this[object].get: (Lines: 1, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
            C.this[object].set: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertyWithParametersAndReturnType()
        {
            var source = @"
class C
{
    C1 this[C2 x] { get { return null; } set { } }
}

class C1 { }
class C2 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 6, ExecutableLines: 1, MntIndex: 100, CycCxty: 4, CoupledTypes: {C1, C2}, DepthInherit: 1)
    C: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, CoupledTypes: {C1, C2}, DepthInherit: 1)
        C.this[C2]: (Lines: 1, ExecutableLines: 1, MntIndex: 100, CycCxty: 2, CoupledTypes: {C1, C2})
            C.this[C2].get: (Lines: 1, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, C2})
            C.this[C2].set: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1, C2})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertyWithParameterInitializers()
        {
            var source = @"
class C
{
#pragma warning disable CS1066
    C1 this[int i = C2.MyConst] { get { return null; } set { } }
}

class C1 { }

class C2
{
    public const int MyConst = 0;
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 4, MntIndex: 97, CycCxty: 4, CoupledTypes: {C1, C2}, DepthInherit: 1)
    C: (Lines: 5, ExecutableLines: 3, MntIndex: 100, CycCxty: 2, CoupledTypes: {C1, C2}, DepthInherit: 1)
        C.this[int]: (Lines: 2, ExecutableLines: 3, MntIndex: 100, CycCxty: 2, CoupledTypes: {C1, C2})
            C.this[int].get: (Lines: 1, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1})
            C.this[int].set: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C2: (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, DepthInherit: 1)
        C2.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertyWithTypeReferencesInBody()
        {
            var source = @"
class C
{
    C3 P
    {
        get
        {
            C2 c2 = C4.MyC2;
            return (C3)null;
        }
        set
        {
            object c2 = C4.MyC2;
            c2 = (C3)null;
        }
    }
}

class C1 { }
class C2 : C3 { }
class C3 : C1 { }
class C4 { public static C2 MyC2 = null; }
";

            var expectedMetricsText = @"
Assembly: (Lines: 20, ExecutableLines: 5, MntIndex: 95, CycCxty: 6, CoupledTypes: {C1, C2, C3, C4}, DepthInherit: 3)
    C: (Lines: 16, ExecutableLines: 4, MntIndex: 86, CycCxty: 2, CoupledTypes: {C2, C3, C4}, DepthInherit: 1)
        C.P: (Lines: 13, ExecutableLines: 4, MntIndex: 86, CycCxty: 2, CoupledTypes: {C2, C3, C4})
            C.P.get: (Lines: 5, ExecutableLines: 2, MntIndex: 86, CycCxty: 1, CoupledTypes: {C2, C3, C4})
            C.P.set: (Lines: 5, ExecutableLines: 2, MntIndex: 86, CycCxty: 1, CoupledTypes: {C2, C3, C4})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C3}, DepthInherit: 3)
    C3: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    C4: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, CoupledTypes: {C2}, DepthInherit: 1)
        C4.MyC2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertiesWithDifferentTypeReferencesInBody()
        {
            var source = @"
class C
{
    object P
    {
        get
        {
            return (I)null;
        }
        set
        {
            value = (C2)null;
        }
    }
}

interface I { }
class C1 : I { }
class C2 : C1 { }
";

            var expectedMetricsText = @"
Assembly: (Lines: 17, ExecutableLines: 2, MntIndex: 99, CycCxty: 5, CoupledTypes: {C1, C2, I}, DepthInherit: 2)
    C: (Lines: 14, ExecutableLines: 2, MntIndex: 98, CycCxty: 2, CoupledTypes: {C2, I}, DepthInherit: 1)
        C.P: (Lines: 11, ExecutableLines: 2, MntIndex: 98, CycCxty: 2, CoupledTypes: {C2, I})
            C.P.get: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1, CoupledTypes: {I})
            C.P.set: (Lines: 4, ExecutableLines: 1, MntIndex: 96, CycCxty: 1, CoupledTypes: {C2})
    C1: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {I}, DepthInherit: 1)
    C2: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C1}, DepthInherit: 2)
    I: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void PropertyWithTypeReferencesInAttribute()
        {
            var source = @"
class C1
{
    [System.Obsolete(C2.MyConst)]
    object P
    {
        get
        {
            return null;
        }
        set
        {
        }
    }
}

class C2
{
    public const string MyConst = nameof(MyConst);
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 18, ExecutableLines: 4, MntIndex: 95, CycCxty: 3, CoupledTypes: {C2, System.ObsoleteAttribute}, DepthInherit: 1)
    C1: (Lines: 14, ExecutableLines: 3, MntIndex: 100, CycCxty: 2, CoupledTypes: {C2, System.ObsoleteAttribute}, DepthInherit: 1)
        C1.P: (Lines: 11, ExecutableLines: 3, MntIndex: 100, CycCxty: 2, CoupledTypes: {C2, System.ObsoleteAttribute})
            C1.P.get: (Lines: 4, ExecutableLines: 1, MntIndex: 100, CycCxty: 1)
            C1.P.set: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
    C2: (Lines: 4, ExecutableLines: 1, MntIndex: 90, CycCxty: 1, DepthInherit: 1)
        C2.MyConst: (Lines: 1, ExecutableLines: 1, MntIndex: 90, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleFieldLikeEvent()
        {
            var source = @"
using System;

class C
{
    public delegate void SampleEventHandler(object sender, EventArgs e);

    public event SampleEventHandler SampleEvent = C2.MyHandler;
}

class C2
{
    public static void MyHandler(object sender, EventArgs e) { }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 3, CoupledTypes: {C.SampleEventHandler, C2, System.EventArgs}, DepthInherit: 1)
   C: (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {C.SampleEventHandler, C2}, DepthInherit: 1)
      C.SampleEvent: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, CoupledTypes: {C.SampleEventHandler, C2})
   SampleEventHandler: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.EventArgs}, DepthInherit: 1)
   C2: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.EventArgs}, DepthInherit: 1)
      C2.MyHandler(object, System.EventArgs): (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.EventArgs})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void SimpleEventWithAccessors()
        {
            var source = @"
using System;

class C
{
    public event EventHandler ExplicitEvent
    {
        add { C2.ExplicitEvent += value; }
        remove { C3.ExplicitEvent -= value; }
    }
}

class C2
{
    public static EventHandler ExplicitEvent;
}

class C3
{
    public static EventHandler ExplicitEvent;
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 16, ExecutableLines: 2, MntIndex: 98, CycCxty: 4, CoupledTypes: {C2, C3, System.EventHandler}, DepthInherit: 1)
    C: (Lines: 8, ExecutableLines: 2, MntIndex: 94, CycCxty: 2, CoupledTypes: {C2, C3, System.EventHandler}, DepthInherit: 1)
        C.ExplicitEvent: (Lines: 5, ExecutableLines: 2, MntIndex: 94, CycCxty: 2, CoupledTypes: {C2, C3, System.EventHandler})
            C.ExplicitEvent.add: (Lines: 1, ExecutableLines: 1, MntIndex: 94, CycCxty: 1, CoupledTypes: {C2, System.EventHandler})
            C.ExplicitEvent.remove: (Lines: 1, ExecutableLines: 1, MntIndex: 94, CycCxty: 1, CoupledTypes: {C3, System.EventHandler})
    C2: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.EventHandler}, DepthInherit: 1)
        C2.ExplicitEvent: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, CoupledTypes: {System.EventHandler})
    C3: (Lines: 4, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, CoupledTypes: {System.EventHandler}, DepthInherit: 1)
        C3.ExplicitEvent: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0, CoupledTypes: {System.EventHandler})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ConditionalLogic_IfStatement()
        {
            var source = @"
class C
{
    void SimpleIf(bool b)
    {
        if (b)
        {
        }
    }

    void SimpleIfElse(bool b)
    {
        if (b)
        {
        }
        else
        {
        }
    }

    void NestedIf(bool b, bool b2)
    {
        if (b)
        {
            if (b2)
            {
            }
        }
    }

    void ElseIf(bool b, bool b2)
    {
        if (b)
        {
        }
        else if (b2)
        {
        }
    }

    void MultipleIfs(bool b, bool b2)
    {
        if (b)
        {
        }

        if (b2)
        {
        }

        if (b2)
        {
        }

        if (b2)
        {
        }

        if (b2)
        {
        }
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 62, ExecutableLines: 11, MntIndex: 87, CycCxty: 16, DepthInherit: 1)
    C: (Lines: 62, ExecutableLines: 11, MntIndex: 87, CycCxty: 16, DepthInherit: 1)
        C.SimpleIf(bool): (Lines: 6, ExecutableLines: 1, MntIndex: 100, CycCxty: 2)
        C.SimpleIfElse(bool): (Lines: 9, ExecutableLines: 1, MntIndex: 100, CycCxty: 2)
        C.NestedIf(bool, bool): (Lines: 9, ExecutableLines: 2, MntIndex: 91, CycCxty: 3)
        C.ElseIf(bool, bool): (Lines: 9, ExecutableLines: 2, MntIndex: 91, CycCxty: 3)
        C.MultipleIfs(bool, bool): (Lines: 22, ExecutableLines: 5, MntIndex: 79, CycCxty: 6)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ConditionalLogic_ConditionalExpression()
        {
            var source = @"
class C
{
    void SimpleConditional(bool b)
    {
        var x = b ? true : false;
    }

    void NestedConditional(bool b, bool b2, bool b3, bool b4)
    {
        var x = b ? true : b2 ? b3 : b4;
    }

    void MultipleConditionals(bool b1, bool b2, bool b3, bool b4, bool b5)
    {
        var x1 = b1 ? true : false;
        var x2 = b2 ? true : false;
        var x3 = b3 ? true : false;
        var x4 = b4 ? true : false;
        var x5 = b5 ? true : false;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 21, ExecutableLines: 8, MntIndex: 81, CycCxty: 11, DepthInherit: 1)
    C: (Lines: 21, ExecutableLines: 8, MntIndex: 81, CycCxty: 11, DepthInherit: 1)
        C.SimpleConditional(bool): (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 2)
        C.NestedConditional(bool, bool, bool, bool): (Lines: 4, ExecutableLines: 2, MntIndex: 84, CycCxty: 3)
        C.MultipleConditionals(bool, bool, bool, bool, bool): (Lines: 8, ExecutableLines: 5, MntIndex: 70, CycCxty: 6)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ConditionalLogic_LogicalOperators()
        {
            var source = @"
class C
{
    void SimpleLogical(bool b1, bool b2, bool b3, bool b4)
    {
        var x = b1 && b2 || b3;
    }

    void NestedLogical(bool b1, bool b2, bool b3, bool b4)
    {
        var x = b1 && (b2 && b3 || b4);
    }

    void MultipleLogicals(bool b1, bool b2, bool b3, bool b4, bool b5)
    {
        var x1 = b1 && b2 || b3;
        var x2 = b1 && (b2 && b3 || b4);
        var x3 = b3 && b4 || b5;
        var x4 = b1 && (b2 && b3 || b4 && b5);
        var x5 = b1 && (b2 && b3 || b4);
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 21, ExecutableLines: 7, MntIndex: 79, CycCxty: 22, DepthInherit: 1)
    C: (Lines: 21, ExecutableLines: 7, MntIndex: 79, CycCxty: 22, DepthInherit: 1)
        C.SimpleLogical(bool, bool, bool, bool): (Lines: 4, ExecutableLines: 1, MntIndex: 91, CycCxty: 3)
        C.NestedLogical(bool, bool, bool, bool): (Lines: 4, ExecutableLines: 1, MntIndex: 89, CycCxty: 4)
        C.MultipleLogicals(bool, bool, bool, bool, bool): (Lines: 8, ExecutableLines: 5, MntIndex: 67, CycCxty: 15)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ConditionalLogic_CoalesceAndConditionalAccess()
        {
            var source = @"
class C
{
    private readonly C2 c2 = null;
    private readonly bool b = false;

    void SimpleCoalesce(bool? b)
    {
        var x = b ?? true;
    }

    void SimpleConditionalAccess(C c)
    {
        var x = c?.b;
    }

    void NestedCoalesce(bool? b, bool? b2)
    {
        var x = b ?? b2 ?? true;
    }

    void NestedConditionalAccess(C c)
    {
        var x = c?.c2?.B;
    }

    void MultipleCoalesceAndConditionalAccess(C c1, C c2, C c3, C c4, C c5)
    {
        var x1 = c1?.c2?.B;
        var x2 = (c2 ?? c3)?.c2?.B;
        var x3 = (c4.c2 ?? c5.c2)?.B;
        var x4 = c1?.b ?? c2?.b ?? c3?.b ?? (c4 ?? c5).b;
    }
}

class C2 { public readonly bool B = false; }
";

            var expectedMetricsText = @"
Assembly: (Lines: 34, ExecutableLines: 11, MntIndex: 90, CycCxty: 26, CoupledTypes: {C2, System.Nullable<T>}, DepthInherit: 1)
   C: (Lines: 33, ExecutableLines: 10, MntIndex: 87, CycCxty: 25, CoupledTypes: {C2, System.Nullable<T>}, DepthInherit: 1)
      C.c2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0, CoupledTypes: {C2})
      C.b: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
      C.SimpleCoalesce(bool?): (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 2, CoupledTypes: {System.Nullable<T>})
      C.SimpleConditionalAccess(C): (Lines: 4, ExecutableLines: 1, MntIndex: 93, CycCxty: 2, CoupledTypes: {System.Nullable<T>})
      C.NestedCoalesce(bool?, bool?): (Lines: 4, ExecutableLines: 1, MntIndex: 92, CycCxty: 3, CoupledTypes: {System.Nullable<T>})
      C.NestedConditionalAccess(C): (Lines: 4, ExecutableLines: 1, MntIndex: 91, CycCxty: 3, CoupledTypes: {C2, System.Nullable<T>})
      C.MultipleCoalesceAndConditionalAccess(C, C, C, C, C): (Lines: 7, ExecutableLines: 4, MntIndex: 69, CycCxty: 15, CoupledTypes: {C2, System.Nullable<T>})
   C2: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 1, DepthInherit: 1)
      C2.B: (Lines: 1, ExecutableLines: 1, MntIndex: 93, CycCxty: 0)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void ConditionalLogic_Loops()
        {
            var source = @"
class C
{
    void SimpleWhileLoop(bool b, int i)
    {
        while (b)
        {
            i++;
        }
    }

    void SimpleForLoop()
    {
        for (int i = 0; i < 10; i++)
        {
            System.Console.WriteLine(i);
        }
    }

    void SimpleForEachLoop(int[] a)
    {
        foreach (var i in a)
        {
            System.Console.WriteLine(i);
        }
    }

    void NestedLoops(bool b, int[] a)
    {
        while (b)
        {
            for (int i = 0; i < 10; i++)
            {
                System.Console.WriteLine(i);
            }

            foreach (var i in a)
            {
                System.Console.WriteLine(i);
            }
        }
    }

    void MultipleLoops(bool b, int[] a, int j)
    {
        while (b)
        {
            j++;
        }

        for (int i = 0; i < 10; i++)
        {
            System.Console.WriteLine(i);;
        }

        foreach (var i in a)
        {
            System.Console.WriteLine(i);
        }

        while (b)
        {
            j++;
        }

        for (int i = 0; i < 10; i++)
        {
            System.Console.WriteLine(i);
        }

        foreach (var i in a)
        {
            System.Console.WriteLine(i);
        }
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 75, ExecutableLines: 23, MntIndex: 74, CycCxty: 17, CoupledTypes: {System.Collections.IEnumerable, System.Console}, DepthInherit: 1)
    C: (Lines: 75, ExecutableLines: 23, MntIndex: 74, CycCxty: 17, CoupledTypes: {System.Collections.IEnumerable, System.Console}, DepthInherit: 1)
        C.SimpleWhileLoop(bool, int): (Lines: 7, ExecutableLines: 2, MntIndex: 90, CycCxty: 2)
        C.SimpleForLoop(): (Lines: 7, ExecutableLines: 2, MntIndex: 84, CycCxty: 2, CoupledTypes: {System.Console})
        C.SimpleForEachLoop(int[]): (Lines: 7, ExecutableLines: 2, MntIndex: 90, CycCxty: 2, CoupledTypes: {System.Collections.IEnumerable, System.Console})
        C.NestedLoops(bool, int[]): (Lines: 15, ExecutableLines: 5, MntIndex: 73, CycCxty: 4, CoupledTypes: {System.Collections.IEnumerable, System.Console})
        C.MultipleLoops(bool, int[], int): (Lines: 32, ExecutableLines: 12, MntIndex: 61, CycCxty: 7, CoupledTypes: {System.Collections.IEnumerable, System.Console})
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void LocalDeclarationsAndArithmeticOperations()
        {
            var source = @"
class C1
{
    void M()
    {
        int x = 0;
        x++;
        int y = 1;
        int z = y * x * 1;
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 10, ExecutableLines: 4, MntIndex: 76, CycCxty: 1, DepthInherit: 1)
    C1: (Lines: 10, ExecutableLines: 4, MntIndex: 76, CycCxty: 1, DepthInherit: 1)
        C1.M(): (Lines: 7, ExecutableLines: 4, MntIndex: 76, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void MethodWithLotOfArithmeticOperations()
        {
            var source = @"
class C1
{
    void M(
        int x1, int y1,
        int x2, int y2,
        int x3, int y3,
        int x4, int y4,
        int x5, int y5,
        int x6, int y6,
        int x7, int y7,
        int x8, int y8,
        int x9, int y9,
        int x10, int y10)
    {
        if (true)
        {
            var u1 = y1 - x1 * 1;
            var u2 = y2 + x2 / 1;
            var u3 = y3 > x3 % 1;
            var u4 = y4 < x4 * 1;
            var u5 = y5 * x5 * 1;
            var u6 = y6 * x6 * 1;
            var u7 = y7 * x7 * 1;
            var u8 = y8 * x8 * 1;
            var u9 = y9 * x9 * 1;
            var u10 = y10 * x10 * 1;

            var v1 = y1 - x1 * 1;
            var v2 = y2 + x2 / 1;
            var v3 = y3 > x3 % 1;
            var v4 = y4 < x4 * 1;
            var v5 = y5 * x5 * 1;
            var v6 = y6 * x6 * 1;
            var v7 = y7 * x7 * 1;
            var v8 = y8 * x8 * 1;
            var v9 = y9 * x9 * 1;
            var v10 = y10 * x10 * 1;

            var w1 = y1 - x1 * 1;
            var w2 = y2 + x2 / 1;
            var w3 = y3 > x3 % 1;
            var w4 = y4 < x4 * 1;
            var w5 = y5 * x5 * 1;
            var w6 = y6 * x6 * 1;
            var w7 = y7 * x7 * 1;
            var w8 = y8 * x8 * 1;
            var w9 = y9 * x9 * 1;
            var w10 = y10 * x10 * 1;

            var z1 = y1 - x1 * 1;
            var z2 = y2 + x2 / 1;
            var z3 = y3 > x3 % 1;
            var z4 = y4 < x4 * 1;
            var z5 = y5 * x5 * 1;
            var z6 = y6 * x6 * 1;
            var z7 = y7 * x7 * 1;
            var z8 = y8 * x8 * 1;
            var z9 = y9 * x9 * 1;
            var z10 = y10 * x10 * 1;
        }

        if (true)
        {
            var u1 = y1 - x1 * 1;
            var u2 = y2 + x2 / 1;
            var u3 = y3 > x3 % 1;
            var u4 = y4 < x4 * 1;
            var u5 = y5 * x5 * 1;
            var u6 = y6 * x6 * 1;
            var u7 = y7 * x7 * 1;
            var u8 = y8 * x8 * 1;
            var u9 = y9 * x9 * 1;
            var u10 = y10 * x10 * 1;

            var v1 = y1 - x1 * 1;
            var v2 = y2 + x2 / 1;
            var v3 = y3 > x3 % 1;
            var v4 = y4 < x4 * 1;
            var v5 = y5 * x5 * 1;
            var v6 = y6 * x6 * 1;
            var v7 = y7 * x7 * 1;
            var v8 = y8 * x8 * 1;
            var v9 = y9 * x9 * 1;
            var v10 = y10 * x10 * 1;

            var w1 = y1 - x1 * 1;
            var w2 = y2 + x2 / 1;
            var w3 = y3 > x3 % 1;
            var w4 = y4 < x4 * 1;
            var w5 = y5 * x5 * 1;
            var w6 = y6 * x6 * 1;
            var w7 = y7 * x7 * 1;
            var w8 = y8 * x8 * 1;
            var w9 = y9 * x9 * 1;
            var w10 = y10 * x10 * 1;

            var z1 = y1 - x1 * 1;
            var z2 = y2 + x2 / 1;
            var z3 = y3 > x3 % 1;
            var z4 = y4 < x4 * 1;
            var z5 = y5 * x5 * 1;
            var z6 = y6 * x6 * 1;
            var z7 = y7 * x7 * 1;
            var z8 = y8 * x8 * 1;
            var z9 = y9 * x9 * 1;
            var z10 = y10 * x10 * 1;
        }

        if (true)
        {
            var u1 = y1 - x1 * 1;
            var u2 = y2 + x2 / 1;
            var u3 = y3 > x3 % 1;
            var u4 = y4 < x4 * 1;
            var u5 = y5 * x5 * 1;
            var u6 = y6 * x6 * 1;
            var u7 = y7 * x7 * 1;
            var u8 = y8 * x8 * 1;
            var u9 = y9 * x9 * 1;
            var u10 = y10 * x10 * 1;

            var v1 = y1 - x1 * 1;
            var v2 = y2 + x2 / 1;
            var v3 = y3 > x3 % 1;
            var v4 = y4 < x4 * 1;
            var v5 = y5 * x5 * 1;
            var v6 = y6 * x6 * 1;
            var v7 = y7 * x7 * 1;
            var v8 = y8 * x8 * 1;
            var v9 = y9 * x9 * 1;
            var v10 = y10 * x10 * 1;

            var w1 = y1 - x1 * 1;
            var w2 = y2 + x2 / 1;
            var w3 = y3 > x3 % 1;
            var w4 = y4 < x4 * 1;
            var w5 = y5 * x5 * 1;
            var w6 = y6 * x6 * 1;
            var w7 = y7 * x7 * 1;
            var w8 = y8 * x8 * 1;
            var w9 = y9 * x9 * 1;
            var w10 = y10 * x10 * 1;

            var z1 = y1 - x1 * 1;
            var z2 = y2 + x2 / 1;
            var z3 = y3 > x3 % 1;
            var z4 = y4 < x4 * 1;
            var z5 = y5 * x5 * 1;
            var z6 = y6 * x6 * 1;
            var z7 = y7 * x7 * 1;
            var z8 = y8 * x8 * 1;
            var z9 = y9 * x9 * 1;
            var z10 = y10 * x10 * 1;
        }
    }
}
";

            var expectedMetricsText = @"
Assembly: (Lines: 156, ExecutableLines: 123, MntIndex: 27, CycCxty: 4, DepthInherit: 1)
    C1: (Lines: 156, ExecutableLines: 123, MntIndex: 27, CycCxty: 4, DepthInherit: 1)
        C1.M(int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int): (Lines: 153, ExecutableLines: 123, MntIndex: 27, CycCxty: 4)
";

            VerifyCSharp(source, expectedMetricsText);
        }

        [Fact]
        public void VisualBasicTest01()
        {
            var source = @"
Class C
    Sub M(i As Integer)
        Select Case i
            Case 0
                Exit Select
            Case 1
                Exit Select
            Case Else
        End Select
    End Sub
End Class
";

            var expectedMetricsText = @"
Assembly: (Lines: 11, ExecutableLines: 3, MntIndex: 81, CycCxty: 4, DepthInherit: 1)
    C: (Lines: 11, ExecutableLines: 3, MntIndex: 81, CycCxty: 4, DepthInherit: 1)
        Public Sub M(i As Integer): (Lines: 9, ExecutableLines: 3, MntIndex: 81, CycCxty: 4)
";

            VerifyBasic(source, expectedMetricsText);
        }

        [Fact]
        public void VisualBasicTest02()
        {
            var source = @"
Namespace N1
    Class C
        Sub M(Of T)(i As Integer)
        End Sub

        Class NestedClass(Of U)
            Public Field0 As Integer = 0
            Public Field1, Field2 As New Integer
        End Class
    End Class
End Namespace

Class TopLevel
    Public ReadOnly Property P As Integer
End Class

";

            var expectedMetricsText = @"
Assembly: (Lines: 14, ExecutableLines: 2, MntIndex: 100, CycCxty: 3, DepthInherit: 1)
    TopLevel: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
        Public ReadOnly Property P As Integer: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0)
    N1: (Lines: 11, ExecutableLines: 2, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
        C: (Lines: 9, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            Public Sub M(Of T)(i As Integer): (Lines: 2, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        NestedClass(Of U): (Lines: 4, ExecutableLines: 2, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            Public Field0 As Integer: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0)
            Public Field1 As Integer: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0)
            Public Field2 As Integer: (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 0)
";

            VerifyBasic(source, expectedMetricsText);
        }

        [Fact]
        public void EmptyLinesAreNotCounted()
        {
            var source = @"



namespace N1
{



    class C1_10_lines
    {

    
        void M1_3_lines_has_leading_and_trailing_newlines()
        {
        }


    }

    class C2_13_lines {



        void M2_2_lines_has_leading_newlines()
        { }
        void M3_3_lines_has_trailing_newlines()
        {
        }



    }

    class C3_3_lines
    {   void M4_1_lines_has_no_newlines() { }
    }

    class C4_14_lines
    {


        // Leading Comment

        void M5_5_lines_has_leading_trailing_newlines_and_comments()
        {
        }

        // Trailing Comment does not count for method above


    }

    class C5_12_lines
    {


        // Leading Comment1
        // Leading Comment2
        void M6_5_lines_has_leading_comments_trailing_newlines()
        {
        }


    }

    class C6_11_lines
    {


        void M7_3_lines_has_trailing_comments_leading_newlines()
        {
        }

        // Trailing Comment1 does not count for method above
        // Trailing Comment2 does not count for method above
    }

    class C7_9_lines
    {

        /// <summary>
        /// </summary>
        void M8_5_lines_has_doc_comment()
        {
        }
    }

    class C8_10_lines
    {

        /*
            Block comment
        */
        void M9_6_lines_has_leading_block_comment()
        {
        }
    }

    class C9_10_lines
    {

        // Leading Comment1

        // Leading Comment2
        void M10_6_lines_blank_lines_between_leading_comments_are_counted()
        {
        }
    }
}


";

            var expectedMetricsText = @"
Assembly: (Lines: 106, ExecutableLines: 0, MntIndex: 100, CycCxty: 10, DepthInherit: 1)
    N1: (Lines: 106, ExecutableLines: 0, MntIndex: 100, CycCxty: 10, DepthInherit: 1)
        C1_10_lines: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C1_10_lines.M1_3_lines_has_leading_and_trailing_newlines(): (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C2_13_lines: (Lines: 13, ExecutableLines: 0, MntIndex: 100, CycCxty: 2, DepthInherit: 1)
            N1.C2_13_lines.M2_2_lines_has_leading_newlines(): (Lines: 2, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
            N1.C2_13_lines.M3_3_lines_has_trailing_newlines(): (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C3_3_lines: (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C3_3_lines.M4_1_lines_has_no_newlines(): (Lines: 1, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C4_14_lines: (Lines: 14, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C4_14_lines.M5_5_lines_has_leading_trailing_newlines_and_comments(): (Lines: 5, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C5_12_lines: (Lines: 12, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C5_12_lines.M6_5_lines_has_leading_comments_trailing_newlines(): (Lines: 5, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C6_11_lines: (Lines: 11, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C6_11_lines.M7_3_lines_has_trailing_comments_leading_newlines(): (Lines: 3, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C7_9_lines: (Lines: 9, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C7_9_lines.M8_5_lines_has_doc_comment(): (Lines: 5, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C8_10_lines: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C8_10_lines.M9_6_lines_has_leading_block_comment(): (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
        C9_10_lines: (Lines: 10, ExecutableLines: 0, MntIndex: 100, CycCxty: 1, DepthInherit: 1)
            N1.C9_10_lines.M10_6_lines_blank_lines_between_leading_comments_are_counted(): (Lines: 6, ExecutableLines: 0, MntIndex: 100, CycCxty: 1)
";

            VerifyCSharp(source, expectedMetricsText);
        }
    }
}
