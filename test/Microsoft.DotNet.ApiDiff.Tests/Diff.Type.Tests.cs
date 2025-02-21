// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffTypeTests : DiffBaseTests
{
    #region Classes

    [Fact]
    public Task TestClassAdd() => RunTestAsync(
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
                    public class MyAddedClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyAddedClass
                +     {
                +         public MyAddedClass() { }
                +     }
                  }
                """);

    [Fact]
    public Task TestClassChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyBeforeClass
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
                    public class MyAfterClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyBeforeClass
                -     {
                -         public MyBeforeClass() { }
                -     }
                +     public class MyAfterClass
                +     {
                +         public MyAfterClass() { }
                +     }
                  }
                """);

    [Fact]
    public Task TestClassDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public class MyAddedClass
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
                -     public class MyAddedClass
                -     {
                -         public MyAddedClass() { }
                -     }
                  }
                """);

    #endregion

    #region Structs

    [Fact]
    public Task TestStructAdd() => RunTestAsync(
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
    public Task TestStructChange() => RunTestAsync(
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
    public Task TestStructDelete() => RunTestAsync(
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

    #region Records

    [Fact]
    public Task TestRecordAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public record MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public record MyStruct
                    {
                    }
                    public record MyRecord(int a);
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public record MyRecord(int a)
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task TestRecordChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public record MyBeforeRecord(int a);
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public record MyAfterRecord(int a);
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public record MyBeforeRecord(int a)
                -     {
                -     }
                +     public record MyAfterRecord(int a)
                +     {
                +     }
                  }
                """);

    [Fact]
    public Task TestRecordDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public record MyStruct
                    {
                    }
                    public record MyRecord(int a);
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public record MyStruct
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public record MyRecord(int a)
                -     {
                -     }
                  }
                """);

    #endregion

    #region Delegates

    [Fact]
    public Task TestDelegateAdd() => RunTestAsync(
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
    public Task TestDelegateChange() => RunTestAsync(
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
    public Task TestDelegateDelete() => RunTestAsync(
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

    #region Interfaces

    [Fact]
    public Task TestInterfaceAdd() => RunTestAsync(
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
    public Task TestInterfaceChange() => RunTestAsync(
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
    public Task TestInterfaceDelete() => RunTestAsync(
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

    #region Classes - Hide Default constructors

    [Fact]
    public Task TestTypeAddHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                    public class MyClass1
                    {
                        public MyClass1() { }
                    }
                    public class MyClass2
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyClass1
                +     {
                +     }
                +     public class MyClass2
                +     {
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestTypeChangeHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyBeforeClass1
                    {
                        public MyBeforeClass1() { }
                    }
                    public class MyBeforeClass2
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyAfterClass1
                    {
                        public MyAfterClass1() { }
                    }
                    public class MyAfterClass2
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyBeforeClass1
                -     {
                -     }
                -     public class MyBeforeClass2
                -     {
                -     }
                +     public class MyAfterClass1
                +     {
                +     }
                +     public class MyAfterClass2
                +     {
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestNestedTypeAddHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                    public class MyClass1
                    {
                        public class MyNestedClass1
                        {
                            public MyNestedClass1() { }
                        }
                        public class MyNestedClass2
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyClass1
                +     {
                +         public class MyNestedClass1
                +         {
                +         }
                +         public class MyNestedClass2
                +         {
                +         }
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestNestedTypeChangeHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass1
                    {
                        public class MyBeforeNestedClass1
                        {
                            public MyBeforeNestedClass1() { }
                        }
                        public class MyBeforeNestedClass2
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass1
                    {
                        public class MyAfterNestedClass1
                        {
                            public MyAfterNestedClass1() { }
                        }
                        public class MyAfterNestedClass2
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass1
                      {
                -         public class MyBeforeNestedClass1
                -         {
                -         }
                -         public class MyBeforeNestedClass2
                -         {
                -         }
                +         public class MyAfterNestedClass1
                +         {
                +         }
                +         public class MyAfterNestedClass2
                +         {
                +         }
                      }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    #endregion

    #region Nested types

    [Fact]
    public Task TestNestedTypeAdd() => RunTestAsync(
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
                +             public MyNestedType() { }
                +         }
                      }
                  }
                """);

    [Fact]
    public Task TestNestedTypeChange() => RunTestAsync(
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
                -             public MyBeforeNestedType() { }
                -         }
                +         public class MyAfterNestedType
                +         {
                +             public MyAfterNestedType() { }
                +         }
                      }
                  }
                """);

    [Fact]
    public Task TestNestedTypeRemove() => RunTestAsync(
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
                -             public MyNestedType() { }
                -         }
                      }
                  }
                """);

    [Fact]
    public Task TestNestedTypeMemberAdd() => RunTestAsync(
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
                +             public void MyMethod() { }
                          }
                      }
                  }
                """);

    [Fact]
    public Task TestNestedTypeMemberChange() => RunTestAsync(
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
                -             public void MyBeforeMethod() { }
                +             public void MyAfterMethod() { }
                          }
                      }
                  }
                """);

    [Fact]
    public Task TestNestedTypeMemberRemove() => RunTestAsync(
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
                -             public void MyMethod() { }
                          }
                      }
                  }
                """);

    #endregion

    #region Exclusions

    [Fact]
    public Task TestExcludeAddedType() => RunTestAsync(
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["T:MyNamespace.MyStruct"]);

    [Fact]
    public Task TestExcludeModifiedType() => RunTestAsync(
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["T:MyNamespace.MyStruct1", "T:MyNamespace.MyStruct2"]);

    [Fact]
    public Task TestExcludeRemovedType() => RunTestAsync(
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
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["T:MyNamespace.MyStruct"]);

    #endregion

    #region General

    [Fact]
    public Task TestTypeKindChange() =>
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
                +         public MyType() { }
                +     }
                  }
                """);

    [Fact]
    public Task TestShowPartial() => RunTestAsync(
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
                +         public MyClass() { }
                +         public partial class MySubClass
                +         {
                +             public MySubClass() { }
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
