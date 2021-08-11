﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class MembersMustExistTests_Strict
    {
        [Fact]
        public static void MissingMembersOnLeftAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
  }
  public delegate void EventHandler(object sender, System.EventArgs e);
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public void ShouldReportMethod(string a, string b) { }
    public string ShouldReportMissingProperty { get; }
    public string this[int index] { get => string.Empty; }
    public event EventHandler ShouldReportMissingEvent;
    public int ReportMissingField = 0;
  }

  public delegate void EventHandler(object sender, System.EventArgs e);
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.ShouldReportMethod(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.get_ShouldReportMissingProperty"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.get_Item(System.Int32)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.add_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.remove_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.ReportMissingField"),
            };

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
        }

        [Fact]
        public static void HiddenMemberInRightIsNotReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class FirstBase
  {
    public void MyMethod() { }
    public string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public virtual string MyVirtualMethod() => string.Empty;
  }
  public class Second : FirstBase { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class FirstBase
  {
    public void MyMethod() { }
    public string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public virtual string MyVirtualMethod() => string.Empty;
  }
  public class Second : FirstBase
  {
    public new void MyMethod() { }
    public new string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public new T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public override string MyVirtualMethod() => string.Empty;
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            Assert.Empty(differences);
        }

        [Fact]
        public static void MultipleOverridesMissingInLeftAreReported()
        {
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    public string MultipleOverrides(string a, int b, int c) => string.Empty;
  }
}
";

            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, int b, int c) => string.Empty;
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.String)"),
            };

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void IncludeInternalsIsRespectedForMembers_IndividualAssemblies(bool includeInternals)
        {
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    internal string MultipleOverrides(string a, int b, int c) => string.Empty;
    internal int InternalProperty { get; set; }
  }
}
";

            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    internal int InternalProperty { get; }
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected = new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.Int32)"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.set_InternalProperty(System.Int32)"),
                };

                Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
            }
            else
            {
                Assert.Empty(differences);
            }
        }

        [Fact]
        public static void MissingMembersOnBothSidesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public string MissingMethodRight() => string.Empty;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public void MissingMethodLeft(string a, string b) { }
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MissingMethodRight"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MissingMethodLeft(System.String,System.String)"),
            };

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
        }

        [Fact]
        public static void MultipleRightsMissingMembersOnLeftAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
        }
      }
    }
  }
}
"};

            ApiComparer differ = new();
            differ.StrictMode = true;
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expectedDiffs =
            {
                new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                },
                Array.Empty<CompatDifference>(),
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expectedDiffs, differences);
        }
    }
}
