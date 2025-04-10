// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffNamespaceTests : DiffBaseTests
{
    #region Block-scoped namespaces

    [Fact]
    public Task BlockScopedNamespaceAdd() => RunTestAsync(
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
    public Task BlockScopedNamespaceChange() => RunTestAsync(
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
    public Task BlockScopedNamespaceDelete() => RunTestAsync(
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
    public Task BlockScopedNamespaceSortAlphabetically() =>
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
    public Task BlockScopedNamespaceUnchanged() => RunTestAsync(
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
    public Task FileScopedNamespaceAdd() =>
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
    public Task FileScopedNamespaceChange() =>
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
    public Task FileScopedNamespaceDelete() =>
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
    public Task FileScopedNamespaceUnchanged() =>
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
    public Task ExcludeAddedNamespace() => RunTestAsync(
                beforeCode: "",
                afterCode: """
                namespace MyNamespace
                {
                }
                """,
                expectedCode: "",
                apisToExclude: ["N:MyNamespace.MyNamespace"]);

    [Fact]
    public Task ExcludeModifiedNamespace() => RunTestAsync(
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
    public Task ExcludeRemovedNamespace() => RunTestAsync(
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

    [Fact]
    public Task NamespaceUsingDependencyKeepFullName() =>
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
                +         public MyAClass();
                +         public void MyMethod(System.Reflection.AssemblyName assemblyName);
                +     }
                + }
                """);

    [Fact]
    public Task NamespacesSameAssemblyDependencyKeepFullName() =>
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
                +         public MyAClass();
                +     }
                + }
                + namespace System.MyNamespaceB
                + {
                +     public class MyBClass
                +     {
                +         public MyBClass();
                +         public void MyMethod(System.MyNamespaceA.MyAClass myAClass);
                +     }
                + }
                """);

    #endregion 
}
