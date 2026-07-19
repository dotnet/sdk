// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffExtensionMemberTests : DiffBaseTests
{
    [TestMethod]
    public Task InstanceExtensionMembersAdd() => RunTestAsync(
        beforeCode: """
            namespace MyNamespace
            {
                public static class MyExtensions
                {
                }
            }
            """,
        afterCode: """
            namespace MyNamespace
            {
                public static class MyExtensions
                {
                    extension(int value)
                    {
                        public int Increment() => value + 1;
                        public bool IsEven => value % 2 == 0;
                    }
                }
            }
            """,
        expectedCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
            +         extension(int value)
            +         {
            +             public int Increment();
            +             public bool IsEven { get; }
            +         }
                  }
              }
            """);

    [TestMethod]
    public Task StaticExtensionMembersAdd() => RunTestAsync(
        beforeCode: """
            namespace MyNamespace
            {
                public static class MyExtensions
                {
                }
            }
            """,
        afterCode: """
            namespace MyNamespace
            {
                public static class MyExtensions
                {
                    extension(int)
                    {
                        public static int Add(int left, int right) => left + right;
                        public static int Zero => 0;
                    }
                }
            }
            """,
        expectedCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
            +         extension(int)
            +         {
            +             public static int Add(int left, int right);
            +             public static int Zero { get; }
            +         }
                  }
              }
            """);

    [TestMethod]
    public Task InstanceExtensionMembersChangeAndDelete() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension(int value)
                      {
                          public int Increment() => value + 1;
                          public bool IsEven => value % 2 == 0;
                      }
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension(int value)
                      {
                          public long Increment() => value + 1;
                          public string Describe() => value.ToString();
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
                        extension(int value)
                        {
              -             public int Increment();
              +             public long Increment();
              -             public bool IsEven { get; }
              +             public string Describe();
                        }
                    }
                }
              """);

    [TestMethod]
    public Task ExtensionBlockDoesNotHideConventionalStaticMembers() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      public static int Classic(this int value) => value;
                      public static int Ordinary() => 0;

                      extension(int value)
                      {
                          public int Increment() => value + 1;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         public static int Classic(this int value);
              +         public static int Ordinary();
              +         extension(int value)
              +         {
              +             public int Increment();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task GenericExtensionMemberAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T value)
                      {
                          public T Identity() => value;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T value)
              +         {
              +             public T Identity();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task ExtensionMembersForDifferentTargetTypesAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension(int value)
                      {
                          public int Increment() => value + 1;
                      }

                      extension(string text)
                      {
                          public int Length() => text.Length;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension(string text)
              +         {
              +             public int Length();
              +         }
              +         extension(int value)
              +         {
              +             public int Increment();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task ExtensionMembersForSameTargetTypeInSeparateBlocksAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension(int value)
                      {
                          public int Increment() => value + 1;
                      }

                      extension(int number)
                      {
                          public int Double() => number * 2;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension(int number)
              +         {
              +             public int Double();
              +         }
              +         extension(int value)
              +         {
              +             public int Increment();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task ExtensionMembersWithAlternatingTargetTypesAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension(int first)
                      {
                          public int Increment() => first + 1;
                      }

                      extension(string text)
                      {
                          public int Length() => text.Length;
                      }

                      extension(int second)
                      {
                          public int Double() => second * 2;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension(string text)
              +         {
              +             public int Length();
              +         }
              +         extension(int second)
              +         {
              +             public int Double();
              +         }
              +         extension(int first)
              +         {
              +             public int Increment();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task GenericStaticExtensionMemberAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T)
                      {
                          public static TResult Create<TResult>(TResult value) => value;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T)
              +         {
              +             public static TResult Create<TResult>(TResult value);
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task GenericInstanceExtensionMemberAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T value)
                      {
                          public TResult Convert<TResult>(TResult result) => result;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T value)
              +         {
              +             public TResult Convert<TResult>(TResult result);
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task ConstrainedGenericInstanceExtensionMemberAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T value)
                          where T : class
                      {
                          public TResult Convert<TResult>(TResult result)
                              where TResult : class
                              => result;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T value)
              +             where T : class
              +         {
              +             public TResult Convert<TResult>(TResult result)
              +                 where TResult : class;
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task GenericStaticExtensionMemberWithTypeLevelGenericAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T)
                      {
                          public static T Default() => throw new System.NotImplementedException();
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T)
              +         {
              +             public static T Default();
              +         }
                    }
                }
              """);

    [TestMethod]
    public Task ConstrainedGenericStaticExtensionMemberAdd() => RunTestAsync(
        beforeCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                  }
              }
              """,
        afterCode: """
              namespace MyNamespace
              {
                  public static class MyExtensions
                  {
                      extension<T>(T)
                          where T : class
                      {
                          public static TResult Create<TResult>(TResult value)
                              where TResult : class
                              => value;
                      }
                  }
              }
              """,
        expectedCode: """
                namespace MyNamespace
                {
                    public static class MyExtensions
                    {
              +         extension<T>(T)
              +             where T : class
              +         {
              +             public static TResult Create<TResult>(TResult value)
              +                 where TResult : class;
              +         }
                    }
                }
              """);
}
