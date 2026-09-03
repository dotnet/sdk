// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffFieldTests : DiffBaseTests
{
    #region Fields

    [TestMethod]
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

    [TestMethod]
    public Task FieldChange() => RunTestAsync(
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

    [TestMethod]
    public Task FieldDelete() => RunTestAsync(
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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
