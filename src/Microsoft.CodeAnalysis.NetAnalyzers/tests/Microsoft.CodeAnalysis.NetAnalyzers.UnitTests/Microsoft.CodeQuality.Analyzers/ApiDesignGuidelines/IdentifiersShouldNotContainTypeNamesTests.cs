// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotContainTypeNames,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldNotContainTypeNamesTests
    {
        [Fact]
        public async Task CSharp_CA1720_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class IntA
{
}
");
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Int
{
}
",
    GetCA1720CSharpResultAt(line: 2, column: 14, identifierName: "Int"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_CA1720_Internal_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class Int
{
}

public class C
{
    private class Int
    {
    }
}

internal class C2
{
    public class Int
    {
    }
}
");
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct Int32
{
}
",
    GetCA1720CSharpResultAt(line: 2, column: 15, identifierName: "Int32"));
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic3()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum Int64
{
}
",
    GetCA1720CSharpResultAt(line: 2, column: 13, identifierName: "Int64"));
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic4()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Derived
{
   public void Int()
   {
   }
}
",
    GetCA1720CSharpResultAt(line: 4, column: 16, identifierName: "Int"));
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic5()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
   public void SomeMethod(int Int)
   {
   }
}
",
    GetCA1720CSharpResultAt(line: 4, column: 31, identifierName: "Int"));
        }

        [Fact]
        public async Task CSharp_CA1720_SomeDiagnostic6()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class DerivedClass
{
   public int Int;
}
",
    GetCA1720CSharpResultAt(line: 4, column: 15, identifierName: "Int"));
        }

        [Fact]
        public async Task CSharp_CA1720_NoDiagnosticOnEqualsOverride()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
   public override bool Equals(object obj)
   {
        throw new System.NotImplementedException();
   }
}
");
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnAbstractBaseNotImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public abstract class Base
{
    public abstract void BaseMethod(object okay, object @object);
    public abstract int this[Guid guid] { get; }
}

public class Derived : Base
{
    public override void BaseMethod(object okay, object @object)
    {
    }

    public override int this[Guid guid]
    {
        get { return 0; }
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 57, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 7, column: 35, identifierName: "guid"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnBaseNotImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Base
{
    public virtual void BaseMethod(object okay, object @object) 
    { 
    }

    public virtual int this[Guid guid]
    { 
        get { return 0; }
    }
}

public class Derived : Base
{
    public override void BaseMethod(object okay, object @object) 
    { 
    }

    public override int this[Guid guid]
    {
        get { return 1; }
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 56, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 10, column: 34, identifierName: "guid"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnBaseNotNestedImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Base
{
    public virtual void BaseMethod(object okay, object @object)
    {
    }
}

public class Derived : Base
{
}

public class SomeClass : Derived
{
    public override void BaseMethod(object okay, object @object)
    {
    }
}",
    GetCA1720CSharpResultAt(line: 4, column: 56, identifierName: "object"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnInterfaceNotImplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived
{
    void DerivedMethod(object okay, object @object);
}

public class Derived : IDerived
{
    public void DerivedMethod(object okay, object @object) 
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 44, identifierName: "object"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnInterfaceNotExplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived
{
    void DerivedMethod(object okay, object @object);
}

public class Derived : IDerived
{
    void IDerived.DerivedMethod(object okay, object @object) 
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 44, identifierName: "object"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnGenericInterfaceNotImplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived<in T1, in T2>
{
    void DerivedMethod(int okay, T1 @object, T2 @int);
}

public class Derived : IDerived<int, string>
{
    public void DerivedMethod(int okay, int @object, string @int)
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 37, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 6, column: 49, identifierName: "int"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnGenericInterfaceNotExplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived<in T1, in T2>
{
    void DerivedMethod(int okay, T1 @object, T2 @int);
}

public class Derived : IDerived<int, string>
{
    void IDerived<int, string>.DerivedMethod(int okay, int @object, string @int)
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 37, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 6, column: 49, identifierName: "int"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnInterfaceNotNestedImplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived
{
    void DerivedMethod(object okay, object @object);
}

public interface IMyInterface : IDerived
{
}

public class Derived : IMyInterface
{
    public void DerivedMethod(object okay, object @object) 
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 44, identifierName: "object"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnInterfaceNotNestedExplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived
{
    void DerivedMethod(object okay, object @object);
}

public interface IMyInterface : IDerived
{
}

public class Derived : IMyInterface
{
    void IDerived.DerivedMethod(object okay, object @object) 
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 44, identifierName: "object"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnGenericInterfaceNotNestedImplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived<in T1, in T2>
{
    void DerivedMethod(int okay, T1 @object, T2 @int);
}

public interface IMyInterface<in T1, in T2> : IDerived<T1, T2>
{
}

public class Derived : IMyInterface<int, string>
{
    public void DerivedMethod(int okay, int @object, string @int)
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 37, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 6, column: 49, identifierName: "int"));
        }

        [Fact]
        public async Task CSharp_CA1720_DiagnosticOnGenericInterfaceNotNestedExplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IDerived<in T1, in T2>
{
    void DerivedMethod(int okay, T1 @object, T2 @int);
}

public interface IMyInterface<in T1, in T2> : IDerived<T1, T2>
{
}

public class Derived : IMyInterface<int, string>
{
    void IDerived<int, string>.DerivedMethod(int okay, int @object, string @int)
    {
    }
}",
    GetCA1720CSharpResultAt(line: 6, column: 37, identifierName: "object"),
    GetCA1720CSharpResultAt(line: 6, column: 49, identifierName: "int"));
        }

        [Fact]
        public async Task CSharp_CA1720_NoDiagnosticOnIEqualityComparerGetHashCodeImplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public sealed class SomeEqualityComparer : IEqualityComparer<string>, IEqualityComparer<int>
{
    public bool Equals(string x, string y) { throw new NotImplementedException(); }

    public bool Equals(int x, int y) { throw new NotImplementedException(); }

    public int GetHashCode(string obj)
    {
        throw new NotImplementedException();
    }

    public int GetHashCode(int obj)
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CSharp_CA1720_NoDiagnosticOnIEqualityComparerGetHashCodeExplicitImplementation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public sealed class SomeEqualityComparer : IEqualityComparer<string>, IEqualityComparer<int>
{
    public bool Equals(string x, string y) { throw new NotImplementedException(); }

    public bool Equals(int x, int y) { throw new NotImplementedException(); }

    int IEqualityComparer<string>.GetHashCode(string obj)
    {
        throw new NotImplementedException();
    }

    int IEqualityComparer<int>.GetHashCode(int obj)
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact, WorkItem(1823, "https://github.com/dotnet/roslyn-analyzers/issues/1823")]
        public async Task CA1720_ObjIdentifier_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void MyMethod(object obj) {}
}");
        }

        [Fact, WorkItem(4052, "https://github.com/dotnet/roslyn-analyzers/issues/4052")]
        public async Task CA1720_TopLevelStatements_NoDiagnostic()
        {
            await new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { @"int x = 0;" },
                    OutputKind = OutputKind.ConsoleApplication,
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            }.RunAsync();
        }

        #region Helpers

        private static DiagnosticResult GetCA1720CSharpResultAt(int line, int column, string identifierName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(identifierName);

        #endregion
    }
}