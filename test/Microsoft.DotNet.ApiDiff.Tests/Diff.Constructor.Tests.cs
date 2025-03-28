// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffConstructorTests : DiffBaseTests
{
    #region Constructors

    [Fact]
    public Task ConstructorAdd() => RunTestAsync(
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
                """, hideImplicitDefaultConstructors: true);

    [Fact]
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
                """, hideImplicitDefaultConstructors: true);

    [Fact]
    public Task ConstructorDelete() => RunTestAsync(
                beforeCode: """
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
                -         public MyClass(int x);
                      }
                  }
                """, hideImplicitDefaultConstructors: true);

    #endregion

    #region Primary constructors

    [Fact]
    public Task PrimaryConstructorAdd() => RunTestAsync(
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
                +         public MyClass(string x);
                +         public string X { get; }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);

    [Fact]
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
                """, hideImplicitDefaultConstructors: true);

    [Fact]
    public Task PrimaryConstructorDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass(string x)
                    {
                        public string X { get; } = x;
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
                -         public MyClass(string x);
                -         public string X { get; }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);

    #endregion

    #region Hide Default constructors

    [Fact]
    public Task AddHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                    public class MyClass1
                    {
                        public MyClass1() { }
                    }
                    public class MyClass2
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyClass1
                +     {
                +     }
                +     public class MyClass2
                +     {
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task ChangeHideImplicitDefaultConstructors() => RunTestAsync(
                // This isn't really a modification, but a deletion and an addition, and since
                // deletions show up before additions, that explains the order of the expected code.
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyBeforeClass1
                    {
                        public MyBeforeClass1() { }
                    }
                    public class MyBeforeClass2
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyAfterClass1
                    {
                        public MyAfterClass1() { }
                    }
                    public class MyAfterClass2
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyBeforeClass1
                -     {
                -     }
                -     public class MyBeforeClass2
                -     {
                -     }
                +     public class MyAfterClass1
                +     {
                +     }
                +     public class MyAfterClass2
                +     {
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task NestedTypeAddHideImplicitDefaultConstructors() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public delegate void MyDelegate();
                    public class MyClass1
                    {
                        public class MyNestedClass1
                        {
                            public MyNestedClass1() { }
                        }
                        public class MyNestedClass2
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public class MyClass1
                +     {
                +         public class MyNestedClass1
                +         {
                +         }
                +         public class MyNestedClass2
                +         {
                +         }
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task NestedTypeChangeHideImplicitDefaultConstructors() => RunTestAsync(
                // This isn't really a modification, but a deletion and an addition, and since
                // deletions show up before additions, that explains the order of the expected code.
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass1
                    {
                        public class MyBeforeNestedClass1
                        {
                            public MyBeforeNestedClass1() { }
                        }
                        public class MyBeforeNestedClass2
                        {
                        }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass1
                    {
                        public class MyAfterNestedClass1
                        {
                            public MyAfterNestedClass1() { }
                        }
                        public class MyAfterNestedClass2
                        {
                        }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass1
                      {
                -         public class MyBeforeNestedClass1
                -         {
                -         }
                -         public class MyBeforeNestedClass2
                -         {
                -         }
                +         public class MyAfterNestedClass1
                +         {
                +         }
                +         public class MyAfterNestedClass2
                +         {
                +         }
                      }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    #endregion

    #region Visibility

    [Fact]
    public Task DefaultConstructorMakePrivate() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public MyClass()
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
                        private MyClass()
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
                -         public MyClass();
                      }
                  }
                """);

    [Fact]
    public Task DefaultConstructorMakePublic() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        private MyClass()
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
                        public MyClass()
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
                +         public MyClass();
                      }
                  }
                """);

    #endregion
}
