// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffNamespaceTests : DiffBaseTests
{
    #region Block-scoped namespaces

    [Fact]
    public void TestBlockScopedNamespaceAdd()
    {
        RunTest(beforeCode: "",
                afterCode: """
                namespace MyAddedNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: """
                + namespace MyAddedNamespace
                + {
                +     public struct MyStruct
                +     {
                +     }
                + }
                """);
    }

    [Fact]
    public void TestBlockScopedNamespaceChange()
    {
        RunTest(beforeCode: """
                namespace MyBeforeNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyAfterNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: """
                - namespace MyBeforeNamespace
                - {
                -     public struct MyStruct
                -     {
                -     }
                - }
                + namespace MyAfterNamespace
                + {
                +     public struct MyStruct
                +     {
                +     }
                + }
                """);
    }

    [Fact]
    public void TestBlockScopedNamespaceDelete()
    {
        RunTest(beforeCode: """
                namespace MyDeletedNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: "",
                expectedCode: """
                - namespace MyDeletedNamespace
                - {
                -     public struct MyStruct
                -     {
                -     }
                - }
                """);
    }

    [Fact]
    public void TestBlockScopedNamespaceSortAlphabetically()
    {
        // The output is block scoped
        RunTest(beforeCode: "",
                afterCode: """
                namespace C
                {
                    public struct CType
                    {
                    }
                }
                namespace B
                {
                    public struct BType
                    {
                    }
                }
                namespace D
                {
                    public struct DType
                    {
                    }
                }
                namespace A
                {
                    public struct AType
                    {
                    }
                }
                """,
                expectedCode: """
                + namespace A
                + {
                +     public struct AType
                +     {
                +     }
                + }
                + namespace B
                + {
                +     public struct BType
                +     {
                +     }
                + }
                + namespace C
                + {
                +     public struct CType
                +     {
                +     }
                + }
                + namespace D
                + {
                +     public struct DType
                +     {
                +     }
                + }
                """);
    }

    #endregion

    #region File-scoped namespaces

    [Fact]
    public void TestFileScopedNamespaceAdd()
    {
        // The output is block scoped
        RunTest(beforeCode: "",
                afterCode: """
                namespace MyAddedNamespace;
                public struct MyStruct
                {
                }
                """,
                expectedCode: """
                + namespace MyAddedNamespace
                + {
                +     public struct MyStruct
                +     {
                +     }
                + }
                """);
    }

    [Fact]
    public void TestFileScopedNamespaceChange()
    {
        // The output is block scoped
        RunTest(beforeCode: """
                namespace MyBeforeNamespace;
                public struct MyStruct
                {
                }
                """,
                afterCode: """
                namespace MyAfterNamespace;
                public struct MyStruct
                {
                }
                """,
                expectedCode: """
                - namespace MyBeforeNamespace
                - {
                -     public struct MyStruct
                -     {
                -     }
                - }
                + namespace MyAfterNamespace
                + {
                +     public struct MyStruct
                +     {
                +     }
                + }
                """);
    }

    [Fact]
    public void TestFileScopedNamespaceDelete()
    {
        // The output is block scoped
        RunTest(beforeCode: """
                namespace MyDeletedNamespace;
                public struct MyStruct
                {
                }
                """,
                afterCode: "",
                expectedCode: """
                - namespace MyDeletedNamespace
                - {
                -     public struct MyStruct
                -     {
                -     }
                - }
                """);
    }

    #endregion
}
