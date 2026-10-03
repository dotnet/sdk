// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public partial class UseConcreteTypeTests
    {
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
