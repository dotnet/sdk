// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffClassTests : DiffBaseTests
{
    #region Classes

    [Fact]
    public Task ClassAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyAddedClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyAddedClass
                +     {
                +         public MyAddedClass();
                +     }
                  }
                """);

    [Fact]
    public Task ClassAddWithDefaultConstructor() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyAddedClass
                    {
                        public MyAddedClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyAddedClass
                +     {
                +         public MyAddedClass();
                +     }
                  }
                """);

    [Fact]
    public Task ClassChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyBeforeClass
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyAfterClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyBeforeClass
                -     {
                -         public MyBeforeClass();
                -     }
                +     public class MyAfterClass
                +     {
                +         public MyAfterClass();
                +     }
                  }
                """);

    [Fact]
    public Task ClassDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyAddedClass
                    {
                    }
                }
                """,
                    afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                    expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyAddedClass
                -     {
                -         public MyAddedClass();
                -     }
                  }
                """);

    #endregion
}
