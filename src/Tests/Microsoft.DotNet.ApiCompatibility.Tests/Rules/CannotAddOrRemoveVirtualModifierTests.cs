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
    public class CannotAddOrRemoveVirtualModifierTests
    {
        [Fact]
        public static void DifferencesReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public abstract class First {
    public virtual void F() {}
    public void G() {}
    public abstract void H();
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public abstract class First {
    public void F() {}
    public virtual void G() {}
    public virtual void H() {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotRemoveVirtualModifier, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F"),
                new CompatDifference(DiagnosticIds.CannotAddVirtualModifier, string.Empty, DifferenceType.Added, "M:CompatTests.First.G"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void Null()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First {
    public virtual void F() {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First {
    public virtual void G() {}
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
        public static void StrictMode()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public abstract class First {
    public virtual void F() {}
    public void G() {}
    public abstract void H();
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public abstract class First {
    public void F() {}
    public virtual void G() {}
    public virtual void H() {}
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
                new CompatDifference(DiagnosticIds.CannotRemoveVirtualModifier, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F"),
                new CompatDifference(DiagnosticIds.CannotAddVirtualModifier, string.Empty, DifferenceType.Added, "M:CompatTests.First.G"),
                new CompatDifference(DiagnosticIds.CannotAddVirtualModifier, string.Empty, DifferenceType.Added, "M:CompatTests.First.H"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
