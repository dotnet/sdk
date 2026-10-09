// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public partial class UseConcreteTypeTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DynamicData(nameof(AnonymousTypeShapes))]
        public void ContainsAnonymousType_TraversesTypeShapes(string shape, bool containsAnonymousType)
        {
            // Construct symbols directly for pointer signatures that cannot name an anonymous type in source.
            var compilation = CSharpCompilation.Create("Test",
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(
                    "class Outer<T> { public class Inner<U> { } }", cancellationToken: TestContext.CancellationToken) },
                references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
            ITypeSymbol elementType = containsAnonymousType
                ? compilation.CreateAnonymousTypeSymbol(
                    ImmutableArray.Create<ITypeSymbol>(compilation.GetSpecialType(SpecialType.System_Int32)),
                    ImmutableArray.Create("Value"))
                : compilation.GetSpecialType(SpecialType.System_Int32);
            var voidType = compilation.GetSpecialType(SpecialType.System_Void);
            ITypeSymbol type = shape switch
            {
                "type" => elementType,
                "array" => compilation.CreateArrayTypeSymbol(elementType),
                "multidimensional array" => compilation.CreateArrayTypeSymbol(elementType, rank: 2),
                "jagged array" => compilation.CreateArrayTypeSymbol(compilation.CreateArrayTypeSymbol(elementType)),
                "generic" => compilation.GetTypeByMetadataName("System.Collections.Generic.List`1").Construct(elementType),
                "containing generic" => compilation.GetTypeByMetadataName("Outer`1").Construct(elementType)
                    .GetTypeMembers("Inner")[0].Construct(compilation.GetSpecialType(SpecialType.System_Int32)),
                "tuple" => compilation.CreateTupleTypeSymbol(
                    ImmutableArray.Create(elementType, compilation.GetSpecialType(SpecialType.System_Int32))),
                "pointer" => compilation.CreatePointerTypeSymbol(elementType),
                "pointer to pointer" => compilation.CreatePointerTypeSymbol(compilation.CreatePointerTypeSymbol(elementType)),
                "array of pointers" => compilation.CreateArrayTypeSymbol(compilation.CreatePointerTypeSymbol(elementType)),
                "function pointer return" => compilation.CreateFunctionPointerTypeSymbol(
                    elementType, RefKind.None, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<RefKind>.Empty),
                "function pointer ref return" => compilation.CreateFunctionPointerTypeSymbol(
                    elementType, RefKind.Ref, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<RefKind>.Empty),
                "function pointer ref readonly return" => compilation.CreateFunctionPointerTypeSymbol(
                    elementType, RefKind.RefReadOnly, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<RefKind>.Empty),
                "function pointer parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create(elementType), ImmutableArray.Create(RefKind.None)),
                "function pointer ref parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create(elementType), ImmutableArray.Create(RefKind.Ref)),
                "function pointer out parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create(elementType), ImmutableArray.Create(RefKind.Out)),
                "function pointer in parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create(elementType), ImmutableArray.Create(RefKind.In)),
                "function pointer ref readonly parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create(elementType), ImmutableArray.Create(RefKind.RefReadOnlyParameter)),
                "function pointer later parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None,
                    ImmutableArray.Create<ITypeSymbol>(compilation.GetSpecialType(SpecialType.System_Int32), elementType),
                    ImmutableArray.Create(RefKind.None, RefKind.None)),
                "function pointer with pointer parameter" => compilation.CreateFunctionPointerTypeSymbol(
                    voidType, RefKind.None, ImmutableArray.Create<ITypeSymbol>(compilation.CreatePointerTypeSymbol(elementType)),
                    ImmutableArray.Create(RefKind.None)),
                "nested function pointer" => compilation.CreateFunctionPointerTypeSymbol(
                    compilation.CreateFunctionPointerTypeSymbol(
                        elementType, RefKind.Ref, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<RefKind>.Empty),
                    RefKind.None, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<RefKind>.Empty),
                _ => throw new System.ArgumentOutOfRangeException(nameof(shape)),
            };

            Assert.AreEqual(containsAnonymousType, UseConcreteTypeAnalyzer.ContainsAnonymousType(type));
        }

        public static IEnumerable<object[]> AnonymousTypeShapes
        {
            get
            {
                foreach (var shape in new[]
                {
                    "type", "array", "multidimensional array", "jagged array", "generic", "containing generic", "tuple",
                    "pointer", "pointer to pointer", "array of pointers", "function pointer return",
                    "function pointer ref return", "function pointer ref readonly return",
                    "function pointer parameter", "function pointer ref parameter",
                    "function pointer out parameter", "function pointer in parameter",
                    "function pointer ref readonly parameter", "function pointer later parameter",
                    "function pointer with pointer parameter", "nested function pointer",
                })
                {
                    yield return new object[] { shape, false };
                    yield return new object[] { shape, true };
                }
            }
        }

        [TestMethod]
        [DataRow("throw new System.Exception()")]
        [DataRow("condition ? new { Value = 1 } : throw new System.Exception()")]
        [DataRow("condition ? throw new System.Exception() : new { Value = 1 }")]
        [DataRow("new { Value = 1 } ?? throw new System.Exception()")]
        [DataRow("condition ? new C() : (condition ? new { Value = 1 } : throw new System.Exception())")]
        public async Task ShouldNotTrigger_ThrowExpressionWithoutNameableConcreteType_CSharp(string expression)
        {
            await TestCSAsync($$"""
                class C
                {
                    private object Build(bool condition) => {{expression}};
                }
                """);
        }

        [TestMethod]
        [DataRow("ref")]
        [DataRow("ref readonly")]
        public async Task MethodReturnFromByRefValue_CSharp(string refKind)
        {
            await TestCSAsync($$"""
                class C
                {
                    private object BuildAnonymous()
                    {
                        var value = new { Value = 1 };
                        return GetReference(ref value);
                    }

                    private object {|#0:BuildConcrete|}()
                    {
                        var value = new C();
                        return GetReference(ref value);
                    }

                    private static {{refKind}} T GetReference<T>(ref T value) => ref value;
                }
                """,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                    .WithLocation(0).WithArguments("BuildConcrete", "object", "C"));
        }

        [TestMethod]
        public async Task AssignmentsFromByRefValue_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Option Infer On

                Class C
                    Public Sub UseAnonymous()
                        Dim value = New With {.Value = 1}
                        Dim local As Object
                        local = GetValue(value)
                        local.ToString()
                    End Sub

                    Public Sub UseConcrete()
                        Dim value = New C()
                        Dim {|#0:local|} As Object
                        local = GetValue(value)
                        local.ToString()
                    End Sub

                    Private Shared Function GetValue(Of T)(ByRef value As T) As T
                        Return value
                    End Function
                End Class
                """,
                VerifyVB.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(0).WithArguments("local", "Object", "C"));
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_MixedAnonymousAndConcreteMethodReturns_CSharp()
        {
            await TestCSAsync("""
                class ConcreteType
                {
                    private object GetNewConcreteTypeInstance(bool returnAnonymousObjectInstead)
                    {
                        if (returnAnonymousObjectInstead)
                        {
                            return new { };
                        }

                        return new ConcreteType();
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(50366, "https://github.com/dotnet/sdk/issues/50366")]
        public async Task ShouldNotTrigger_AnonymousLocalMethodReturn_CSharp()
        {
            await TestCSAsync("""
                class C
                {
                    private object BuildObject()
                    {
                        var outputData = new
                        {
                            Name = "Alex"
                        };

                        return outputData;
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_NestedAnonymousMethodReturn_CSharp()
        {
            await TestCSAsync("""
                using System.Linq;

                class C
                {
                    private object BuildArray() => new[] { new { Name = "Alex" } };

                    private object BuildList() => new[] { new { Name = "Alex" } }.ToList();
                }
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_AnonymousContainingTypeMethodReturn_CSharp()
        {
            await TestCSAsync("""
                class C
                {
                    private object BuildObject() => Create(new { Name = "Alex" });

                    private static Outer<T>.Inner Create<T>(T value) => new();
                }

                class Outer<T>
                {
                    public class Inner
                    {
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_WhenAnotherReturnOperationWasNotExplicitlyHandled_CSharp()
        {
            await TestCSAsync("""
                record RecordType(int Value);

                class C
                {
                    private object BuildObject(bool returnRecord, RecordType record)
                    {
                        if (returnRecord)
                        {
                            return record with { Value = 1 };
                        }

                        return new C();
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_AnonymousAssignments_CSharp()
        {
            await TestCSAsync("""
                class C
                {
                    private object _field = new { Name = "Alex" };

                    public void M()
                    {
                        _field.ToString();

                        object local = new { Name = "Alex" };
                        local.ToString();
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_MixedAnonymousAndConcreteMethodReturns_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Class ConcreteType
                    Private Function GetNewConcreteTypeInstance(returnAnonymousObjectInstead As Boolean) As Object
                        If returnAnonymousObjectInstead Then
                            Return New With {.Name = "Alex"}
                        End If

                        Return New ConcreteType()
                    End Function
                End Class
                """);
        }

        [TestMethod]
        [WorkItem(50366, "https://github.com/dotnet/sdk/issues/50366")]
        public async Task ShouldNotTrigger_AnonymousLocalMethodReturn_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Class C
                    Private Function BuildObject() As Object
                        Dim outputData = New With {
                            .Name = "Alex"
                        }

                        Return outputData
                    End Function
                End Class
                """);
        }

        [TestMethod]
        [WorkItem(56233, "https://github.com/dotnet/sdk/issues/56233")]
        public async Task ShouldNotTrigger_NestedAnonymousMethodReturn_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Class C
                    Private Function BuildArray() As Object
                        Return {New With {.Name = "Alex"}}
                    End Function
                End Class
                """);
        }

        [TestMethod]
        [DynamicData(nameof(DisqualifiedSources))]
        public async Task Disqualication(string insert)
        {
            string source = $$"""
                                #pragma warning disable CS8019
                                #nullable enable

                                using System;
                                using System.Threading.Tasks;

                                namespace Example
                                {
                                    public interface IFoo
                                    {
                                        void Bar();
                                    }

                                    public class Foo : IFoo
                                    {
                                        public void Bar()
                                        {
                                        }
                                    }

                                    {{insert}}
                                }
                """;

            await TestCSAsync(source);
        }

        public static IEnumerable<object[]> DisqualifiedSources => new List<object[]>
        {
            new[]
            {
                """
                                    public class TestLocalFunction
                                    {
                                        private IFoo SyncMethod(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0: return MakeFoo();
                                                default: return new Foo();
                                            }

                                            static IFoo MakeFoo() => new Foo() as IFoo;
                                        }

                                        private async Task<IFoo> AsyncMethod(Task stuff, int x)
                                        {
                                            await stuff;

                                            switch (x)
                                            {
                                                case 0: return MakeFoo();
                                                default: return new Foo();
                                            }

                                            static IFoo MakeFoo() => new Foo() as IFoo;
                                        }

                                        private async Task<IFoo> TryCatchAsyncMethod(Task stuff, int x)
                                        {
                                            try
                                            {
                                                await stuff;
                                                return new Foo();
                                            }
                                            catch
                                            {
                                                return new Foo();
                                            }
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class TestArray
                                    {
                                        private readonly IFoo[] _foos = new IFoo[3];

                                        private IFoo Method(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0: return _foos[0];
                                                default: return new Foo();
                                            }
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class TestLambda
                                    {
                                        private IFoo MethodWithExpression(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0: return Stub(() => new Foo());
                                                default: return new Foo();
                                            }
                                        }

                                        private IFoo MethodWithBlock(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0:
                                                    return Stub(() =>
                                                    {
                                                        return new Foo();
                                                    });
                                                default: return new Foo();
                                            }
                                        }

                                        public IFoo Stub(Func<IFoo> func)
                                        {
                                            return func();
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class TestByRef
                                    {
                                        private IFoo MethodUsingByRef(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0:
                                                {
                                                    IFoo localRef = new Foo();
                                                    RefMethod(ref localRef);
                                                    return localRef;
                                                }

                                                case 1:
                                                {
                                                    OutMethod(out var localOut);
                                                    return localOut;
                                                }

                                                default:
                                                    return new Foo();
                                            }
                                        }

                                        public void RefMethod(ref IFoo foo)
                                        {
                                            foo = new Foo();
                                        }

                                        public void OutMethod(out IFoo foo)
                                        {
                                            foo = new Foo();
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class TestTuples
                                    {
                                        private IFoo MethodTuple(int x)
                                        {
                                            switch (x)
                                            {
                                                case 0:
                                                    var (l, m) = MakeTuple();
                                                    return l;

                                                default: return new Foo();
                                            }
                                        }

                                        public (IFoo, IFoo) MakeTuple()
                                        {
                                            return (new Foo(), new Foo());
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public interface IBase
                                    {
                                        IFoo Method();
                                    }

                                    public class TestInterfaceMethod : IBase
                                    {
                                        public IFoo Method()
                                        {
                                            return new Foo();
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class Base1
                                    {
                                        public virtual IFoo Method()
                                        {
                                            return new Foo();
                                        }
                                    }

                                    public class Base2 : Base1
                                    {
                                    }

                                    public class TestOverrideMethod : Base2
                                    {
                                        public override IFoo Method()
                                        {
                                            return new Foo();
                                        }
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public abstract class TestConstraints
                                    {
                                        public virtual IFoo VirtualMethod() =>new Foo();
                                        public abstract IFoo AbstractMethod();
                                        public IFoo PublicMethod() => new Foo();
                                        public IFoo InternalMethod() => new Foo();
                                    }
                                    
                    """,
            },

            new[]
            {
                """
                                    public class TestConflictingLocals
                                    {
                                        public void Test(int x)
                                        {
                                            IFoo l;

                                            switch (x)
                                            {
                                                case 0: l = new Foo(); break;
                                                case 1: l = MakeFoo(); break;
                                            }
                                        }

                                        public IFoo MakeFoo() => new Foo();
                                    }
                                    
                    """,
            },
        };
    }
}
