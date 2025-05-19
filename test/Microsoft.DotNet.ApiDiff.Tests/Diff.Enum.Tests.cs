// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffEnumTests : DiffBaseTests
{
    #region Enum types

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
