// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffStructTests : DiffBaseTests
{
    #region Structs

    [Fact]
    public Task StructAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public struct MyAddedStruct
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public struct MyAddedStruct
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task StructChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyBeforeStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyAfterStruct
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public struct MyBeforeStruct
                -     {
                -     }
                +     public struct MyAfterStruct
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task StructDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public struct MyDeletedStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public struct MyDeletedStruct
                -     {
                -     }
                  }
                """);

    #endregion
}
