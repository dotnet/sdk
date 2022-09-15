// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotChangeVisibilityTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotChangeVisibility(settings, context));

        /*
         * Tests for:
         * - Reduce visibility of type
         * - Expand visibility of type
         * - Expand visibility of member
         * - Restricting visibility of protected member inside sealed type
         * - Restricting visibility of protected member inside type without accessible constructor
         * - Restricting visibility of member
         * - Including/not including internal symbols
         * - Strict mode
         */

        public static TheoryData<string, string, CompatDifference[], CompatDifference[]> TestCases => new()
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
},
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
new CompatDifference[] {},
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
new CompatDifference[] {},
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside sealed type
            // We suppress the warning for declaring protected members in sealed types,
            // since we want to check for a different diagnostic.
            {
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {},
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside type without accessible constructor
            {
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {},
new CompatDifference[] {}
            },
            // Reduce visibility of member
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
},
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            }
        };

        public static TheoryData<string, string, CompatDifference[], CompatDifference[]> StrictMode => new()
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
},
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotExpandVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
},
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotExpandVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
},
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotExpandVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            },
            // Reducing visibility of protected member inside sealed type
            // We suppress the warning for declaring protected members in sealed types,
            // since we want to check for a different diagnostic.
            {
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {},
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside type without accessible constructor
            {
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {},
new CompatDifference[] {}
            },
            // Reduce visibility of member
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
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
},
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            }
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void EnsureDiagnosticIsReported(
            string leftSyntax,
            string rightSyntax,
            CompatDifference[] expectedWithInternal,
            CompatDifference[] expectedWithoutInternal)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differWithInternal = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: true));
            ApiComparer differWithoutInternal = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: false));

            IEnumerable<CompatDifference> actualWithInternal = differWithInternal.GetDifferences(left, right);
            IEnumerable<CompatDifference> actualWithoutInternal = differWithoutInternal.GetDifferences(left, right);

            Assert.Equal(expectedWithInternal, actualWithInternal);
            Assert.Equal(expectedWithoutInternal, actualWithoutInternal);
        }

        [Theory]
        [MemberData(nameof(StrictMode))]
        public void EnsureDiagnosticIsReportedInStrictMode(
            string leftSyntax,
            string rightSyntax,
            CompatDifference[] expectedWithInternal,
            CompatDifference[] expectedWithoutInternal)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differWithInternal = new(s_ruleFactory, new ApiComparerSettings(
                includeInternalSymbols: true,
                strictMode: true));
            ApiComparer differWithoutInternal = new(s_ruleFactory, new ApiComparerSettings(
                includeInternalSymbols: false,
                strictMode: true));

            IEnumerable<CompatDifference> actualWithInternal = differWithInternal.GetDifferences(left, right);
            IEnumerable<CompatDifference> actualWithoutInternal = differWithoutInternal.GetDifferences(left, right);

            Assert.Equal(expectedWithInternal, actualWithInternal);
            Assert.Equal(expectedWithoutInternal, actualWithoutInternal);
        }
    }
}
