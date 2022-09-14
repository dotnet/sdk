// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotReduceVisibilityTests
    {
        /*
         * Tests for:
         * - Reduce visibility of type
         * - Expand visibility of type
         * - Expand visibility of member
         * - Restricting visibility of protected member inside sealed type
         * - Restricting visibility of protected member inside type without accessible constructor
         * - Restricting visibility of member
         */

        public static TheoryData<string, string, CompatDifference[]> TestCases => new()
        {
            // Reduce visibility of type
            {
                @"
namespace CompatTests
{
  public class First {}
}
",
                @"
namespace CompatTests
{
  internal class First {}
}
",
new CompatDifference[] {}
            },
            // Expand visibility of type
            {
                @"
namespace CompatTests
{
  internal class First {}
}
",
                @"
namespace CompatTests
{
  public class First {}
}
",
new CompatDifference[] {}
            },
            // Expand visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
new CompatDifference[] {}
            },
            // Restricting visibility of protected member inside sealed type
            // We suppress the warning for declaring protected members in sealed types,
            // since we want to check for a different diagnostic.
            {
                @"
namespace CompatTests
{
  public sealed class First {
    public int F;
  }
}
",
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0628
    protected int F;
  }
}
",
new CompatDifference[] {}
            },
            // Restricting visibility of protected member inside type without accessible constructor
            {
                @"
namespace CompatTests
{
  public class First {
    private First() {}
    public int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    private First() {}
    protected int F;
  }
}
",
new CompatDifference[] {}
            },
            // Restricting visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
new CompatDifference[] {}
            }
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            TestRuleFactory s_ruleFactory = new();
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }
    }
}
