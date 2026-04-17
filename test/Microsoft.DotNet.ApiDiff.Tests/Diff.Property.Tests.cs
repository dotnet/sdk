// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffPropertyTests : DiffBaseTests
{
    [Fact]
    public Task PropertyAdd() => RunTestAsync(
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
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertyChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyBeforeProperty { get { throw null; } set { } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyAfterProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyBeforeProperty { get; set; }
                +         public int MyAfterProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertyDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
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
                -         public int MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertySetAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; }
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertySetRemove() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; }
                      }
                  }
                """);

    [Fact]
    public Task PropertySetVisibilityProtected() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } protected set { } }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; protected set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertySetVisibilityPrivate() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; private set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public int MyProperty { get; }
                      }
                  }
                """);

    [Fact]
    public Task PropertyReturnChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public float MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public float MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertyNullabilityAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int? MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int MyProperty { get; set; }
                +         public int? MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public Task PropertyNullabilityRemove() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int? MyProperty { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                      public class MyClass
                      {
                -         public int? MyProperty { get; set; }
                +         public int MyProperty { get; set; }
                      }
                  }
                """);

    [Fact]
    public void PropertyIndentationFallback_UsesNestingDepthWhenLeadingTriviaIsMissing()
    {
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(
            """
            namespace MyNamespace
            {
                public class MyClass
                {
                    public static bool IsSupported { get; }
                }
            }
            """).GetCompilationUnitRoot();

        PropertyDeclarationSyntax property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        CompilationUnitSyntax rootWithStrippedPropertyTrivia = root.ReplaceNode(property, property.WithoutLeadingTrivia());
        PropertyDeclarationSyntax propertyWithMissingLeadingTrivia = rootWithStrippedPropertyTrivia.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();

        MethodInfo getLeadingTriviaForDiff = typeof(MemoryOutputDiffGenerator)
            .GetMethod("GetLeadingTriviaForDiff", BindingFlags.NonPublic | BindingFlags.Static)!;

        var leadingTrivia = (Microsoft.CodeAnalysis.SyntaxTriviaList)getLeadingTriviaForDiff.Invoke(null, [propertyWithMissingLeadingTrivia])!;
        Assert.Equal("        ", leadingTrivia.ToFullString());
    }

    #region Exclusions

    [Fact]
    public Task ExcludeAddedProperty() => RunTestAsync(
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
                        public int MyProperty { get; set; }
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    [Fact]
    public Task ExcludeModifiedProperty() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty1 { get; set; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty2 { get; set; }
                    }
                }
                """,
                expectedCode: "",
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty1", "P:MyNamespace.MyClass.MyProperty2"]);

    [Fact]
    public Task ExcludeRemovedProperty() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public class MyClass
                    {
                        public int MyProperty { get { throw null; } set { } }
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
                apisToExclude: ["P:MyNamespace.MyClass.MyProperty"]);

    #endregion
}
