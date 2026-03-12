// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffMethodTests : DiffBaseTests
{
    #region Methods

    [Fact]
    public Task MethodAdd() => RunTestAsync(
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
                        public void MyMethod()
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
                +         public void MyMethod();
                      }
                  }
                """);

    [Fact]
    public Task MethodChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyBeforeMethod()
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
                        public void MyAfterMethod()
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
                -         public void MyBeforeMethod();
                +         public void MyAfterMethod();
                      }
                  }
                """);

    [Fact]
    public Task MethodDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod()
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
                -         public void MyMethod();
                      }
                  }
                """);

    [Fact]
    public Task MethodReturnChange() =>
        // The DocID remains the same, but the return type changes
        RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod()
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
                        public int MyMethod()
                        {
                            return 0;
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public void MyMethod();
                +         public int MyMethod();
                      }
                  }
                """);

    [Fact]
    public Task MethodParametersChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod()
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
                        public void MyMethod(int x)
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
                -         public void MyMethod();
                +         public void MyMethod(int x);
                      }
                  }
                """);

    #endregion

    #region Exclusions

    [Fact]

    public Task ExcludeModifiedMethod() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod1() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod2() { }
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["M:MyNamespace.MyClass.MyMethod1", "M:MyNamespace.MyClass.MyMethod2"]);

    [Fact]
    public Task ExcludeRemovedMethod() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public void MyMethod() { }
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
                expectedCode: "",
                apisToExclude: ["M:MyNamespace.MyClass.MyMethod"]);

    #endregion
}
