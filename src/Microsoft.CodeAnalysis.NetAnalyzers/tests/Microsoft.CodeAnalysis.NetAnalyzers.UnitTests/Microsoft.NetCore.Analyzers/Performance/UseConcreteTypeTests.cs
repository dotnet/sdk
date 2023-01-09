// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public static partial class UseConcreteTypeTests
    {
        [Fact]
        public static async Task ShouldNotTrigger1()
        {
            const string Source = @"
                #nullable enable

                using System;
                using System.Collections.Generic;

                namespace Example
                {
                    public class BaseType
                    {
                    }

                    public class Derived1 : BaseType
                    {
                    }
                
                    public class Derived2 : BaseType
                    {
                        private BaseType? Foo(int x)
                        {
                            if (x == 0) return null;
                            if (x == 1) return new Derived1();

                            return this;
                        }
                    }
                }";

            await TestCSAsync(Source);
        }

        [Fact]
        public static async Task ShouldNotTrigger2()
        {
            const string Source = @"
                #nullable enable

                using System.Collections.Generic;

                namespace System
                {
                    public static partial class MemoryExtensions
                    {
                        public static unsafe bool SequenceEqual<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> other, IEqualityComparer<T>? comparer = null)
                        {
                            comparer = EqualityComparer<T>.Default;
                            return comparer!.Equals(span[0], other[0]);
                        }

                        public static int CommonPrefixLength<T>(this Span<T> span, ReadOnlySpan<T> other, IEqualityComparer<T>? comparer)
                        {
                            return comparer!.Equals(span[0], other[0]) ? 0 : 1;
                        }

                        public static bool Foo()
                        {
                            Span<byte> s1 = stackalloc byte[2];
                            Span<byte> s2 = stackalloc byte[2];
                            return SequenceEqual(s1, s2, EqualityComparer<byte>.Default);
                        }

                        public static int Bar()
                        {
                            Span<byte> s1 = stackalloc byte[2];
                            Span<byte> s2 = stackalloc byte[2];
                            return CommonPrefixLength(s1, s2, EqualityComparer<byte>.Default);
                        }
                    }
                }";

            await TestCSAsync(Source);
        }

        [Fact]
        public static async Task ShouldNotTrigger3()
        {
            const string Source = @"
                #nullable enable

                using System;
                using System.IO;

                namespace Example
                {
                    internal static class C
                    {
                        private static Stream GetStream(int i)
                        {
                            if (i == 0)
                            {
                                return Stream.Null;
                            }

                            return new MyStream();
                        }
                    }
                }

                public class MyStream : MemoryStream { }
                ";

            await TestCSAsync(Source);
        }

        [Fact]
        public static async Task ShouldNotTrigger4()
        {
            const string Source = @"
                #nullable enable

                using System;
                using System.IO;

                namespace Example
                {
                    internal partial class C
                    {
                        private partial Stream GetStream(int i);
                    }

                    internal partial class C
                    {
                        private partial Stream GetStream(int i)
                        {
                            return new MyStream();
                        }
                    }
                }

                public class MyStream : MemoryStream { }
                ";

            await TestCSAsync(Source);
        }

        [Fact]
        public static async Task ShouldNotTrigger5()
        {
            const string Source = @"
                #nullable enable

                interface IFoo
                {
                    int M();
                }

                internal class C : IFoo
                {
                    int IFoo.M() => 42;
                }

                internal class Use
                {
                    static int Bar()
                    {
                        IFoo f = new C();
                        return f.M();
                    }
                }
                ";

            await TestCSAsync(Source);
        }

        [Fact]
        public static async Task ShouldTrigger1()
        {
            const string Source = @"
                namespace Example
                {
                    public interface IFoo<T>
                    {
                        void Bar();
                    }

                    public class Foo<T> : IFoo<T>
                    {
                        public void Bar() { }
                    }

                    public static class Tester
                    {
                        private static void Do<T>(IFoo<T> {|#0:foo|})
                        {
                            foo.Bar();
                        }

                        private static void MakeCall()
                        {
                            Do<int>(new Foo<int>());
                        }
                     }
                }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForParameter)
                        .WithLocation(0)
                        .WithArguments("foo", "Example.IFoo<T>", "Example.Foo<int>"));
        }

        [Fact]
        public static async Task ShouldTrigger2()
        {
            const string Source = @"
                namespace Example
                {
                    public interface IFoo<T>
                    {
                        void Bar();
                    }

                    public class Foo<T> : IFoo<T>
                    {
                        public void Bar() { }
                    }

                    public static class Tester
                    {
                        private static void Do<T>(IFoo<T> {|#0:foo|})
                        {
                            foo.Bar();
                        }

                        private static void MakeCall<T>()
                        {
                            Do<T>(new Foo<T>());
                        }
                     }
                }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForParameter)
                        .WithLocation(0)
                        .WithArguments("foo", "Example.IFoo<T>", "Example.Foo<T>"));
        }

        [Fact]
        public static async Task ShouldTrigger3()
        {
            const string Source = @"
                #nullable enable

                using System;
                using System.IO;

                namespace Example
                {
                    internal class C
                    {
                        private MemoryStream? _stream;

                        private Stream {|#0:GetStream|}()
                        {
                            return _stream ?? Create();
                        }

                        private MemoryStream Create() => new MemoryStream();
                    }
                }
                ";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                        .WithLocation(0)
                        .WithArguments("GetStream", "System.IO.Stream", "System.IO.MemoryStream"));
        }

        [Fact]
        public static async Task ShouldTrigger4()
        {
            const string Source = @"
                #nullable enable

                using System;
                using System.IO;

                namespace Example
                {
                    internal class C
                    {
                        private MemoryStream? _stream;

                        private Stream? {|#0:GetStream|}()
                        {
                            return _stream ?? Create();
                        }

                        private MemoryStream? Create() => new MemoryStream();
                    }
                }
                ";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                        .WithLocation(0)
                        .WithArguments("GetStream", "System.IO.Stream?", "System.IO.MemoryStream?"));
        }

        [Fact]
        public static async Task Params()
        {
            const string Source = @"
                namespace Example
                {
                    public interface IFoo
                    {
                        void Bar();
                    }

                    public class Foo : IFoo
                    {
                        public void Bar() {}
                    }

                    public class Test
                    {
                        private void Method(IFoo {|#0:foo|})
                        {
                            foo.Bar();
                        }

                        private void Method2(IFoo foo)
                        {
                            foo.Bar();
                        }

                        private void Caller(IFoo ifoo)
                        {
                            Method(new Foo());
                            Method2(new Foo());
                            Method2(ifoo);
                        }
                    }
                }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForParameter)
                        .WithLocation(0)
                        .WithArguments("foo", "Example.IFoo", "Example.Foo"));
        }

        [Fact]
        public static async Task Conditional()
        {
            const string Source = @"
#nullable enable
            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class FooProvider
                {
                    public Foo Foo { get { return new Foo(); } }
                }

                public class AttributeData
                {
                    public SyntaxReference? ApplicationReference { get; }
                }

                public abstract class SyntaxReference
                {
                    public abstract SyntaxNode GetSyntax();
                }

                public abstract class SyntaxNode
                {
                    public Location GetLocation() => new();
                }

                public class Location
                {
                }

                public class Test
                {
                    private IFoo? {|#0:Method1|}(FooProvider? provider)
                    {
                        return provider?.Foo;
                    }

                    private void Method2()
                    {
                        AttributeData attr = new();
                        _ = attr.ApplicationReference?.GetSyntax().GetLocation();
                    }
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                        .WithLocation(0)
                        .WithArguments("Method1", "Example.IFoo?", "Example.Foo?"));
        }

        [Fact]
        public static async Task Tuples()
        {
            const string Source = @"
            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class Test
                {
                    private IFoo {|#0:MethodTuple|}(int x)
                    {
                        switch (x)
                        {
                            case 0:
                            {
                                Foo l; IFoo m;
                                (l, m) = MakeTuple();
                                return l;
                            }

                            case 1:
                            {
                                var (l, m) = MakeTuple();
                                return l;
                            }

                            default: return new Foo();
                        }
                    }

                    public (Foo, IFoo) MakeTuple()
                    {
                        return (new Foo(), new Foo());
                    }
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                        .WithLocation(0)
                        .WithArguments("MethodTuple", "Example.IFoo", "Example.Foo"));
        }

        [Fact]
        public static async Task Locals()
        {
            const string Source = @"
#nullable enable
            using System;

            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class Test
                {
                    private static readonly Foo _fooField = new();
                    private Foo FooMethod() => new Foo();
                    private void FooRefMethod(ref Foo x) { }
                    private void FooOutMethod(out Foo x) { x = new Foo(); }
                    private Foo FooProp { get { return _fooField; } }

                    private static readonly IFoo _ifooField = (IFoo)new Foo();
                    private IFoo IFooMethod() => _ifooField;
                    private void IFooRefMethod(ref IFoo x) { }
                    private void IFooOutMethod(out IFoo x) { x = new Foo(); }
                    private IFoo IFooProp { get { return _ifooField; } }

                    public void Method(int x, Foo fooParam, IFoo ifooParam)
                    {
                        Foo fooLocal = new Foo();
                        Foo[] fooArray = new Foo[0];
                        Func<Foo> fooDelegate = FooMethod;

                        IFoo ifooLocal = new Foo();
                        IFoo[] ifooArray = new IFoo[0];
                        Func<IFoo> ifooDelegate = IFooMethod;

                        IFoo? {|#0:l0|} = null;
                        IFoo? l1 = new Foo();
                        IFoo? l2 = new Foo();
                        IFoo? l3 = new Foo();
                        IFoo? l4 = new Foo();
                        IFoo? l5 = new Foo();
                        IFoo? l6 = new Foo();
                        IFoo? l7 = new Foo();
                        IFoo? l8 = new Foo();
                        IFoo? l9 = new Foo();

                        switch (x)
                        {
                            case 0: l0 = new Foo(); break;
                            case 1: l0 = null; break;
                            case 2: l0 = null!; break;
                            case 3: l0 = _fooField; break;
                            case 4: l0 = fooArray[0]; break;
                            case 5: l0 = FooProp; break;
                            case 6: l0 = fooLocal; break;
                            case 7: l0 = fooParam; break;
                            case 8: l0 = FooMethod(); break;
                            case 9: l0 = fooDelegate(); break;
                        }

                        l1 = ifooLocal;
                        l2 = _ifooField;
                        l3 = ifooArray[0];
                        l4 = IFooProp;
                        l5 = IFooMethod();
                        l6 = ifooParam;
                        l7 = ifooDelegate();
                        IFooRefMethod(ref l8);
                        IFooOutMethod(out l9);

                        // induce virtual calls to trigger the diags
                        l0?.Bar();
                        l1?.Bar();
                        l2?.Bar();
                        l3?.Bar();
                        l4?.Bar();
                        l5?.Bar();
                        l6?.Bar();
                        l7?.Bar();
                        l8?.Bar();
                        l9?.Bar();
                    }
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                        .WithLocation(0)
                        .WithArguments("l0", "Example.IFoo?", "Example.Foo?"));
        }

        [Fact]
        public static async Task Complex()
        {
            const string Source = @"
#pragma warning disable CS0619

            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class Test
                {
                    IFoo {|#0:f0|} = MakeFoo();
                    IFoo {|#1:f1|} = new Foo();
                    IFoo {|#2:f2|};
                    IFoo {|#3:f3|};
                    IFoo {|#4:f4|};
                    IFoo {|#5:f5|};
                    IFoo {|#6:f6|};

                    public Test(int x)
                    {
                        f2 = MakeFoo();
                        f3 = (x == 0) ? new Foo() : MakeFoo();
                        f4 = new Foo();
                        f5 ??= MakeFoo();
                        f6 = MakeFoo() ?? MakeFoo();
                    }

                    public void M(int x)
                    {
                        IFoo {|#7:l0|} = MakeFoo();
                        IFoo {|#8:l1|} = (x == 0) ? new Foo() : MakeFoo();
                        IFoo {|#9:l2|} = new Foo();
                        IFoo {|#10:l3|}; l3 = MakeFoo();
                        IFoo {|#11:l4|}; l4 = (x == 0) ? new Foo() : MakeFoo();
                        IFoo {|#12:l5|}; l5 = new Foo();
                        IFoo {|#13:l6|}; l6 = null; l6 ??= MakeFoo();
                        IFoo {|#14:l7|}; l7 = MakeFoo() ?? MakeFoo();

                        // make virtual calls so the analyzer will trigger
                        l0.Bar();
                        l1.Bar();
                        l2.Bar();
                        l3.Bar();
                        l4.Bar();
                        l5.Bar();
                        l6.Bar();
                        l7.Bar();
                        f0.Bar();
                        f1.Bar();
                        f2.Bar();
                        f3.Bar();
                        f4.Bar();
                        f5.Bar();
                        f6.Bar();
                    }

                    static Foo MakeFoo() => new Foo();
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(0)
                    .WithArguments("f0", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(1)
                    .WithArguments("f1", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(2)
                    .WithArguments("f2", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(3)
                    .WithArguments("f3", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(4)
                    .WithArguments("f4", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(5)
                    .WithArguments("f5", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                    .WithLocation(6)
                    .WithArguments("f6", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(7)
                    .WithArguments("l0", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(8)
                    .WithArguments("l1", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(9)
                    .WithArguments("l2", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(10)
                    .WithArguments("l3", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(11)
                    .WithArguments("l4", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(12)
                    .WithArguments("l5", "Example.IFoo", "Example.Foo"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(13)
                    .WithArguments("l6", "Example.IFoo", "Example.Foo?"),

                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForLocal)
                    .WithLocation(14)
                    .WithArguments("l7", "Example.IFoo", "Example.Foo"));
        }

        [Fact]
        public static async Task Fields()
        {
            const string Source = @"
#nullable enable
            using System;

            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class Test
                {
                    private static readonly Foo _fooField = new();
                    private Foo FooMethod() => new Foo();
                    private void FooRefMethod(ref Foo x) { }
                    private void FooOutMethod(out Foo x) { x = new Foo(); }
                    private Foo FooProp { get { return _fooField; } }

                    private static readonly IFoo _ifooField = (IFoo)new Foo();
                    private IFoo IFooMethod() => _ifooField;
                    private void IFooRefMethod(ref IFoo x) { }
                    private void IFooOutMethod(out IFoo x) { x = new Foo(); }
                    private IFoo IFooProp { get { return _ifooField; } }

                    private IFoo? {|#0:l0|} = null;
                    private IFoo? l1 = new Foo();
                    private IFoo? l2 = new Foo();
                    private IFoo? l3 = new Foo();
                    private IFoo? l4 = new Foo();
                    private IFoo? l5 = new Foo();
                    private IFoo? l6 = new Foo();
                    private IFoo? l7 = new Foo();
                    private IFoo? l8 = new Foo();
                    private IFoo? l9 = new Foo();
                    public IFoo? l10 = new Foo();
                    internal IFoo? l11 = new Foo();

                    public void Method(int x, Foo fooParam, IFoo ifooParam)
                    {
                        Foo fooLocal = new Foo();
                        Foo[] fooArray = new Foo[0];
                        Func<Foo> fooDelegate = FooMethod;

                        IFoo ifooLocal = new Foo();
                        IFoo[] ifooArray = new IFoo[0];
                        Func<IFoo> ifooDelegate = IFooMethod;

                        switch (x)
                        {
                            case 0: l0 = new Foo(); break;
                            case 1: l0 = null; break;
                            case 2: l0 = null!; break;
                            case 3: l0 = _fooField; break;
                            case 4: l0 = fooArray[0]; break;
                            case 5: l0 = FooProp; break;
                            case 6: l0 = fooLocal; break;
                            case 7: l0 = fooParam; break;
                            case 8: l0 = FooMethod(); break;
                            case 9: l0 = fooDelegate(); break;
                        }

                        l1 = ifooLocal;
                        l2 = _ifooField;
                        l3 = ifooArray[0];
                        l4 = IFooProp;
                        l5 = IFooMethod();
                        l6 = ifooParam;
                        l7 = ifooDelegate();
                        IFooRefMethod(ref l8!);
                        IFooOutMethod(out l9);

                        // induce virtual calls to trigger the diags
                        l0?.Bar();
                        l1?.Bar();
                        l2?.Bar();
                        l3?.Bar();
                        l4?.Bar();
                        l5?.Bar();
                        l6?.Bar();
                        l7?.Bar();
                        l8?.Bar();
                        l9?.Bar();
                        l10?.Bar();
                        l11?.Bar();
                    }
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForField)
                        .WithLocation(0)
                        .WithArguments("l0", "Example.IFoo?", "Example.Foo?"));
        }

        [Fact]
        public static async Task Methods()
        {
            const string Source = @"
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
                    public void Bar() {}
                }

                public interface I4
                {
                    IFoo M4();
                }

                public class Test : I4
                {
                    public IFoo M1() => new Foo();
                    internal IFoo M2() => new Foo();
                    private IFoo {|#0:M3|}() => new Foo();
                    IFoo I4.M4() => new Foo();
                    private static IFoo M5() => new Foo();
                    private IFoo M6() => new Foo();

                    private async Task<string> M7(Task stuff)
                    {
                        await stuff;
                        return ""Hello"";
                    }

                    private async Task<IFoo> M8(Task stuff)
                    {
                        await stuff;
                        return (IFoo)new Foo();
                    }

                    private static Func<IFoo> _func = M5;

                    private void Trigger() => Dispatch(M6);
                    private void Dispatch(Func<IFoo> func) => func();
                }
            }";

            await TestCSAsync(Source,
                VerifyCS.Diagnostic(UseConcreteTypeAnalyzer.UseConcreteTypeForMethodReturn)
                        .WithLocation(0)
                        .WithArguments("M3", "Example.IFoo", "Example.Foo"));
        }

        [Fact]
        public static async Task OutParams()
        {
            const string Source = @"
            namespace Example
            {
                public interface IFoo
                {
                    void Bar();
                }

                public class Foo : IFoo
                {
                    public void Bar() {}
                }

                public class Test
                {
                    public void M1()
                    {
                        if (!GetIFoo(out var f))
                        {
                            f = new Foo();
                        }

                        f.Bar();
                    }

                    public bool GetIFoo(out IFoo ifoo)
                    {
                        ifoo = new Foo();
                        return true;
                    }
                }
            }";

            await TestCSAsync(Source);
        }

        private static async Task TestCSAsync(string source, params DiagnosticResult[] diagnosticResults)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
            };
            test.ExpectedDiagnostics.AddRange(diagnosticResults);
            await test.RunAsync();
        }
    }
}
