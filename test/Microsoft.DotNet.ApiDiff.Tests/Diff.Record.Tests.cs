// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffRecordTests : DiffBaseTests
{
    #region Records

    [Fact]
    public Task RecordAdd() => RunTestAsync(
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
                    public record MyRecord1(int x);
                    public record MyRecord2
                    {
                        public double Y { get; set; }
                    }
                    public record MyRecord3(int x)
                    {
                        public double Y { get; set; }
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                +     public record MyRecord1(int x);
                +     public record MyRecord2
                +     {
                +         public double Y { get; set; }
                +     }
                +     public record MyRecord3(int x)
                +     {
                +         public double Y { get; set; }
                +     }
                  }
                """);

    [Fact]
    public Task RecordChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public record MyBeforeRecord1(int a);
                    public record MyRecord2
                    {
                        public double Y { get; set; }
                    }
                    public record MyRecord3(int a)
                    {
                        public int Y { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public record MyAfterRecord1(int a);
                    public record MyRecord2
                    {
                        public int X { get; set; }
                    }
                    public record MyRecord3(double a)
                    {
                        public double Y { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public record MyBeforeRecord1(int a);
                      public record MyRecord2
                      {
                -         public double Y { get; set; }
                +         public int X { get; set; }
                      }
                -     public record MyRecord3(int a)
                -     {
                -         public int Y { get; set; }
                -     }
                +     public record MyAfterRecord1(int a);
                +     public record MyRecord3(double a)
                +     {
                +         public double Y { get; set; }
                +     }
                  }
                """); // Note they get sorted

    [Fact]
    public Task RecordDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                    public record MyRecord1(int a);
                    public record MyRecord2
                    {
                        public double Y { get; set; }
                    }
                    public record MyRecord3(int x)
                    {
                        public double Y { get; set; }
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
                -     public record MyRecord1(int a);
                -     public record MyRecord2
                -     {
                -         public double Y { get; set; }
                -     }
                -     public record MyRecord3(int x)
                -     {
                -         public double Y { get; set; }
                -     }
                  }
                """);

    #endregion
}
