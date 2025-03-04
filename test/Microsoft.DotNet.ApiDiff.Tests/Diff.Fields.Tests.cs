// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffFieldTests : DiffBaseTests
{
    #region Fields

    [Fact]
    public Task TestFieldAdd() => RunTestAsync(
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
    public Task TesttFieldChange() => RunTestAsync(
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
    public Task TesttFieldDelete() => RunTestAsync(
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
}
