// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.StaticHolderTypesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpStaticHolderTypesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class StaticHolderTypeFixerTests
    {
        [Fact]
        public async Task CA1052FixesNonStaticClassWithOnlyStaticDeclaredMembersCSharp()
        {
            const string Code = @"
public class [|C|]
{
    public static void SomeMethod() { }
}
";

            const string FixedCode = @"
public static class C
{
    public static void SomeMethod() { }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }

        [Fact]
        public async Task CA1052FixesNonStaticClassWithPublicDefaultConstructorAndStaticMethodCSharp()
        {
            const string Code = @"
public class [|C|]
{
    public C() { }
    public static void SomeMethod() { }
}
";

            const string FixedCode = @"
public static class C
{
    public static void SomeMethod() { }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }

        [Fact]
        public async Task CA1052FixesNonStaticClassWithProtectedDefaultConstructorAndStaticMethodCSharp()
        {
            const string Code = @"
public class [|C|]
{
    protected C() { }
    public static void SomeMethod() { }
}
";

            const string FixedCode = @"
public static class C
{
    public static void SomeMethod() { }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }

        [Fact]
        public async Task CA1052FixesNonStaticClassWithPrivateDefaultConstructorAndStaticMethodCSharp()
        {
            const string Code = @"
public class [|C|]
{
    private C() { }
    public static void SomeMethod() { }
}
";

            const string FixedCode = @"
public static class C
{
    public static void SomeMethod() { }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }

        [Fact]
        public async Task CA1052FixesNestedPublicNonStaticClassWithPublicDefaultConstructorAndStaticMethodCSharp()
        {
            const string Code = @"
public class C
{
    public void Moo() { }

    public class [|CInner|]
    {
        public CInner() { }
        public static void SomeMethod() { }
    }
}
";

            const string FixedCode = @"
public class C
{
    public void Moo() { }

    public static class CInner
    {
        public static void SomeMethod() { }
    }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }

        [Fact]
        public async Task CA1052FixesNestedPublicClassInOtherwiseEmptyNonStaticClassCSharp()
        {
            const string Code = @"
public class [|C|]
{
    public class CInner
    {
    }
}
";

            const string FixedCode = @"
public static class C
{
    public class CInner
    {
    }
}
";

            await VerifyCS.VerifyCodeFixAsync(Code, FixedCode);
        }
    }
}
