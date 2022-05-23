// Copyright (c) .NET Foundation and contributors. All rights reserved.
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
    public class EnumValuesMustMatchTests
    {
        [Fact]
        public static void DifferencesReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 1,
    B = 1,
    C = 2,
    D = 3,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.A"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void RemovedEnum()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
  public enum Second {}
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            Assert.NotEmpty(differences);
        }

        [Fact]
        public static void AddedEnum()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 1,
    B = 1,
    C = 2,
    D = 3,
  }
  public enum Second {}
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            Assert.NotEmpty(differences);
        }

        [Fact]
        public static void BackingStoreChanged()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First: short {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public enum First: int {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.A"),
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.B"),
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.C"),
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.D"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
