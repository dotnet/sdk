// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffAttributeTests : DiffBaseTests
{
    #region Type attributes

    [Fact]
    public Task TypeAttributeAdd() => RunTestAsync(
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
                +     [MyNamespace.MyAttributeAttribute]
                      public class MyClass
                      {
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task TypeAttributeDeleteAndAdd() =>
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
                -     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                -     public class MyAttribute1Attribute : System.Attribute
                -     {
                -         public MyAttribute1Attribute();
                -     }
                -     [MyNamespace.MyAttribute1Attribute]
                +     [MyNamespace.MyAttribute2Attribute]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                +     public class MyAttribute2Attribute : System.Attribute
                +     {
                +         public MyAttribute2Attribute();
                +     }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task TypeAttributeSwitch() => RunTestAsync(
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
                -     [MyNamespace.MyAttribute1Attribute]
                +     [MyNamespace.MyAttribute2Attribute]
                      public class MyClass
                      {
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task TypeChangeAndAttributeAdd() => RunTestAsync(
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
                -         public MyClass1();
                -     }
                +     [MyNamespace.MyAttributeAttribute]
                +     public class MyClass2
                +     {
                +         public MyClass2();
                +     }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task TypeChangeButAttributeStays() => RunTestAsync(
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
                -     [MyNamespace.MyAttributeAttribute]
                -     public class MyClass1
                -     {
                -         public MyClass1();
                -     }
                +     [MyNamespace.MyAttributeAttribute]
                +     public class MyClass2
                +     {
                +         public MyClass2();
                +     }
                  }
                """,
                attributesToExclude: []);

    #endregion

    #region Member attributes

    [Fact]
    public Task MemberAttributeAdd() => RunTestAsync(
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
                +         [MyNamespace.MyAttributeAttribute]
                          public void MyMethod();
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task MemberAttributeDeleteAndAdd() =>
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
                -     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                -     public class MyAttribute1Attribute : System.Attribute
                -     {
                -         public MyAttribute1Attribute();
                -     }
                      public class MyClass
                      {
                -         [MyNamespace.MyAttribute1Attribute]
                +         [MyNamespace.MyAttribute2Attribute]
                          public void MyMethod();
                      }
                +     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                +     public class MyAttribute2Attribute : System.Attribute
                +     {
                +         public MyAttribute2Attribute();
                +     }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task MemberAttributeSwitch() => RunTestAsync(
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
                -         [MyNamespace.MyAttribute1Attribute]
                +         [MyNamespace.MyAttribute2Attribute]
                          public void MyMethod();
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task MemberChangeAndAttributeAdd() => RunTestAsync(
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
                -         public void MyMethod1();
                +         [MyNamespace.MyAttributeAttribute]
                +         public void MyMethod2();
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task MemberChangeButAttributeStays() => RunTestAsync(
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
                -         [MyNamespace.MyAttributeAttribute]
                -         public void MyMethod1();
                +         [MyNamespace.MyAttributeAttribute]
                +         public void MyMethod2();
                      }
                  }
                """,
                attributesToExclude: []);

    #endregion

    #region Parameter attributes

    //[Fact]
    [Fact(Skip = "Parameter attributes are not showing up in the syntax tree.")]
    public Task ParameterAttributeAdd() => RunTestAsync(
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
                -         public MyClass(int x);
                +         public MyClass([MyNamespace.MyAttributeAttribute] int x);
                -         public void MyMethod(int y);
                +         public void MyMethod([MyNamespace.MyAttributeAttribute] int y);
                      }
                  }
                """,
                attributesToExclude: []);

    #endregion

    #region Attribute list expansion

    [Fact]
    public Task TypeAttributeListExpansion() => RunTestAsync(
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
                +     [MyNamespace.MyAttribute1Attribute]
                +     [MyNamespace.MyAttribute2Attribute]
                      public class MyClass
                      {
                      }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task MethodAttributeListExpansion() => RunTestAsync(
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
                +         [MyNamespace.MyAttribute1Attribute]
                +         [MyNamespace.MyAttribute2Attribute]
                          public MyClass(int x);
                      }
                  }
                """,
                attributesToExclude: []);

    //[Fact]
    [Fact(Skip = "Parameter attributes are not showing up in the syntax tree.")]
    public Task ParameterAttributeListNoExpansion() => RunTestAsync(
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
                -         public MyClass(int x);
                +         public MyClass([MyNamespace.MyAttribute1Attribute, MyNamespace.MyAttribute2Attribute] int x);
                -         public void MyMethod(int y);
                +         public void MyMethod([MyNamespace.MyAttribute1Attribute, MyNamespace.MyAttribute2Attribute] int y);
                      }
                  }
                """,
                attributesToExclude: []);

    #endregion

    #region Attribute exclusion

    [Fact]
    public Task SuppressAllDefaultAttributesUsedByTool()
    {
        // The attributes that should get hidden in this test must all be part of
        // the AttributesToExclude.txt file that the ApiDiff tool uses by default.

        FileInfo file = new FileInfo("AttributesToExclude.txt");
        if (!file.Exists)
        {
            throw new FileNotFoundException($"{file.FullName} file not found.");
        }
        string[] attributesToExclude = File.ReadAllLines(file.FullName);

        return RunTestAsync(
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
                +     [MyNamespace.MyAttributeAttribute]
                      public class MyClass
                      {
                      }
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute();
                +     }
                  }
                """,
                attributesToExclude: attributesToExclude);
    }

    [Fact]
    public Task SuppressNone() => RunTestAsync(
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
                +     [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
                +     [System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute("Text")]
                +     [MyNamespace.MyAttributeAttribute]
                +     [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("Text")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute();
                +     }
                  }
                """,
                attributesToExclude: []);

    [Fact]
    public Task SuppressOnlyCustom() => RunTestAsync(
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
                +     [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
                +     [System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute("Text")]
                +     [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("Text")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute();
                +     }
                  }
                """,
                attributesToExclude: ["T:MyNamespace.MyAttributeAttribute"]); // Overrides the default list

    [Fact]
    public Task SuppressChangedAttribute() => RunTestAsync(
                // Includes dummy property addition so that something else shows up in the diff, but not the attribute change.
                beforeCode: """
                namespace MyNamespace
                {
                    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Original text")]
                    public class MyClass
                    {
                    }
                }
                """,
                afterCode: """
                using System;
                namespace MyNamespace
                {
                    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Changed text")]
                    public class MyClass
                    {
                        public int X { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int X { get; }
                      }
                  }
                """,
                attributesToExclude: ["T:System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"]); // Overrides the default list

    [Fact]
    public Task SuppressAttributesRepeatedWithDifferentArguments() => RunTestAsync(
                // Include dummy property
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
                    [System.Runtime.Versioning.UnsupportedOSPlatform("tvos")]
                    [System.Runtime.Versioning.UnsupportedOSPlatform("linux")]
                    public class MyClass
                    {
                        public int X { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int X { get; }
                      }
                  }
                """,
                attributesToExclude: ["T:System.Runtime.Versioning.UnsupportedOSPlatformAttribute"]); // Overrides the default list

    [Fact]
    public Task SuppressTypeAndAttributeUsage() => RunTestAsync(
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
                attributesToExclude: ["T:MyNamespace.MyAttributeAttribute"], // Exclude the attribute decorating other APIs
                apisToExclude: ["T:MyNamespace.MyAttributeAttribute"]); // Excludes the type definition of the attribute itself

    #endregion

    #region Attributes with arguments

    [Fact]
    public Task AttributeWithArguments() => RunTestAsync(
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
                +     [MyNamespace.MyAttributeAttribute("First", "Second")]
                      public class MyClass
                      {
                      }
                +     [System.AttributeUsageAttribute(System.AttributeTargets.All)]
                +     public class MyAttributeAttribute : System.Attribute
                +     {
                +         public MyAttributeAttribute(string first, string second);
                +         public string First { get; }
                +         public string Second { get; }
                +     }
                  }
                """,
                attributesToExclude: []); // Make sure to show AttributeUsage, which by default is suppressed

    [Fact]
    public Task AttributesRepeatedWithDifferentArguments() => RunTestAsync(
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
                    [System.Runtime.Versioning.UnsupportedOSPlatform("tvos")]
                    [System.Runtime.Versioning.UnsupportedOSPlatform("linux")]
                    public class MyClass
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     [System.Runtime.Versioning.UnsupportedOSPlatformAttribute("tvos")]
                +     [System.Runtime.Versioning.UnsupportedOSPlatformAttribute("linux")]
                      public class MyClass
                      {
                      }
                  }
                """,
                attributesToExclude: null); // null forces using the default list

    #endregion
}
