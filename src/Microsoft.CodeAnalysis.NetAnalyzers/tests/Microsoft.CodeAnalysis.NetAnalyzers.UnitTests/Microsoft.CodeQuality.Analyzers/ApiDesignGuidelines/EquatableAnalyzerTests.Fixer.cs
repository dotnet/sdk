// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EquatableFixerTests
    {
        [Fact]
        public async Task CodeFixForStructWithEqualsOverrideButNoIEquatableImplementation()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

struct {|CA1066:S|}
{
    public override bool Equals(object other)
    {
        return true;
    }

    public override int GetHashCode() => 0;
}
", @"
using System;

struct S : IEquatable<S>
{
    public override bool Equals(object other)
    {
        return true;
    }

    public override int GetHashCode() => 0;

    public bool Equals(S other)
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CodeFixForStructWithIEquatableImplementationButNoEqualsOverride()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

struct {|CA1067:S|} : IEquatable<S>
{
    public bool Equals(S other)
    {
        return true;
    }
}
", @"
using System;

struct S : IEquatable<S>
{
    public bool Equals(S other)
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is S && Equals((S)obj);
    }
}
");
        }

        [Fact]
        public async Task CodeFixForClassWithIEquatableImplementationButNoEqualsOverride()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class {|CA1067:C|} : IEquatable<C>
{
    public bool Equals(C other)
    {
        return true;
    }
}
", @"
using System;

class C : IEquatable<C>
{
    public bool Equals(C other)
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as C);
    }
}
");
        }

        [Fact]
        public async Task CodeFixForClassWithExplicitIEquatableImplementationAndNoEqualsOverride()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class {|CA1067:C|} : IEquatable<C>
{
    bool IEquatable<C>.Equals(C other)
    {
        return true;
    }
}
", @"
using System;

class C : IEquatable<C>
{
    bool IEquatable<C>.Equals(C other)
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        return ((IEquatable<C>)this).Equals(obj as C);
    }
}
");
        }

        [Fact]
        public async Task CodeFixForStructWithExplicitIEquatableImplementationAndNoEqualsOverride()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

struct {|CA1067:S|} : IEquatable<S>
{
    bool IEquatable<S>.Equals(S other)
    {
        return true;
    }
}
", @"
using System;

struct S : IEquatable<S>
{
    bool IEquatable<S>.Equals(S other)
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is S && ((IEquatable<S>)this).Equals((S)obj);
    }
}
");
        }
    }
}