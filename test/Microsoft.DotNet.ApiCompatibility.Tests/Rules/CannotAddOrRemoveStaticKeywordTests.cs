// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotAddOrRemoveStaticKeywordTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotAddOrRemoveStaticKeyword(settings, context));

        private static string CreateType(string s, params object[] args) => string.Format(@"
namespace CompatTests {{
  public{0} First
  {{
{1}
  }}
}}
", s, string.Join("\n", args));

        private static CompatDifference[] CreateDifferences(params (DifferenceType dt, string diagId, string memberId)[] args)
        {
            var differences = new CompatDifference[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                differences[i] = CompatDifference.CreateWithDefaultMetadata(
                    args[i].diagId,
                    string.Empty,
                    args[i].dt,
                    args[i].memberId);
            }
            return differences;
        }

        public static IEnumerable<object[]> RemoveStaticCases()
        {
            // Remove static from method
            yield return new object[] {
                CreateType(" class", " public static void F() {}"),
                CreateType(" class", " public void F() {}"),
                CreateDifferences((DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "M:CompatTests.First.F")),
            };
            
            // Remove static from property
            yield return new object[] {
                CreateType(" class", " public static int F { get; }"),
                CreateType(" class", " public int F { get; }"),
                CreateDifferences((DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "P:CompatTests.First.F"),
                                    (DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "M:CompatTests.First.get_F")),
            };
            
            // Remove static from field
            yield return new object[] {
                CreateType(" class", " public static int F;"),
                CreateType(" class", " public int F;"),
                CreateDifferences((DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "F:CompatTests.First.F")),
            };
            
            // Remove static from event
            yield return new object[] {
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public static event EventHandler F;"),
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public event EventHandler F;"),
                CreateDifferences((DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "M:CompatTests.First.add_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "M:CompatTests.First.remove_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Removed, DiagnosticIds.CannotRemoveStaticFromMember, "E:CompatTests.First.F")),
            };
        }

        public static IEnumerable<object[]> AddStaticCases()
        {
            // Add static to method
            yield return new object[] {
                CreateType(" class", " public void F() {}"),
                CreateType(" class", " public static void F() {}"),
                CreateDifferences((DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "M:CompatTests.First.F")),
            };
            
            // Add static to property
            yield return new object[] {
                CreateType(" class", " public int F { get; }"),
                CreateType(" class", " public static int F { get; }"),
                CreateDifferences((DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "P:CompatTests.First.F"),
                                    (DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "M:CompatTests.First.get_F")),
            };
            
            // Add static to field
            yield return new object[] {
                CreateType(" class", " public int F;"),
                CreateType(" class", " public static int F;"),
                CreateDifferences((DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "F:CompatTests.First.F")),
            };
            
            // Add static to event
            yield return new object[] {
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public event EventHandler F;"),
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public static event EventHandler F;"),
                CreateDifferences((DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "M:CompatTests.First.add_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "M:CompatTests.First.remove_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "E:CompatTests.First.F")),
            };
        }

        public static IEnumerable<object[]> AddStaticToTypeCases()
        {
            // Add static to class
            yield return new object[] {
                CreateType(" class", " public void F() {}"),
                CreateType(" static class", " public static void F() {}"),
                // We expect one difference for the type becoming static and one for the method
                // becoming static (since static class requires static members)
                CreateDifferences((DifferenceType.Added, DiagnosticIds.CannotAddStaticToType, "T:CompatTests.First"),
                                  (DifferenceType.Added, DiagnosticIds.CannotAddStaticToMember, "M:CompatTests.First.F")),
            };
        }

        public static IEnumerable<object[]> RemoveStaticFromTypeCases()
        {
            // Remove static from class - this is compatible
            yield return new object[] {
                CreateType(" static class", " public static void F() {}"),
                CreateType(" class", " public static void F() {}"),
                CreateDifferences(), // No differences expected
            };
        }

        [Theory]
        [MemberData(nameof(RemoveStaticCases))]
        [MemberData(nameof(AddStaticCases))]
        [MemberData(nameof(AddStaticToTypeCases))]
        [MemberData(nameof(RemoveStaticFromTypeCases))]
        public static void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void EnsureNoCrashWhenMembersDoNotExist()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First {
    public static void F() {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First {
    public static void G() {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            // Register CannotAddOrRemoveStaticKeyword and MemberMustExist rules as this test validates both.
            ApiComparer differ = new(s_ruleFactory.WithRule((settings, context) => new MembersMustExist(settings, context)));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void ReproductionCaseFromIssue()
        {
            // This is the reproduction case from the issue
            string leftSyntax = @"
namespace CompatTests
{
    public class Class1
    {
        public static string Foo => ""net8.0"";
        public string Bar => ""net8.0"";
    }
}
";
            string rightSyntax = @"
namespace CompatTests
{
    public class Class1
    {
        public string Foo => ""net10.0"";
        public static string Bar => ""net10.0"";
    }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            // We expect 4 differences: 2 for Foo (property + getter), 2 for Bar (property + getter)
            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveStaticFromMember, string.Empty, DifferenceType.Removed, "P:CompatTests.Class1.Foo"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveStaticFromMember, string.Empty, DifferenceType.Removed, "M:CompatTests.Class1.get_Foo"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddStaticToMember, string.Empty, DifferenceType.Added, "P:CompatTests.Class1.Bar"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddStaticToMember, string.Empty, DifferenceType.Added, "M:CompatTests.Class1.get_Bar"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
