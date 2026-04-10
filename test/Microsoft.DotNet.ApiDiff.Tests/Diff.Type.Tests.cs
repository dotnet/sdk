// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffTypeTests : DiffBaseTests
{
    #region Nested types

    [Fact]
    public Task NestedTypeAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                            public MyNestedType() { }
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                +         public class MyNestedType
                +         {
                +             public MyNestedType();
                +         }
                      }
                  }
                """);

    [Fact]
    public Task NestedTypeChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyBeforeNestedType
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyAfterNestedType
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                -         public class MyBeforeNestedType
                -         {
                -             public MyBeforeNestedType();
                -         }
                +         public class MyAfterNestedType
                +         {
                +             public MyAfterNestedType();
                +         }
                      }
                  }
                """);

    [Fact]
    public Task NestedTypeRemove() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                            public MyNestedType() { }
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                -         public class MyNestedType
                -         {
                -             public MyNestedType();
                -         }
                      }
                  }
                """);

    [Fact]
    public Task NestedTypeMemberAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                               public void MyMethod() { }
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                          public class MyNestedType
                          {
                +             public void MyMethod();
                          }
                      }
                  }
                """);

    [Fact]
    public Task NestedTypeMemberChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                               public void MyBeforeMethod() { }
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                               public void MyAfterMethod() { }
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                          public class MyNestedType
                          {
                -             public void MyBeforeMethod();
                +             public void MyAfterMethod();
                          }
                      }
                  }
                """);

    [Fact]
    public Task NestedTypeMemberRemove() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                               public void MyMethod() { }
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                        public class MyNestedType
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyType
                      {
                          public class MyNestedType
                          {
                -             public void MyMethod();
                          }
                      }
                  }
                """);

    #endregion

    #region Exclusions

    [Fact]
    public Task ExcludeAddedType() => RunTestAsync(
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
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["T:MyNamespace.MyStruct"]);

    [Fact]
    public Task ExcludeModifiedType() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public struct MyStruct1
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
                    public struct MyStruct2
                    {
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["T:MyNamespace.MyStruct1", "T:MyNamespace.MyStruct2"]);

    [Fact]
    public Task ExcludeRemovedType() => RunTestAsync(
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
                }
                """,
                expectedCode: "", // The removal is not shown
                apisToExclude: ["T:MyNamespace.MyStruct"]);

    #endregion

    #region Other

    [Fact]
    public Task TypeKindChange() =>
        // Name remains the same (as well as DocID), but the kind changes
        RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyType
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyType
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public struct MyType
                -     {
                -     }
                +     public class MyType
                +     {
                +         public MyType();
                +     }
                  }
                """);

    [Fact]
    public Task ShowPartial() => RunTestAsync(
                beforeCode: "",
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public class MySubClass
                        {
                        }
                    }
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: """
                + namespace MyNamespace
                + {
                +     public partial class MyClass
                +     {
                +         public MyClass();
                +         public partial class MySubClass
                +         {
                +             public MySubClass();
                +         }
                +     }
                +     public partial struct MyStruct
                +     {
                +     }
                + }
                """,
                addPartialModifier: true);

    #endregion
}
