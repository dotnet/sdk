// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffNamespaceTests : DiffBaseTests
{
    #region Block-scoped namespaces

    [Fact]
    public Task TestBlockScopedNamespaceAdd() => RunTestAsync(
                beforeCode: "",
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

    [Fact]
    public Task TestBlockScopedNamespaceChange() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestBlockScopedNamespaceDelete() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestBlockScopedNamespaceSortAlphabetically() =>
        // The output is block scoped
        RunTestAsync(
                beforeCode: "",
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

    [Fact]
    public Task TestBlockScopedNamespaceUnchanged() => RunTestAsync(
                beforeCode: """
                namespace MyAddedNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyAddedNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: ""); // No changes

    #endregion

    #region File-scoped namespaces

    [Fact]
    public Task TestFileScopedNamespaceAdd() =>
        // The output is block scoped
        RunTestAsync(
                beforeCode: "",
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

    [Fact]
    public Task TestFileScopedNamespaceChange() =>
        // The output is block scoped
        RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestFileScopedNamespaceDelete() =>
        // The output is block scoped
        RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestFileScopedNamespaceUnchanged() =>
        RunTestAsync(
                beforeCode: """
                namespace MyAddedNamespace;
                public struct MyStruct
                {
                }
                """,
                afterCode: """
                namespace MyAddedNamespace;
                public struct MyStruct
                {
                }
                """,
                expectedCode: ""); // No changes

    #endregion

    #region Exclusions

    [Fact]
    public Task TestExcludeAddedNamespace() => RunTestAsync(
                beforeCode: "",
                afterCode: """
                namespace MyNamespace
                {
                }
                """,
                expectedCode: "",
                apisToExclude: ["N:MyNamespace.MyNamespace"]);

    [Fact]
    public Task TestExcludeModifiedNamespace() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace1
                {
                }
                """,
                afterCode: """
                namespace MyNamespace2
                {
                }
                """,
                expectedCode: "",
                apisToExclude: ["N:MyNamespace.MyNamespace1", "N:MyNamespace.MyNamespace2"]);

    [Fact]
    public Task TestExcludeRemovedNamespace() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                }
                """,
                afterCode: "",
                expectedCode: "",
                apisToExclude: ["N:MyNamespace.MyNamespace"]);

    #endregion

    #region Full names

    [Fact(Skip = "Still working on this")]
    public Task TestNamespaceUsingDependencyKeepFullName() =>
        // If the same assembly contains two APIs in two different namespaces, but the two namespaces
        // share a prefix of their name, and a reference to the API from the other namespace is
        // excluding part of the namespace, make sure the final result contains the full name.
        RunTestAsync(
        beforeCode: "",
        afterCode: """
                using System.Reflection;
                namespace System.MyNamespace
                {
                    public class MyAClass
                    {
                        public void MyMethod(Reflection.AssemblyName assemblyName) { }
                    }
                }
                """,
        expectedCode: """
                + namespace System.MyNamespace
                + {
                +     public class MyAClass
                +     {
                +         public void MyMethod(System.Reflection.AssemblyName assemblyName);
                +     }
                + }
                """,
        hideImplicitDefaultConstructors: true);

    [Fact(Skip = "Still working on this")]
    public Task TestNamespacesSameAssemblyDependencyKeepFullName() =>
        // If the same assembly contains two APIs in two different namespaces, but the two namespaces
        // share a prefix of their name, and a reference to the API from the other namespace is
        // excluding part of the namespace, make sure the final result contains the full name.
        RunTestAsync(
        beforeCode: "",
        afterCode: """
                namespace System.MyNamespaceA
                {
                    public class MyAClass
                    {
                    }
                }
                namespace System.MyNamespaceB
                {
                    public class MyBClass
                    {
                        public void MyMethod(MyNamespaceA.MyAClass myAClass) { }
                    }
                }
                """,
        expectedCode: """
                + namespace System.MyNamespaceA
                + {
                +     public class MyAClass
                +     {
                +     }
                + }
                + namespace System.MyNamespaceB
                + {
                +     public class MyBClass
                +     {
                +         public void MyMethod(System.MyNamespaceA.MyAClass myAClass);
                +     }
                + }
                """,
        hideImplicitDefaultConstructors: true);

    #endregion 
}
