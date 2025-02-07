// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffOverloadsTests : DiffBaseTests
{
    [Fact]
    public void TestEqualityOperators()
    {
        // The equality and inequality operators require also adding Equals and GetHashCode overrides
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestAdditionOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestSubtractionOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestMultiplicationOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestDivisionOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestModulusOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestLessAndGreaterThanOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestLessAndGreaterThanOrEqualOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestIncrementOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestDecrementOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestLogicalNotOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestBitwiseNotOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestBitwiseAndOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestBitwiseOrOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestBitwiseXorOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestLeftShiftOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestRightShiftOperator()
    {
        RunTest(beforeCode: """
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
    }


    [Fact]
    public void TestImplicitOperator()
    {
        RunTest(beforeCode: """
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
    }

    [Fact]
    public void TestExplicitOperator()
    {
        RunTest(beforeCode: """
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
    }

    // The checked operator wasn't being handled by Roslyn, it's going to be fixed with https://github.com/dotnet/roslyn/pull/77102
    [Fact(Skip = "https://github.com/dotnet/roslyn/issues/77101")]
    public void TestExplicitCheckedOperator()
    {
        RunTest(beforeCode: """
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
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public static explicit operator byte(MyClass value) { throw null; }
                +         public static explicit operator checked byte(MyClass value) { throw null; }
                      }
                  }
                """);
    }
}
