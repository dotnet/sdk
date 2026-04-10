// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffFieldTests : DiffBaseTests
{
    #region Fields

    [Fact]
    public Task FieldAdd() => RunTestAsync(
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
                        public int _myField;
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int _myField;
                      }
                  }
                """);

    [Fact]
    public Task tFieldChange() => RunTestAsync(
                // Test both change of type and change of name
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myInt1;
                        public double _myField;
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myInt2;
                        public float _myField;
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public double _myField;
                +         public float _myField;
                -         public int _myInt1;
                +         public int _myInt2;
                      }
                  }
                """);

    [Fact]
    public Task tFieldDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField;
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
                      public class MyClass
                      {
                -         public int _myField;
                      }
                  }
                """);

    #endregion

    #region Field lists

    [Fact]
    public Task FieldListAdd() => RunTestAsync(
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
                        public int _myField1, _myField2;
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int _myField1;
                +         public int _myField2;
                      }
                  }
                """);

    [Fact]
    public Task FieldListDataTypeChange() => RunTestAsync(
            beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField1, _myField2;
                    }
                }
                """,
            afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public double _myField1, _myField2;
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int _myField1;
                +         public double _myField1;
                -         public int _myField2;
                +         public double _myField2;
                      }
                  }
                """);

    [Fact]
    public Task FieldListOrderChange() => RunTestAsync(
            beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField1, _myField2;
                    }
                }
                """,
            afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField2, _myField1;
                    }
                }
                """,
            expectedCode: ""); // No change expected

    [Fact]
    public Task FieldListNameChange() => RunTestAsync(
            beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField1, _myField2;
                    }
                }
                """,
            afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField3, _myField4;
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int _myField1;
                -         public int _myField2;
                +         public int _myField3;
                +         public int _myField4;
                      }
                  }
                """);

    [Fact]
    public Task FieldVisibilityChange() => RunTestAsync(
            beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int _myField1, _myField2;
                    }
                }
                """,
            afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        protected int _myField1, _myField2;
                    }
                }
                """,
            expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int _myField1;
                +         protected int _myField1;
                -         public int _myField2;
                +         protected int _myField2;
                      }
                  }
                """);

    #endregion
}
