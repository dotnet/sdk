// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffEnumTests : DiffBaseTests
{
    #region Enum types

    [TestMethod]
    public Task EnumTypeAddWithOneMember() => RunTestAsync(
        // Dummy record is added to avoid thinking namespace is empty.
        beforeCode: """
        namespace MyNamespace
        {
            public record MyRecord(int x);
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
            public record MyRecord(int x);
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
        +     public enum MyEnum
        +     {
        +         Default = 0,
        +     }
          }
        """);

    [TestMethod]
    public Task EnumTypeAddWithMultipleSortedMembers() => RunTestAsync(
        // Dummy record is added to avoid thinking namespace is empty.
        beforeCode: """
        namespace MyNamespace
        {
            public record MyRecord(int x);
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
                Bar = 2,
            }
            public record MyRecord(int x);
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
        +     public enum MyEnum
        +     {
        +         Default = 0,
        +         Foo = 1,
        +         Bar = 2,
        +     }
          }
        """);

    [TestMethod]
    public Task EnumTypeAddWithMultipleUnsortedMembers() => RunTestAsync(
        // Enum members shall show up sorted by value, not alphabetically.
        // Dummy record is added to avoid thinking namespace is empty.
        beforeCode: """
        namespace MyNamespace
        {
            public record MyRecord(int x);
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                What = 3,
                Foo = 1,
                Bar = 2,
                Default = 0,
            }
            public record MyRecord(int x);
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
        +     public enum MyEnum
        +     {
        +         Default = 0,
        +         Foo = 1,
        +         Bar = 2,
        +         What = 3,
        +     }
          }
        """);

    [TestMethod]
    public Task EnumTypeWithOneMemberRemove() => RunTestAsync(
        // Dummy record is added to avoid thinking namespace is empty.
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
            public record MyRecord(int x);
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public record MyRecord(int x);
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
        -     public enum MyEnum
        -     {
        -         Default = 0,
        -     }
          }
        """);

    [TestMethod]
    public Task EnumTypeWithMultipleMembersRemove() => RunTestAsync(
        // Dummy record is added to avoid thinking namespace is empty.
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
                Bar = 2,
            }
            public record MyRecord(int x);
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public record MyRecord(int x);
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
        -     public enum MyEnum
        -     {
        -         Default = 0,
        -         Foo = 1,
        -         Bar = 2,
        -     }
          }
        """);

    #endregion

    #region Enum members

    [TestMethod]
    public Task EnumMemberAddOne() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Other = 1,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        +         Other = 1,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberAddMultipleSorted() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
                Bar = 2,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        +         Foo = 1,
        +         Bar = 2,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberAddMultipleUnsorted() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Bar = 2,
                Default = 0,
                Foo = 1,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        +         Foo = 1,
        +         Bar = 2,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberValueChange() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 1,
                Foo = 2,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        -         Default = 0,
        +         Default = 1,
        -         Foo = 1,
        +         Foo = 2,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberNameChange() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo2 = 1,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        -         Foo = 1,
        +         Foo2 = 1,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberRemoveOne() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Other = 1,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        -         Other = 1,
              }
          }
        """);

    [TestMethod]
    public Task EnumMemberRemoveMultiple() => RunTestAsync(
        beforeCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Default = 0,
                Foo = 1,
                Bar = 2,
            }
        }
        """,
        afterCode: """
        namespace MyNamespace
        {
            public enum MyEnum
            {
                Foo = 1,
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public enum MyEnum
              {
        -         Default = 0,
        -         Bar = 2,
              }
          }
        """);

    #endregion
}
