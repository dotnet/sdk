// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffAttributeTests : DiffBaseTests
{
    #region Type attributes

    [Fact]
    public Task TestTypeAttributeAdd() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestTypeAttributeDeleteAndAdd() =>
        // Added APIs always show up at the end.
        RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestTypeAttributeSwitch() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestTypeChangeAndAttributeAdd() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestTypeChangeButAttributeStays() => RunTestAsync(
                beforeCode: """
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

    #endregion

    #region Member attributes

    [Fact]
    public Task TestMemberAttributeAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                        public void MyMethod() { }
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
                    public class MyClass
                    {
                        [MyAttribute]
                        public void MyMethod() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         [MyAttribute]
                          public void MyMethod() { }
                      }
                  }
                """, hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestMemberAttributeDeleteAndAdd() =>
        // Added APIs always show up at the end.
        RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttribute1Attribute : System.Attribute
                    {
                        public MyAttribute1Attribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass() { }
                        [MyAttribute1]
                        public void MyMethod() { }
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
                    public class MyClass
                    {
                        public MyClass() { }
                        [MyAttribute2]
                        public void MyMethod() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     [System.AttributeUsage(System.AttributeTargets.All)]
                -     public class MyAttribute1Attribute : System.Attribute
                -     {
                -     }
                      public class MyClass
                      {
                -         [MyAttribute1]
                +         [MyAttribute2]
                          public void MyMethod() { }
                      }
                +     [System.AttributeUsage(System.AttributeTargets.All)]
                +     public class MyAttribute2Attribute : System.Attribute
                +     {
                +     }
                  }
                """,
                hideImplicitDefaultConstructors: true,
                attributesToExclude: []);

    [Fact]
    public Task TestMemberAttributeSwitch() => RunTestAsync(
                beforeCode: """
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
                        public MyClass() { }
                        [MyAttribute1]
                        public void MyMethod() { }
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
                        public MyClass() { }
                        [MyAttribute2]
                        public void MyMethod() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         [MyAttribute1]
                +         [MyAttribute2]
                          public void MyMethod() { }
                      }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestMemberChangeAndAttributeAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass() { }
                        public void MyMethod1() { }
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
                    public class MyClass
                    {
                        public MyClass() { }
                        [MyAttribute]
                        public void MyMethod2() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public void MyMethod1() { }
                +         [MyAttribute]
                +         public void MyMethod2() { }
                      }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    [Fact]
    public Task TestMemberChangeButAttributeStays() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public MyAttributeAttribute() { }
                    }
                    public class MyClass
                    {
                        public MyClass() { }
                        [MyAttribute]
                        public void MyMethod1() { }
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
                    public class MyClass
                    {
                        public MyClass() { }
                        [MyAttribute]
                        public void MyMethod2() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         [MyAttribute]
                -         public void MyMethod1() { }
                +         [MyAttribute]
                +         public void MyMethod2() { }
                      }
                  }
                """,
                hideImplicitDefaultConstructors: true);

    #endregion

    #region Parameter attributes

    //[Fact]
    [Fact(Skip = "Parameter attributes are not showing up in the syntax tree.")]
    internal void TestParameterAttributeAdd() => RunTestAsync(
                beforeCode: """
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

    #endregion

    #region Attribute list expansion

    [Fact]
    public Task TestTypeAttributeListExpansion() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestMethodAttributeListExpansion() => RunTestAsync(
                beforeCode: """
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

    //[Fact]
    [Fact(Skip = "Parameter attributes are not showing up in the syntax tree.")]
    internal void TestParameterAttributeListNoExpansion() => RunTestAsync(
                beforeCode: """
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

    #endregion

    #region Attribute exclusion

    [Fact]
    public Task TestSuppressAllDefaultAttributes() =>
        // The attributes that should get hidden in this test must all be part of
        // the DiffGeneratorFactory.DefaultAttributesToExclude list.
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

    [Fact]
    public Task TestSuppressNone() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestSuppressOnlyCustom() => RunTestAsync(
                beforeCode: """
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

    [Fact]
    public Task TestSuppressTypeAndAttributeUsage() => RunTestAsync(
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

    #endregion

    #region Attributes with arguments

    [Fact]
    public Task TestAttributeWithArguments() => RunTestAsync(
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
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public class MyAttributeAttribute : System.Attribute
                    {
                        public string First { get; }
                        public string Second { get; }
                        public MyAttributeAttribute(string first, string second)
                        {
                            First = first;
                            Second = second;
                        }
                    }
                    [MyAttribute(first: "First", second: "Second")]
                    public class MyClass
                    {
                        public MyClass() { }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [MyAttribute("First", "Second")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsage(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute(string first, string second) { }
                +         public string First { get { throw null; } }
                +         public string Second { get { throw null; } }
                +     }
                  }
                """, attributesToExclude: []);


    #endregion
}

[System.AttributeUsage(System.AttributeTargets.All)]
public class MyAttribute(string first, string second) : System.Attribute
{
    public string First { get; } = first;
    public string Second { get; } = second;
}

[MyAttribute(first: "First", second: "Second")]
public class MyClass
{
}
