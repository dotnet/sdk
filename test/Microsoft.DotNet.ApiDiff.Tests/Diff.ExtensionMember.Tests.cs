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
}
