// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffDelegateTests : DiffBaseTests
{
    #region Delegates

    [Fact]
    public Task DelegateAdd() => RunTestAsync(
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
                    public delegate void MyDelegate();
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public delegate void MyDelegate();
                  }
                """);

    [Fact]
    public Task DelegateChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public delegate void MyBeforeDelegate();
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public delegate void MyAfterDelegate();
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public delegate void MyBeforeDelegate();
                +     public delegate void MyAfterDelegate();
                  }
                """);

    [Fact]
    public Task DelegateDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public delegate void MyDelegate();
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
                -     public delegate void MyDelegate();
                  }
                """);

    #endregion
}
