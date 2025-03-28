// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffInterfaceTests : DiffBaseTests
{
    #region Interfaces

    [Fact]
    public Task InterfaceAdd() => RunTestAsync(
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
                    public interface IMyInterface
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public interface IMyInterface
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task InterfaceChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public interface IMyBeforeInterface
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public interface IMyAfterInterface
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public interface IMyBeforeInterface
                -     {
                -     }
                +     public interface IMyAfterInterface
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task InterfaceDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public interface IMyInterface
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
                -     public interface IMyInterface
                -     {
                -     }
                  }
                """);

    #endregion
}
