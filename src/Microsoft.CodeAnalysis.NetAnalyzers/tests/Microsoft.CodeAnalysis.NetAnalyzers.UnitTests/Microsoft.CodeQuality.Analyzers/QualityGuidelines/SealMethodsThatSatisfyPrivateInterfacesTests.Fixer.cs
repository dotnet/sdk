// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.SealMethodsThatSatisfyPrivateInterfacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class SealMethodsThatSatisfyPrivateInterfacesFixerTests
    {
        [Fact]
        public async Task TestCSharp_OverriddenMethodChangedToSealedAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public class C : B, IFace
{
    public override void [|M|]()
    {
    }
}",

@"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public class C : B, IFace
{
    public sealed override void M()
    {
    }
}");
        }

        [Fact]
        public async Task TestCSharp_VirtualMethodChangedToNotVirtualAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"internal interface IFace
{
    void M();
}

public class C : IFace
{
    public virtual void [|M|]()
    {
    }
}",

@"internal interface IFace
{
    void M();
}

public class C : IFace
{
    public void M()
    {
    }
}");
        }

        [Fact]
        public async Task TestCSharp_AbstractMethodChangedToNotAbstractAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"internal interface IFace
{
    void M();
}

public abstract class C : IFace
{
    public abstract void [|M|]();
}",

@"internal interface IFace
{
    void M();
}

public abstract class C : IFace
{
    public void M()
    {
    }
}");
        }

        [Fact]
        public async Task TestCSharp_ContainingTypeChangedToSealedAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public class C : B, IFace
{
    public override void [|M|]()
    {
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public sealed class C : B, IFace
{
    public override void M()
    {
    }
}",
                    },
                },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = "MakeDeclaringTypeSealed",
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_ContainingTypeChangedToInternalAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public class C : B, IFace
{
    public override void [|M|]()
    {
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

internal class C : B, IFace
{
    public override void M()
    {
    }
}",
                    },
                },
                CodeActionIndex = 2,
                CodeActionEquivalenceKey = "MakeDeclaringTypeInternal",
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_AbstractContainingTypeChangedToInternalAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public abstract class C : B, IFace
{
    public override void [|M|]()
    {
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

internal abstract class C : B, IFace
{
    public override void M()
    {
    }
}",
                    },
                },
                CodeActionIndex = 1, // sealed option is not available because class is abstract
                CodeActionEquivalenceKey = "MakeDeclaringTypeInternal",
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_ImplicitOverride_ContainingTypeChangedToSealedAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"internal interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

public class [|C|] : B, IFace
{
}",

@"internal interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

public sealed class C : B, IFace
{
}");
        }

        [Fact]
        public async Task TestCSharp_ImplicitOverride_ContainingTypeChangedToInternalAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

public class [|C|] : B, IFace
{
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public class B
{
    public virtual void M()
    {
    }
}

internal class C : B, IFace
{
}",
                    },
                },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = "MakeDeclaringTypeInternal",
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_ImplicitOverride_AbstractContainingTypeChangedToInternalAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

public abstract class [|C|] : B, IFace
{
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal interface IFace
{
    void M();
}

public abstract class B
{
    public abstract void M();
}

internal abstract class C : B, IFace
{
}",
                    },
                },
                CodeActionIndex = 0, // sealed option is not available because type is abstract
                CodeActionEquivalenceKey = "MakeDeclaringTypeInternal",
            }.RunAsync();
        }

        [Fact]
        public async Task TestBasic_OverriddenMethodChangedToSealedAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class B
    Public MustOverride Sub M()
End Class

Public Class C
    Inherits B
    Implements IFace

    Public Overrides Sub [|M|]() Implements IFace.M
    End Sub
End Class",

@"Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class B
    Public MustOverride Sub M()
End Class

Public Class C
    Inherits B
    Implements IFace

    Public NotOverridable Overrides Sub M() Implements IFace.M
    End Sub
End Class");
        }

        [Fact]
        public async Task TestBasic_VirtualMethodChangedToNotVirtualAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Overridable Sub [|M|]() Implements IFace.M
    End Sub
End Class",

@"Friend Interface IFace
    Sub M()
End Interface

Public Class C
    Implements IFace

    Public Sub M() Implements IFace.M
    End Sub
End Class");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/2285")]
        public async Task TestBasic_AbstractMethodChangedToNotAbstractAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class C
    Implements IFace

    Public MustOverride Sub [|M|]() Implements IFace.M
End Class",

@"Friend Interface IFace
    Sub M()
End Interface

Public MustInherit Class C
    Implements IFace

    Public Sub M() Implements IFace.M
    End Sub
End Class");
        }
    }
}