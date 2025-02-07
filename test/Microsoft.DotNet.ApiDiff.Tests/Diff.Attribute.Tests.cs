// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffAttributeTests : DiffBaseTests
{
    #region Type attributes

    [Fact]
    public void TestTypeAttributeAdd()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [MyAttribute]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [MyAttribute]
                      public class MyClass
                      {
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    [Fact]
    public void TestTypeAttributeDeleteAndAdd()
    {
        // Added APIs always show up at the end.
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [MyAttribute1]
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    [MyAttribute2]
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     [System.AttributeUsage(System.AttributeTargets.All)]
                -     public class MyAttribute1Attribute : System.Attribute
                -     {
                -         public MyAttribute1Attribute() { }
                -     }
                -     [MyAttribute1]
                +     [MyAttribute2]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsage(System.AttributeTargets.All)]
                +     public class MyAttribute2Attribute : System.Attribute
                +     {
                +         public MyAttribute2Attribute() { }
                +     }
                  }
                """,
                attributesToExclude: []);
    }

    [Fact]
    public void TestTypeAttributeSwitch()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    [MyAttribute1]
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    [MyAttribute2]
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     [MyAttribute1]
                +     [MyAttribute2]
                      public class MyClass
                      {
                      }
                  }
                """);
    }

    [Fact]
    public void TestTypeChangeAndAttributeAdd()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass1
                    {
                        public MyClass1() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [MyAttribute]
                    public class MyClass2
                    {
                        public MyClass2() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public class MyClass1
                -     {
                -         public MyClass1() { }
                -     }
                +     [MyAttribute]
                +     public class MyClass2
                +     {
                +         public MyClass2() { }
                +     }
                  }
                """);
    }

    [Fact]
    public void TestTypeChangeButAttributeStays()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [MyAttribute]
                    public class MyClass1
                    {
                        public MyClass1() { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [MyAttribute]
                    public class MyClass2
                    {
                        public MyClass2() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     [MyAttribute]
                -     public class MyClass1
                -     {
                -         public MyClass1() { }
                -     }
                +     [MyAttribute]
                +     public class MyClass2
                +     {
                +         public MyClass2() { }
                +     }
                  }
                """);
    }

    #endregion

    #region Member attributes

    #endregion

    #region Parameter attributes

    //[Fact]
    [ActiveIssue("Parameter attributes are not showing up in the syntax tree.")]
    internal void TestParameterAttributeAdd()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass(int x) { }
                        public void MyMethod(int y) { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass([MyAttribute] int x) { }
                        public void MyMethod([MyAttribute] int y) { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(int x) { }
                +         public MyClass([MyAttribute] int x) { }
                -         public void MyMethod(int y) { }
                +         public void MyMethod([MyAttribute] int y) { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    #endregion

    #region Attribute list expansion

    [Fact]
    public void TestTypeAttributeListExpansion()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    public class MyClass
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    [MyAttribute1, MyAttribute2]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [MyAttribute1]
                +     [MyAttribute2]
                      public class MyClass
                      {
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    [Fact]
    public void TestMethodAttributeListExpansion()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass(int x) { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    public class MyClass
                    {
                        [MyAttribute1, MyAttribute2]
                        public MyClass(int x) { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         [MyAttribute1]
                +         [MyAttribute2]
                          public MyClass(int x) { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    //[Fact]
    [ActiveIssue("Parameter attributes are not showing up in the syntax tree.")]
    internal void TestParameterAttributeListNoExpansion()
    {
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass(int x) { }
                        public void MyMethod(int y) { }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public class MyAttribute2Attribute : System.Attribute
                    {
                        public MyAttribute2Attribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass([MyAttribute1, MyAttribute2] int x) { }
                        public void MyMethod([MyAttribute1, MyAttribute2] int y) { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public MyClass(int x) { }
                +         public MyClass([MyAttribute1, MyAttribute2] int x) { }
                -         public void MyMethod(int y) { }
                +         public void MyMethod([MyAttribute1, MyAttribute2] int y) { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);
    }

    #endregion

    #region Attribute exclusion

    [Fact]
    public void TestSuppressAllDefaultAttributes()
    {
        // The attributes that should get hidden in this test must all be part of
        // the DiffGeneratorFactory.DefaultAttributesToExclude list.
        RunTest(beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                    }
                }
                """,
                afterCode: """
                using System;
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Text")]
                    [MyAttribute]
                    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Text")]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [MyAttribute]
                      public class MyClass
                      {
                      }
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute() { }
                +     }
                  }
                """,
                attributesToExclude: null); // null forces using the default list
    }

    [Fact]
    public void TestSuppressNone()
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
                using System;
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Text")]
                    [MyAttribute]
                    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Text")]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                +     [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Text")]
                +     [MyAttribute]
                +     [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Text")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsage(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute() { }
                +     }
                  }
                """,
                attributesToExclude: []); // empty list is respected as is
    }

    [Fact]
    public void TestSuppressOnlyCustom()
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
                using System;
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Text")]
                    [MyAttribute]
                    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Text")]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                +     [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Text")]
                +     [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Text")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsage(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute() { }
                +     }
                  }
                """,
                attributesToExclude: ["T:MyNamespace.MyAttributeAttribute"]); // Overrides the default list
    }

    [Fact]
    public void TestSuppressTypeAndAttributeUsage()
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
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                    }
                    [MyAttribute]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: "",
                hideImplicitDefaultConstructors: true,
                attributesToExclude: ["T:MyNamespace.MyAttributeAttribute"], // Exclude the attribute decorating other APIs
                apisToExclude: ["T:MyNamespace.MyAttributeAttribute"]); // Excludes the type definition of the attribute itself
    }

    #endregion
}
