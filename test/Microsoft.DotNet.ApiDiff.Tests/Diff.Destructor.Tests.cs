// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffDestructorTests : DiffBaseTests
{
    [Fact]
    public Task DestructorAdd() => RunTestAsync(
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
                        ~MyClass()
                        {
                        }
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         ~MyClass();
                      }
                  }
                """);

    [Fact]
    public Task DestructorDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        ~MyClass()
                        {
                        }
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
                      public class MyClass
                      {
                -         ~MyClass();
                      }
                  }
                """);

}
