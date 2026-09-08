// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffConstructorTests : DiffBaseTests
{
    #region Constructors

    [TestMethod]
    public Task ConstructorAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                        public MyClass(int x)
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public MyClass(int x);
                      }
                  }
                """);

    [TestMethod]
    public Task ConstructorChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass(bool x)
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass(int x)
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(bool x);
                +         public MyClass(int x);
                      }
                  }
                """);

    [TestMethod]
    public Task ConstructorDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                        public MyClass(int x)
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(int x);
                      }
                  }
                """);

    #endregion

    #region Primary constructors


    [TestMethod]
    public Task PrimaryConstructorAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass(string x)
                    {
                        public MyClass() : this("") { }
                        public string X { get; } = x;
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public MyClass(string x);
                +         public string X { get; }
                      }
                  }
                """);

    [TestMethod]
    public Task PrimaryConstructorChange() => RunTestAsync(
                // This isn't really a modification, but a deletion and an addition, and since
                // deletions show up before additions, that explains the order of the expected code.
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass(double x)
                    {
                        public double X { get; } = x;
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass(string x)
                    {
                        public string X { get; } = x;
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(double x);
                -         public double X { get; }
                +         public string X { get; }
                +         public MyClass(string x);
                      }
                  }
                """);

    [TestMethod]
    public Task PrimaryConstructorDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass(string x)
                    {
                        public MyClass() : this("") { }
                        public string X { get; } = x;
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(string x);
                -         public string X { get; }
                      }
                  }
                """);

    #endregion

    #region Visibility

    [TestMethod]
    public Task DefaultConstructorMakePrivate()
    {
        string beforeCode = """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass()
                        {
                        }
                    }
                }
                """;
        string afterCode = """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        private MyClass()
                        {
                        }
                    }
                }
                """;
        return RunTestAsync(
            before: [($"{AssemblyName}.dll", beforeCode)],
            after: [($"{AssemblyName}.dll", afterCode)],
            expected: []); // No results expected as the API is not getting removed
    }

    [TestMethod]
    public Task DefaultConstructorMakePublic()
    {
        string beforeCode = """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        private MyClass()
                        {
                        }
                    }
                }
                """;
        string afterCode = """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass()
                        {
                        }
                    }
                }
                """;
        return RunTestAsync(
            before: [($"{AssemblyName}.dll", beforeCode)],
            after: [($"{AssemblyName}.dll", afterCode)],
            expected: []); // No results expected as the API is not getting added
    }

    #endregion
}
