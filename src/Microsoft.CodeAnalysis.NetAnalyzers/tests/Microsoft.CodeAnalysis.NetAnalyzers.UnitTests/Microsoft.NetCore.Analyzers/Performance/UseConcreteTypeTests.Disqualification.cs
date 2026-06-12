// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public static partial class UseConcreteTypeTests
    {
        [Theory]
        [MemberData(nameof(DisqualifiedSources))]
        public static async Task Disqualication(string insert)
        {
            string source = @$"
                #pragma warning disable CS8019
                #nullable enable

                using System;
                using System.Threading.Tasks;

                namespace Example
                {{
                    public interface IFoo
                    {{
                        void Bar();
                    }}

                    public class Foo : IFoo
                    {{
                        public void Bar()
                        {{
                        }}
                    }}

                    {insert}
                }}";

            await TestCSAsync(source);
        }

        public static IEnumerable<object[]> DisqualifiedSources => new List<object[]>
        {
            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
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
                ",
            },

            new[]
            {
                @"
                public abstract class TestConstraints
                {
                    public virtual IFoo VirtualMethod() =>new Foo();
                    public abstract IFoo AbstractMethod();
                    public IFoo PublicMethod() => new Foo();
                    public IFoo InternalMethod() => new Foo();
                }
                ",
            },

            new[]
            {
                @"
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
                ",
            },
        };
    }
}
