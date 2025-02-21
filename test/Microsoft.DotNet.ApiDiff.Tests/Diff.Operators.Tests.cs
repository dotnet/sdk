// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

// Since operators are also methods, this class tests more basic things than the methods class.
public class DiffOperatorsTests : DiffBaseTests
{
    [Fact]
    public Task TestEqualityOperators() =>
        // The equality and inequality operators require also adding Equals and GetHashCode overrides
        RunTestAsync(
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
                        public static MyClass operator ==(MyClass a, MyClass b) { throw null; }
                        public static MyClass operator !=(MyClass a, MyClass b) { throw null; }
                        public override bool Equals(object? o) { throw null; }
                        public override int GetHashCode() { throw null; }
                    }
                }
                """,
                // Note that the order of the methods is different in the expected code
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public override bool Equals(object? o) { throw null; }
                +         public override int GetHashCode() { throw null; }
                +         public static MyClass operator ==(MyClass a, MyClass b) { throw null; }
                +         public static MyClass operator !=(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestAdditionOperator() => RunTestAsync(
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
                        public static MyClass operator +(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator +(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestSubtractionOperator() => RunTestAsync(
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
                        public static MyClass operator -(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator -(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestMultiplicationOperator() => RunTestAsync(
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
                        public static MyClass operator *(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator *(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestDivisionOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                // This would've thrown CS8597 but it's disabled by CSharpAssemblyDocumentGenerator
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static MyClass operator /(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator /(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestModulusOperator() => RunTestAsync(
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
                        public static MyClass operator %(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator %(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestLessAndGreaterThanOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                // This would've thrown CS8597 but it's disabled by CSharpAssemblyDocumentGenerator
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static bool operator <(MyClass a, MyClass b) { throw null; }
                        public static bool operator >(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                // Note that the order of the operators is different in the expected code
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static bool operator >(MyClass a, MyClass b) { throw null; }
                +         public static bool operator <(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestLessAndGreaterThanOrEqualOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                // This would've thrown CS8597 but it's disabled by CSharpAssemblyDocumentGenerator
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static bool operator <=(MyClass a, MyClass b) { throw null; }
                        public static bool operator >=(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                // Note that the order of the operators is different in the expected code
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static bool operator >=(MyClass a, MyClass b) { throw null; }
                +         public static bool operator <=(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestIncrementOperator() => RunTestAsync(
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
                        public static MyClass operator ++(MyClass a) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator ++(MyClass a) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestDecrementOperator() => RunTestAsync(
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
                        public static MyClass operator --(MyClass a) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator --(MyClass a) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestLogicalNotOperator() => RunTestAsync(
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
                        public static MyClass operator !(MyClass a) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator !(MyClass a) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestBitwiseNotOperator() => RunTestAsync(
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
                        public static MyClass operator ~(MyClass a) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator ~(MyClass a) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestBitwiseAndOperator() => RunTestAsync(
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
                        public static MyClass operator &(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator &(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestBitwiseOrOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                // This would've thrown CS8597 but it's disabled by CSharpAssemblyDocumentGenerator
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static MyClass operator |(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator |(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestBitwiseXorOperator() => RunTestAsync(
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
                        public static MyClass operator ^(MyClass a, MyClass b) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator ^(MyClass a, MyClass b) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestLeftShiftOperator() => RunTestAsync(
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
                        public static MyClass operator <<(MyClass a, int shift) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator <<(MyClass a, int shift) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestRightShiftOperator() => RunTestAsync(
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
                        public static MyClass operator >>(MyClass a, int shift) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static MyClass operator >>(MyClass a, int shift) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestImplicitOperator() => RunTestAsync(
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
                        public static implicit operator MyClass(int value) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static implicit operator MyClass(int value) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestExplicitOperator() => RunTestAsync(
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
                        public static explicit operator int(MyClass value) { throw null; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static explicit operator int(MyClass value) { throw null; }
                      }
                  }
                """);

    [Fact]
    public Task TestExplicitCheckedOperator() => RunTestAsync(
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
                        public static explicit operator byte(MyClass value) => (byte)(MyClass)value;
                        public static explicit operator checked byte(MyClass value) => checked((byte)(MyClass)value);
                    }
                }
                """, // Notice they get sorted
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static explicit operator checked byte(MyClass value) { throw null; }
                +         public static explicit operator byte(MyClass value) { throw null; }
                      }
                  }
                """);

    #region Exclusions

    [Fact]
    public Task TestExcludeAddedOperator() => RunTestAsync(
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
                        public static explicit operator int(MyClass value) { throw null; }
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32"]);

    [Fact]
    public Task TestExcludeModifiedOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static explicit operator int(MyClass value) { throw null; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static explicit operator byte(MyClass value) { throw null; }
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32", "M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Byte"]);

    [Fact]
    public Task TestExcludeRemovedOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static explicit operator int(MyClass value) { throw null; }
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
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32"]);

    [Fact]
    public Task TestExcludeUnmodifiedOperator() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static explicit operator byte(MyClass value) => (byte)(MyClass)value;
                        public static explicit operator checked byte(MyClass value) => checked((byte)(MyClass)value);
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public static explicit operator byte(MyClass value) => (byte)(MyClass)value;
                        public static explicit operator checked byte(MyClass value) => checked((byte)(MyClass)value);
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Byte"]);

    #endregion
}
