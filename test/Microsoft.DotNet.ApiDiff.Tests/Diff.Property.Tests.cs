// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffPropertyTests : DiffBaseTests
{
    [Fact]
    public void TestPropertyAdd()
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
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertyChange()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyBeforeProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyAfterProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyBeforeProperty { get { throw null; } set { } }
                +         public int MyAfterProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertyDelete()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
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
                -         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertySetAdd()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } }
                +         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertySetRemove()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertySetVisibilityProtected()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; protected set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } protected set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertySetVisibilityPrivate()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; private set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertyReturnChange()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public float MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } set { } }
                +         public float MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertyNullabilityAdd()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int? MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get { throw null; } set { } }
                +         public int? MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    [Fact]
    public void TestPropertyNullabilityRemove()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int? MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int? MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);
    }

    #region Exclusions

    [Fact]
    public void TestExcludeAddedProperty()
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
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);
    }

    [Fact]
    public void TestExcludeModifiedProperty()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty1 { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty2 { get; set; }
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty1", "P:MyNamespace.MyClass.MyProperty2"]);
    }

    [Fact]
    public void TestExcludeRemovedProperty()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
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
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);
    }

    #endregion
}
