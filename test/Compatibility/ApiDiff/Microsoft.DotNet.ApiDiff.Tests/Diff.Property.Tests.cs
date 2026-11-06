// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffPropertyTests : DiffBaseTests
{
    [TestMethod]
    public Task PropertyAdd() => RunTestAsync(
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
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertyChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyBeforeProperty { get { throw null; } set { } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyAfterProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyBeforeProperty { get; set; }
                +         public int MyAfterProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertyDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
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
                -         public int MyProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertySetAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; }
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertySetRemove() => RunTestAsync(
                beforeCode: """
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
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertySetVisibilityProtected() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } protected set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; protected set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertySetVisibilityPrivate() => RunTestAsync(
                beforeCode: """
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
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertyReturnChange() => RunTestAsync(
                beforeCode: """
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
                -         public int MyProperty { get; set; }
                +         public float MyProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertyNullabilityAdd() => RunTestAsync(
                beforeCode: """
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
                -         public int MyProperty { get; set; }
                +         public int? MyProperty { get; set; }
                      }
                  }
                """);

    [TestMethod]
    public Task PropertyNullabilityRemove() => RunTestAsync(
                beforeCode: """
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
                -         public int? MyProperty { get; set; }
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    /// <summary>
    /// Regression test for https://github.com/dotnet/core/issues/10213.
    /// When a class has only a private constructor, GenAPI synthesizes an internal
    /// default constructor via TryGetInternalDefaultConstructor. That constructor was
    /// created without a body, causing it to fuse with the next member on the same
    /// line during formatting. This made the first property lose its indentation in the diff.
    /// </summary>
    [TestMethod]
    public Task PropertyAddInClassWithPrivateConstructor() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyStub
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyStub
                    {
                    }
                    public class MyClass
                    {
                        private MyClass() { }
                        public static bool IsSupported { get { throw null; } }
                        public static void Method1() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyClass
                +     {
                +         public static void Method1();
                +         public static bool IsSupported { get; }
                +     }
                  }
                """);

    #region Exclusions

    [TestMethod]
    public Task ExcludeAddedProperty() => RunTestAsync(
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
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    [TestMethod]
    public Task ExcludeModifiedProperty() => RunTestAsync(
                beforeCode: """
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
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty1", "P:MyNamespace.MyClass.MyProperty2"]);

    [TestMethod]
    public Task ExcludeRemovedProperty() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
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
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    #endregion
}
