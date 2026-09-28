// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

// Since operators are also methods, this class tests more basic things than the methods class.
public class DiffOperatorTests : DiffBaseTests
{
    [Fact]
    public Task EqualityOperators() =>
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
                +         public override bool Equals(object? o);
                +         public override int GetHashCode();
                +         public static MyNamespace.MyClass operator ==(MyNamespace.MyClass a, MyNamespace.MyClass b);
                +         public static MyNamespace.MyClass operator !=(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task AdditionOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator +(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task SubtractionOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator -(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task MultiplicationOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator *(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task DivisionOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator /(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task ModulusOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator %(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task LessAndGreaterThanOperator() => RunTestAsync(
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
                +         public static bool operator >(MyNamespace.MyClass a, MyNamespace.MyClass b);
                +         public static bool operator <(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task LessAndGreaterThanOrEqualOperator() => RunTestAsync(
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
                +         public static bool operator >=(MyNamespace.MyClass a, MyNamespace.MyClass b);
                +         public static bool operator <=(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task IncrementOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator ++(MyNamespace.MyClass a);
                      }
                  }
                """);

    [Fact]
    public Task DecrementOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator --(MyNamespace.MyClass a);
                      }
                  }
                """);

    [Fact]
    public Task LogicalNotOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator !(MyNamespace.MyClass a);
                      }
                  }
                """);

    [Fact]
    public Task BitwiseNotOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator ~(MyNamespace.MyClass a);
                      }
                  }
                """);

    [Fact]
    public Task BitwiseAndOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator &(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task BitwiseOrOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator |(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task BitwiseXorOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator ^(MyNamespace.MyClass a, MyNamespace.MyClass b);
                      }
                  }
                """);

    [Fact]
    public Task LeftShiftOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator <<(MyNamespace.MyClass a, int shift);
                      }
                  }
                """);

    [Fact]
    public Task RightShiftOperator() => RunTestAsync(
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
                +         public static MyNamespace.MyClass operator >>(MyNamespace.MyClass a, int shift);
                      }
                  }
                """);

    [Fact]
    public Task ImplicitOperator() => RunTestAsync(
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
                +         public static implicit operator MyNamespace.MyClass(int value);
                      }
                  }
                """);

    [Fact]
    public Task ExplicitOperator() => RunTestAsync(
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
                +         public static explicit operator int(MyNamespace.MyClass value);
                      }
                  }
                """);

    [Fact]
    public Task ExplicitCheckedOperator() => RunTestAsync(
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
                +         public static explicit operator checked byte(MyNamespace.MyClass value);
                +         public static explicit operator byte(MyNamespace.MyClass value);
                      }
                  }
                """);

    #region Exclusions

    [Fact]
    public Task ExcludeAddedOperator() => RunTestAsync(
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
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32"]);

    [Fact]
    public Task ExcludeModifiedOperator() => RunTestAsync(
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
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32", "M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Byte"]);

    [Fact]
    public Task ExcludeRemovedOperator() => RunTestAsync(
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
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Int32"]);

    [Fact]
    public Task ExcludeUnmodifiedOperator() => RunTestAsync(
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
                apisToExclude: ["M:MyNamespace.MyClass.op_Explicit(MyNamespace.MyClass)~System.Byte"]);

    #endregion
}
