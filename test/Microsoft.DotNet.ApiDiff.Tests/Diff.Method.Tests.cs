// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffMethodTests : DiffBaseTests
{
    #region Methods

    [Fact]
    public void TestMethodAdd()
    {
        RunTest(beforeCode: """
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
                +         public void MyMethod() { }
                      }
                  }
                """);
    }

    [Fact]
    public void TestMethodChange()
    {
        RunTest(beforeCode: """
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
                -         public void MyBeforeMethod() { }
                +         public void MyAfterMethod() { }
                      }
                  }
                """);
    }

    [Fact]
    public void TestMethodDelete()
    {
        RunTest(beforeCode: """
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
                -         public void MyMethod() { }
                      }
                  }
                """);
    }

    [Fact]
    public void TestMethodReturnChange()
    {
        // The DocID remains the same, but the return type changes
        RunTest(beforeCode: """
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
                -         public void MyMethod() { }
                +         public int MyMethod() { throw null; }
                      }
                  }
                """);
    }

    [Fact]
    public void TestMethodParametersChange()
    {
        RunTest(beforeCode: """
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
                -         public void MyMethod() { }
                +         public void MyMethod(int x) { }
                      }
                  }
                """);
    }

    #endregion

    #region Constructors

    [Fact]
    public void TestConstructorAdd()
    {
        RunTest(beforeCode: """
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
                        public MyClass(int x)
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
                +         public MyClass(int x) { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    [Fact]
    public void TestConstructorDelete()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass(int x)
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
                -         public MyClass(int x) { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    [Fact]
    public void TestDefaultConstructorMakePrivate()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass()
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
                        private MyClass()
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
                -         public MyClass() { }
                      }
                  }
                """);
    }

    [Fact]
    public void TestDefaultConstructorMakePublic()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        private MyClass()
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
                        public MyClass()
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
                +         public MyClass() { }
                      }
                  }
                """);
    }

    #endregion

    #region Exclusions

    [Fact]
    public void TestExcludeAddedMethod()
    {
        RunTest(beforeCode: """
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
                        public void MyMethod() { }
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.MyMethod"]);
    }

    [Fact]
    public void TestExcludeModifiedMethod()
    {
        RunTest(beforeCode: """
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.MyMethod1", "M:MyNamespace.MyClass.MyMethod2"]);
    }

    [Fact]
    public void TestExcludeRemovedMethod()
    {
        RunTest(beforeCode: """
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.MyMethod"]);
    }

    #endregion
}
