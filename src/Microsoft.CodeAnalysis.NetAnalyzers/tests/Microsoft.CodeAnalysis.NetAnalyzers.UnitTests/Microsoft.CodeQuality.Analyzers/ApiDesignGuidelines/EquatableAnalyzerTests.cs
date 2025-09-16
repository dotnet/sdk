// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EquatableAnalyzerTests
    {
        [Fact]
        public async Task NoDiagnosticForStructWithNoEqualsOverrideAndNoIEquatableImplementationAsync()
        {
            var code = @"
struct S
{
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithNoEqualsOverrideAndNoIEquatableImplementationAsync()
        {
            var code = @"
class C
{
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DiagnosticForStructWithEqualsOverrideButNoIEquatableImplementationAsync()
        {
            var code = @"
struct S
{
    public override bool Equals(object other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(2, 8, EquatableAnalyzer.ImplementIEquatableDescriptor, "S"));
        }

        [Fact]
        public async Task NoDiagnosticForClassWithEqualsOverrideAndNoIEquatableImplementationAsync()
        {
            var code = @"
class C
{
    public override bool Equals(object other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DiagnosticForStructWithIEquatableImplementationButNoEqualsOverrideAsync()
        {
            var code = @"
using System;

struct S : IEquatable<S>
{
    public bool Equals(S other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(4, 8, EquatableAnalyzer.OverridesObjectEqualsDescriptor, "S"));
        }

        [Fact]
        public async Task DiagnosticForClassWithIEquatableImplementationButNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : IEquatable<C>
{
    public bool Equals(C other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(4, 7, EquatableAnalyzer.OverridesObjectEqualsDescriptor, "C"));
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithNoParameterListAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public bool {|CS0548:Equals|}
    {
        {|CS1014:return|} true{|CS1014:;|}
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithMalformedParameterListAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public bool Equals({|CS1026:|}
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithMalformedParameterListAndNoEqualsOverride2Async()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public bool Equals{|CS1003:)|}
    {
        return true;
    }
{|CS1022:}|}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithNoParametersAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public bool Equals()
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithMalformedParameterDeclarationAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public bool Equals({|CS0246:x|}{|CS1001:)|}
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithWrongReturnTypeAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0738:IEquatable<C>|}
{
    public int Equals(C x)
    {
        return 1;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DiagnosticForClassWithIEquatableImplementationWithNoBodyAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : IEquatable<C>
{
    public bool {|CS0501:Equals|}(C other){|CS1002:|}
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(4, 7, EquatableAnalyzer.OverridesObjectEqualsDescriptor, "C"));
        }

        [Fact]
        public async Task NoDiagnosticForClassWithIEquatableImplementationWithNoReturnTypeAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : {|CS0535:IEquatable<C>|}
{
    public {|CS1520:Equals|}(C other)
    {
        {|CS0127:return|} true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task NoDiagnosticForClassWithEqualsOverrideWithWrongSignatureAndNoIEquatableImplementationAsync()
        {
            var code = @"
using System;

class C
{
    public override bool {|CS0115:Equals|}(object other, int n)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DiagnosticForClassWithExplicitIEquatableImplementationAndNoEqualsOverrideAsync()
        {
            var code = @"
using System;

class C : IEquatable<C>
{
    bool IEquatable<C>.Equals(C other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(4, 7, EquatableAnalyzer.OverridesObjectEqualsDescriptor, "C"));
        }

        [Fact]
        public async Task DiagnosticForDerivedStructWithEqualsOverrideAndNoIEquatableImplementationAsync()
        {
            var code = @"
using System;

struct B
{
    public override bool Equals(object other)
    {
        return false;
    }
}

struct C : {|CS0527:B|}
{
    public override bool Equals(object other)
    {
        return true;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(4, 8, EquatableAnalyzer.ImplementIEquatableDescriptor, "B"),
                // Test0.cs(12,8): warning CA1066: Implement IEquatable when overriding Object.Equals
                GetCSharpResultAt(12, 8, EquatableAnalyzer.ImplementIEquatableDescriptor, "C"));
        }

        [Fact, WorkItem(1914, "https://github.com/dotnet/roslyn-analyzers/issues/1914")]
        public async Task NoDiagnosticForParentClassWithIEquatableImplementationAsync()
        {
            var code = @"
using System;

public interface IValueObject<T> : IEquatable<T> { }

public struct S : IValueObject<S>
{
    private readonly int value;

    public override bool Equals(object obj) => obj is S other && Equals(other);

    public bool Equals(S other) => value == other.value;

    public override int GetHashCode() => value;
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(2027, "https://github.com/dotnet/roslyn-analyzers/issues/2027")]
        public async Task NoDiagnosticForDerivedTypesWithBaseTypeWithIEquatableImplementation_01Async()
        {
            var code = @"
using System;

public class A<T> : IEquatable<T>
    where T : A<T>
{
    public virtual bool Equals(T other) => false;

    public override bool Equals(object obj) => Equals(obj as T);
}

public class B : A<B>
{
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(2027, "https://github.com/dotnet/roslyn-analyzers/issues/2027")]
        public async Task NoDiagnosticForDerivedTypesWithBaseTypeWithIEquatableImplementation_02Async()
        {
            var code = @"
using System;

public class A<T> : IEquatable<T>
    where T: class
{
    public virtual bool Equals(T other) => false;

    public override bool Equals(object obj) => Equals(obj as T);
}

public class B : A<B>
{
}

public class C<T> : A<T>
    where T : class
{
}

public class D : C<D>
{
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(2324, "https://github.com/dotnet/roslyn-analyzers/issues/2324")]
        public async Task CA1066_CSharp_RefStruct_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public ref struct S
{
    public override bool Equals(object other)
    {
        return false;
    }
}
",
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, string typeName)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(typeName);
    }
}
