// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffPropertyTests : DiffBaseTests
{
    [Fact]
    public Task TestPropertyAdd() => RunTestAsync(
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
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);

    [Fact]
    public Task TestPropertyChange() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestPropertyDelete() => RunTestAsync(
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

    [Fact]
    public Task TestPropertySetAdd() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestPropertySetRemove() => RunTestAsync(
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
                -         public int MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } }
                      }
                  }
                """);

    [Fact]
    public Task TestPropertySetVisibilityProtected() => RunTestAsync(
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

    [Fact]
    public Task TestPropertySetVisibilityPrivate() => RunTestAsync(
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
                -         public int MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } }
                      }
                  }
                """);

    [Fact]
    public Task TestPropertyReturnChange() => RunTestAsync(
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
                -         public int MyProperty { get { throw null; } set { } }
                +         public float MyProperty { get { throw null; } set { } }
                      }
                  }
                """);

    [Fact]
    public Task TestPropertyNullabilityAdd() => RunTestAsync(
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
                -         public int MyProperty { get { throw null; } set { } }
                +         public int? MyProperty { get { throw null; } set { } }
                      }
                  }
                """);

    [Fact]
    public Task TestPropertyNullabilityRemove() => RunTestAsync(
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
                -         public int? MyProperty { get { throw null; } set { } }
                +         public int MyProperty { get { throw null; } set { } }
                      }
                  }
                """);

    #region Exclusions

    [Fact]
    public Task TestExcludeAddedProperty() => RunTestAsync(
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    [Fact]
    public Task TestExcludeModifiedProperty() => RunTestAsync(
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty1", "P:MyNamespace.MyClass.MyProperty2"]);

    [Fact]
    public Task TestExcludeRemovedProperty() => RunTestAsync(
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
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    #endregion
}
